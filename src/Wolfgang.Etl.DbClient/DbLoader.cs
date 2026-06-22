using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wolfgang.Etl.Abstractions;

namespace Wolfgang.Etl.DbClient;

/// <summary>
/// Loads records into a database via INSERT or UPDATE commands.
/// Uses Dapper for parameter mapping from POCO properties.
/// </summary>
/// <typeparam name="TRecord">
/// The POCO type representing a single row. Properties are mapped to command
/// parameters by name or <c>[Column("name")]</c> attribute.
/// </typeparam>
/// <remarks>
/// <para>
/// Two transaction modes are supported:
/// </para>
/// <list type="bullet">
///   <item><b>Caller-managed</b> — pass a <see cref="DbTransaction"/> to the constructor.
///   The loader uses it but never commits or rolls back. The caller is responsible for
///   transaction lifetime.</item>
///   <item><b>Auto-managed</b> — pass <see langword="null"/> for the transaction parameter.
///   The loader creates its own transaction, commits on success, and rolls back on
///   exception.</item>
/// </list>
/// <para>
/// The caller owns the <see cref="DbConnection"/> lifetime — the loader does not
/// open, close, or dispose it. The connection must be open before calling
/// <c>LoadAsync</c>.
/// </para>
/// <para>
/// <b>Thread safety.</b> A <see cref="DbLoader{TRecord}"/> instance is not safe for
/// concurrent <c>LoadAsync</c> calls. Internal state (stopwatch, progress counters)
/// assumes a single load in flight. Build a separate instance per concurrent load.
/// </para>
/// <para>
/// Command timeout uses the Dapper/ADO.NET default (typically 30 seconds).
/// A dedicated <c>CommandTimeout</c> property is planned (see GitHub issue #25).
/// </para>
/// </remarks>
public class DbLoader<TRecord> : LoaderBase<TRecord, DbReport>
    where TRecord : notnull
{
    // ------------------------------------------------------------------
    // Fields
    // ------------------------------------------------------------------

    // _ownsConnection tracks whether LoadWorkerAsync is responsible for the
    // connection's OpenAsync + Dispose. True for the DbProviderFactory ctor
    // overloads; false when the caller passes a pre-opened DbConnection.
    private readonly DbConnection _connection;
    private readonly bool _ownsConnection;
    private readonly string _commandText;
    private readonly DbTransaction? _callerTransaction;
    private readonly bool _ownsTransaction;
    private readonly ILogger _logger;
    private readonly IProgressTimer? _progressTimer;
    private readonly Stopwatch _stopwatch = new();
    private int _progressTimerWired;
    private int _batchSize = 1;



    // ------------------------------------------------------------------
    // Constructors
    // ------------------------------------------------------------------

    /// <summary>
    /// Initializes a new <see cref="DbLoader{TRecord}"/> with a custom SQL command.
    /// </summary>
    /// <param name="connection">An open <see cref="DbConnection"/>. The caller owns its lifetime.</param>
    /// <param name="commandText">The SQL INSERT or UPDATE command to execute per record.</param>
    /// <param name="transaction">
    /// An optional <see cref="DbTransaction"/>. If null, the loader creates and manages
    /// its own transaction (commit on success, rollback on failure).
    /// </param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="connection"/> or <paramref name="commandText"/> is null.
    /// </exception>
    public DbLoader
    (
        DbConnection connection,
        string commandText,
        DbTransaction? transaction = null,
        ILogger<DbLoader<TRecord>>? logger = null
    )
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _commandText = commandText ?? throw new ArgumentNullException(nameof(commandText));
        _callerTransaction = transaction;
        _ownsTransaction = transaction == null;
        _logger = logger ?? (ILogger)NullLogger.Instance;
    }



    /// <summary>
    /// Initializes a new <see cref="DbLoader{TRecord}"/> that auto-generates
    /// an INSERT statement from <c>[Table]</c> and <c>[Column]</c> attributes on
    /// <typeparamref name="TRecord"/>.
    /// </summary>
    /// <param name="connection">An open <see cref="DbConnection"/>. The caller owns its lifetime.</param>
    /// <param name="writeMode">Determines whether to generate INSERT or UPDATE commands.</param>
    /// <param name="transaction">
    /// An optional <see cref="DbTransaction"/>. If null, the loader creates and manages
    /// its own transaction.
    /// </param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="TRecord"/> does not have a <c>[Table]</c> attribute, or
    /// <paramref name="writeMode"/> is <see cref="WriteMode.Update"/> and no <c>[Key]</c>
    /// properties exist.
    /// </exception>
    public DbLoader
    (
        DbConnection connection,
        WriteMode writeMode,
        DbTransaction? transaction = null,
        ILogger<DbLoader<TRecord>>? logger = null
    )
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _commandText = writeMode == WriteMode.Update
            ? DbCommandBuilder.BuildUpdate<TRecord>()
            : DbCommandBuilder.BuildInsert<TRecord>();
        _callerTransaction = transaction;
        _ownsTransaction = transaction == null;
        _logger = logger ?? (ILogger)NullLogger.Instance;
    }



    /// <summary>
    /// Initializes a new <see cref="DbLoader{TRecord}"/> that owns the
    /// connection's lifetime. The connection is created from the supplied
    /// <see cref="DbProviderFactory"/>, opened lazily before loading begins,
    /// and disposed when loading completes (or throws).
    /// </summary>
    /// <param name="factory">
    /// The provider-specific factory (e.g. <c>Microsoft.Data.SqlClient
    /// .SqlClientFactory.Instance</c>, <c>Npgsql.NpgsqlFactory.Instance</c>).
    /// </param>
    /// <param name="connectionString">The provider-specific connection string.</param>
    /// <param name="commandText">The SQL INSERT or UPDATE command to execute per record.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="factory"/>, <paramref name="connectionString"/>, or
    /// <paramref name="commandText"/> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="factory"/> returned a null connection from
    /// <see cref="DbProviderFactory.CreateConnection"/>.
    /// </exception>
    public DbLoader
    (
        DbProviderFactory factory,
        string connectionString,
        string commandText,
        ILogger<DbLoader<TRecord>>? logger = null
    )
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        if (connectionString == null) throw new ArgumentNullException(nameof(connectionString));
        _commandText = commandText ?? throw new ArgumentNullException(nameof(commandText));

        var conn = factory.CreateConnection()
            ?? throw new InvalidOperationException
            (
                $"{factory.GetType().FullName}.CreateConnection() returned null. " +
                "The provider factory does not produce DbConnection instances."
            );
        conn.ConnectionString = connectionString;
        _connection = conn;
        _ownsConnection = true;
        _ownsTransaction = true;  // auto-managed transaction inside the owned connection
        _logger = logger ?? (ILogger)NullLogger.Instance;
    }



    /// <summary>
    /// Internal constructor for timer injection (testing).
    /// </summary>
    internal DbLoader
    (
        DbConnection connection,
        string commandText,
        IProgressTimer timer,
        ILogger<DbLoader<TRecord>>? logger = null
    )
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _commandText = commandText ?? throw new ArgumentNullException(nameof(commandText));
        _ownsTransaction = true;
        _progressTimer = timer ?? throw new ArgumentNullException(nameof(timer));
        _logger = logger ?? (ILogger)NullLogger.Instance;
    }



    // ------------------------------------------------------------------
    // Properties
    // ------------------------------------------------------------------

    /// <summary>
    /// The SQL command text being executed per record.
    /// </summary>
    public string CommandText => _commandText;



    /// <summary>
    /// How long each <c>ExecuteAsync</c> call may run before timing out.
    /// <c>null</c> (the default) means "use the ADO.NET provider's default",
    /// typically 30 seconds.
    /// </summary>
    /// <remarks>
    /// Maps onto Dapper's <c>commandTimeout</c> parameter (an <c>int?</c> count
    /// of seconds). Fractional seconds are truncated. Applies to every batch,
    /// not the whole load — so a batched insert of 10 batches each gets the
    /// full timeout independently.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The assigned value is negative.
    /// </exception>
    public TimeSpan? CommandTimeout
    {
        get => _commandTimeout;
        set
        {
            if (value.HasValue && value.Value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException
                (
                    nameof(value),
                    value,
                    "CommandTimeout cannot be negative. Use null to fall back to the ADO.NET default."
                );
            }
            _commandTimeout = value;
        }
    }

    private TimeSpan? _commandTimeout;

    private int? CommandTimeoutSeconds => _commandTimeout.HasValue
        ? (int)_commandTimeout.Value.TotalSeconds
        : (int?)null;



    /// <summary>
    /// How <see cref="CommandText"/> is interpreted by the ADO.NET provider.
    /// Defaults to <see cref="CommandType.Text"/> (a SQL INSERT / UPDATE).
    /// Set to <see cref="CommandType.StoredProcedure"/> to invoke a stored
    /// procedure by name per record (or per batch when <see cref="BatchSize"/>
    /// is &gt; 1); <see cref="CommandText"/> then holds the procedure name.
    /// </summary>
    public CommandType CommandType { get; set; } = CommandType.Text;



    /// <summary>
    /// When <see langword="true"/>, the loader runs the full pipeline —
    /// enumerates the source, evaluates <c>SkipItemCount</c> /
    /// <c>MaximumItemCount</c>, increments progress counters, fires the
    /// progress-timer callback, and emits all the usual log messages — but
    /// **skips the actual <c>ExecuteAsync</c> call**. The database is not
    /// modified.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Useful for validating an ETL pipeline (source feed, mapping, batching,
    /// throttling) against production without writing anything, or for
    /// estimating how long a write would take.
    /// </para>
    /// <para>
    /// The auto-managed-transaction path still <c>BeginTransaction</c> /
    /// <c>Commit</c>s — those are connection-level operations and are needed
    /// for the open/dispose lifecycle. They have no effect on the database
    /// because no writes happen inside the transaction.
    /// </para>
    /// <para>
    /// Default <see langword="false"/> preserves the prior behavior.
    /// </para>
    /// </remarks>
    public bool DryRun { get; set; }



    /// <summary>
    /// How the loader reacts when a single row's <c>ExecuteAsync</c> throws.
    /// Defaults to <see cref="RowErrorHandling.Abort"/>, which preserves the
    /// v0.4.0 behavior (first failure rolls back the transaction and
    /// propagates).
    /// </summary>
    /// <remarks>
    /// <see cref="RowErrorHandling.Skip"/> only takes effect on the
    /// per-record path (<see cref="BatchSize"/> == 1, the default). A failed
    /// batch cannot be safely attributed to a single row without per-item
    /// retry, so the load still aborts even in Skip mode when
    /// <c>BatchSize &gt; 1</c>. See <see cref="RowErrorHandling.Skip"/>.
    /// </remarks>
    public RowErrorHandling ErrorHandling { get; set; } = RowErrorHandling.Abort;



    /// <summary>
    /// Maximum number of row-level failures the loader will tolerate before
    /// aborting the load with an <see cref="InvalidOperationException"/>
    /// whose <see cref="Exception.InnerException"/> is the last underlying
    /// row failure. Defaults to <c>0</c>, meaning "unlimited" (every failure
    /// is reported via <see cref="RowFailed"/> and processing continues).
    /// </summary>
    /// <remarks>
    /// Only consulted when <see cref="ErrorHandling"/> is
    /// <see cref="RowErrorHandling.Skip"/>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The assigned value is negative.
    /// </exception>
    public int MaxErrorCount
    {
        get => _maxErrorCount;
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException
                (
                    nameof(value),
                    value,
                    "MaxErrorCount cannot be negative. Use 0 for unlimited."
                );
            }
            _maxErrorCount = value;
        }
    }

    private int _maxErrorCount;



    /// <summary>
    /// Number of rows that have failed to load so far (incremented for every
    /// <see cref="RowErrorHandling.Skip"/>-handled exception). Resets to 0
    /// at the start of each <c>LoadAsync</c> call.
    /// </summary>
    public int CurrentErrorCount { get; private set; }



    /// <summary>
    /// Raised once per row that fails when <see cref="ErrorHandling"/> is
    /// <see cref="RowErrorHandling.Skip"/>. The handler receives the failing
    /// record, the underlying exception, and the row's 1-based item index.
    /// </summary>
    /// <remarks>
    /// Typical use: capture the failing record into a dead-letter store. The
    /// handler is invoked synchronously inside the load loop, so a slow
    /// handler will slow the entire load.
    /// </remarks>
    public event EventHandler<RowFailedEventArgs<TRecord>>? RowFailed;



    /// <summary>
    /// Commit the auto-managed transaction every N successfully-loaded rows.
    /// Defaults to <c>0</c>, meaning "commit only once at the end of the load"
    /// (the v0.4.0 behavior). Larger values let very long loads survive a
    /// mid-flight failure with most of the work persisted.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only takes effect when the loader manages the transaction (i.e. the
    /// caller did NOT pass a <c>DbTransaction</c> to the constructor) AND
    /// <see cref="BatchSize"/> is <c>1</c>. With a caller-supplied transaction
    /// the loader never commits, so this knob has no effect; with
    /// <c>BatchSize &gt; 1</c> the per-batch commit boundary is the
    /// <see cref="BatchSize"/>'s own batch, which we currently don't subdivide.
    /// </para>
    /// <para>
    /// Trade-off: a partial-progress failure with <c>BatchCommitSize &gt; 0</c>
    /// leaves earlier committed batches in the destination. You lose all-or-
    /// nothing semantics in exchange for resumability and lower undo-log
    /// pressure on the database.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The assigned value is negative.
    /// </exception>
    public int BatchCommitSize
    {
        get => _batchCommitSize;
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException
                (
                    nameof(value),
                    value,
                    "BatchCommitSize cannot be negative. Use 0 for 'commit only at end'."
                );
            }
            _batchCommitSize = value;
        }
    }

    private int _batchCommitSize;



    /// <summary>
    /// Number of records sent per <c>ExecuteAsync</c> call. Defaults to <c>1</c>
    /// (one round-trip per record). Larger values pass an <c>IEnumerable&lt;TRecord&gt;</c>
    /// to Dapper, which amortizes per-call overhead (parameter parsing, command setup)
    /// and lets the ADO.NET provider reuse the prepared command across rows.
    /// </summary>
    /// <remarks>
    /// On networked databases (SQL Server, PostgreSQL, MySQL) this is typically the
    /// largest single performance lever in the loader. Memory cost is one buffered
    /// <see cref="List{T}"/> of up to <c>BatchSize</c> records at a time.
    /// <para>
    /// <c>SkipItemCount</c> and <c>MaximumItemCount</c> are still honored at the
    /// per-record granularity — skipped records never enter the batch buffer,
    /// and buffering stops accepting new items once the cumulative-plus-buffered
    /// count would exceed <c>MaximumItemCount</c>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the assigned value is less than 1.
    /// </exception>
    public int BatchSize
    {
        get => _batchSize;
        set
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException
                (
                    nameof(value),
                    value,
                    "BatchSize must be at least 1."
                );
            }

            _batchSize = value;
        }
    }



    /// <inheritdoc/>
    protected override DbReport CreateProgressReport()
    {
        return new DbReport
        (
            CurrentItemCount,
            CurrentSkippedItemCount,
            _commandText,
            _stopwatch.ElapsedMilliseconds
        );
    }



    /// <summary>
    /// Returns a snapshot progress report. Visible to the test assembly via InternalsVisibleTo.
    /// </summary>
    internal DbReport GetProgressReport() => CreateProgressReport();



    /// <inheritdoc/>
    protected override IProgressTimer CreateProgressTimer(IProgress<DbReport> progress)
    {
        if (_progressTimer != null)
        {
            // Atomic 0 → 1 transition. Matches DbExtractor's pattern and prevents
            // double-subscription if CreateProgressTimer is ever called concurrently.
            if (Interlocked.CompareExchange(ref _progressTimerWired, 1, 0) == 0)
            {
                _progressTimer.Elapsed += () => progress.Report(CreateProgressReport());
            }

            return _progressTimer;
        }

        return base.CreateProgressTimer(progress);
    }



    /// <inheritdoc/>
    /// <remarks>
    /// Dispatches to one of two paths based on transaction ownership (decided at
    /// construction time). Splitting the two flows keeps each one short and removes
    /// the repeated <c>if (_ownsTransaction &amp;&amp; transaction != null)</c> guard
    /// that the combined version needed.
    /// </remarks>
    protected override async Task LoadWorkerAsync
    (
        IAsyncEnumerable<TRecord> items,
        CancellationToken token
    )
    {
        _stopwatch.Restart();
        CurrentErrorCount = 0;
        LogLoadingStarted();

        // Owned-connection ctor path: open before the first execute, dispose at
        // the end. Wrapped in try/finally so the connection is released even
        // when LoadWith*Async throws (the caller's exception still propagates).
        if (_ownsConnection && _connection.State != ConnectionState.Open)
        {
            await _connection.OpenAsync(token).ConfigureAwait(false);
        }

        try
        {
            if (_ownsTransaction)
            {
                await LoadWithAutoTransactionAsync(items, token).ConfigureAwait(false);
            }
            else
            {
                await LoadWithCallerTransactionAsync(items, token).ConfigureAwait(false);
            }

            LogLoadingCompleted();
        }
        finally
        {
            if (_ownsConnection)
            {
#if NET5_0_OR_GREATER
                await _connection.DisposeAsync().ConfigureAwait(false);
#else
                _connection.Dispose();
#endif
            }
        }
    }



    /// <summary>
    /// Load path when the loader owns the transaction. Begins, executes, and
    /// commits on success; rolls back on exception; always disposes.
    /// </summary>
    private async Task LoadWithAutoTransactionAsync
    (
        IAsyncEnumerable<TRecord> items,
        CancellationToken token
    )
    {
        if (_batchCommitSize == 0 || _batchSize > 1)
        {
            // Single-transaction path: BatchCommitSize == 0 (commit only at
            // end) OR BatchSize > 1 (per-execute batching uses its own
            // commit boundary, not subdivided here).
            await LoadWithAutoTransactionSingleAsync(items, token).ConfigureAwait(false);
            return;
        }

        // Chunked-commit path: BatchCommitSize > 0 AND BatchSize == 1.
        await LoadWithAutoTransactionChunkedAsync(items, token).ConfigureAwait(false);
    }



    private async Task LoadWithAutoTransactionSingleAsync(IAsyncEnumerable<TRecord> items, CancellationToken token)
    {
        var transaction = await BeginAutoTransactionAsync(token).ConfigureAwait(false);

        try
        {
            await ExecuteItemsAsync(items, transaction, token).ConfigureAwait(false);
            await CommitAutoTransactionAsync(transaction, token).ConfigureAwait(false);
        }
        catch
        {
            await RollbackAutoTransactionAsync(transaction, token).ConfigureAwait(false);
            throw;
        }
        finally
        {
            await DisposeTransactionAsync(transaction).ConfigureAwait(false);
        }
    }



    private async Task LoadWithAutoTransactionChunkedAsync(IAsyncEnumerable<TRecord> items, CancellationToken token)
    {
        // Materialize up to BatchCommitSize items at a time, then run each
        // chunk through the single-transaction path. A failure rolls back
        // ONLY the current chunk; previously-committed chunks survive.
        var enumerator = items.GetAsyncEnumerator(token);
        try
        {
            while (true)
            {
                var chunk = new List<TRecord>(_batchCommitSize);
                while (chunk.Count < _batchCommitSize && await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    chunk.Add(enumerator.Current);
                }

                if (chunk.Count == 0)
                {
                    break;
                }

                await LoadWithAutoTransactionSingleAsync(AsAsyncEnumerable(chunk), token).ConfigureAwait(false);
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }
    }



    // Local IAsyncEnumerable adapter over a materialized List<T>. Used by the
    // chunked-commit path; avoiding the System.Linq.Async extension keeps the
    // src/ project dependency-free of that package.
    private static async IAsyncEnumerable<TRecord> AsAsyncEnumerable(List<TRecord> source)
    {
        foreach (var item in source)
        {
            yield return item;
        }
        await Task.CompletedTask.ConfigureAwait(false);
    }



    /// <summary>
    /// Load path when the caller supplied the transaction. The loader executes
    /// against it but never commits, rolls back, or disposes — those are the
    /// caller's responsibilities.
    /// </summary>
    private Task LoadWithCallerTransactionAsync
    (
        IAsyncEnumerable<TRecord> items,
        CancellationToken token
    )
    {
        return ExecuteItemsAsync(items, _callerTransaction, token);
    }



    private Task ExecuteItemsAsync
    (
        IAsyncEnumerable<TRecord> items,
        DbTransaction? transaction,
        CancellationToken token
    )
    {
        return _batchSize <= 1
            ? ExecuteItemsPerRowAsync(items, transaction, token)
            : ExecuteItemsBatchedAsync(items, transaction, token);
    }



    private async Task ExecuteItemsPerRowAsync
    (
        IAsyncEnumerable<TRecord> items,
        DbTransaction? transaction,
        CancellationToken token
    )
    {
        await foreach (var item in items.WithCancellation(token).ConfigureAwait(false))
        {
            token.ThrowIfCancellationRequested();

            if (CurrentItemCount >= MaximumItemCount)
            {
                LogDebugMaxReached();
                break;
            }

            if (CurrentSkippedItemCount < SkipItemCount)
            {
                IncrementCurrentSkippedItemCount();
                LogDebugItemSkipped();
                continue;
            }

            if (!DryRun && !await TryExecuteItemAsync(item, transaction, token).ConfigureAwait(false))
            {
                continue;
            }

            IncrementCurrentItemCount();
            LogDebugRecordLoaded();
        }
    }



    /// <summary>
    /// Per-row ExecuteAsync wrapper. Returns <see langword="true"/> when the
    /// row succeeded (or threw in <see cref="RowErrorHandling.Abort"/> mode —
    /// then the exception propagates and the return value isn't observed);
    /// <see langword="false"/> when the row failed and was skipped under
    /// <see cref="RowErrorHandling.Skip"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// <see cref="MaxErrorCount"/> was reached after this row's failure in
    /// <see cref="RowErrorHandling.Skip"/> mode.
    /// </exception>
    private async Task<bool> TryExecuteItemAsync(TRecord item, DbTransaction? transaction, CancellationToken token)
    {
        try
        {
            await _connection.ExecuteAsync
            (
                new CommandDefinition
                (
                    _commandText,
                    item,
                    transaction,
                    CommandTimeoutSeconds,
                    CommandType,
                    cancellationToken: token
                )
            ).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when
        (
            ErrorHandling == RowErrorHandling.Skip
            && ex is not OperationCanceledException
        )
        {
            CurrentErrorCount++;
            var itemIndex = CurrentItemCount + CurrentSkippedItemCount + CurrentErrorCount;
            RowFailed?.Invoke(this, new RowFailedEventArgs<TRecord>(item, ex, itemIndex));
            LogRowErrorSkipped(ex, itemIndex);

            if (_maxErrorCount > 0 && CurrentErrorCount >= _maxErrorCount)
            {
                throw new InvalidOperationException
                (
                    $"DbLoader.MaxErrorCount ({_maxErrorCount}) exceeded. " +
                    $"Last failure on item index {itemIndex}; see inner exception.",
                    ex
                );
            }

            return false;
        }
    }



    /// <summary>
    /// Batched load path. Buffers up to <see cref="BatchSize"/> records, then sends
    /// the buffer to Dapper as an <see cref="IEnumerable{T}"/> — Dapper executes the
    /// command once per item using the already-prepared parameter map.
    /// </summary>
    private async Task ExecuteItemsBatchedAsync
    (
        IAsyncEnumerable<TRecord> items,
        DbTransaction? transaction,
        CancellationToken token
    )
    {
        var batch = new List<TRecord>(_batchSize);

        await foreach (var item in items.WithCancellation(token).ConfigureAwait(false))
        {
            token.ThrowIfCancellationRequested();

            // Stop accepting items once we've buffered enough to hit MaximumItemCount.
            // CurrentItemCount only advances when a batch flushes, so the still-pending
            // batch.Count must be included in the comparison.
            if (CurrentItemCount + batch.Count >= MaximumItemCount)
            {
                LogDebugMaxReached();
                break;
            }

            if (CurrentSkippedItemCount < SkipItemCount)
            {
                IncrementCurrentSkippedItemCount();
                LogDebugItemSkipped();
                continue;
            }

            batch.Add(item);

            if (batch.Count >= _batchSize)
            {
                await FlushBatchAsync(batch, transaction, token).ConfigureAwait(false);
            }
        }

        if (batch.Count > 0)
        {
            await FlushBatchAsync(batch, transaction, token).ConfigureAwait(false);
        }
    }



    private async Task FlushBatchAsync
    (
        List<TRecord> batch,
        DbTransaction? transaction,
        CancellationToken token
    )
    {
        if (!DryRun)
        {
            await _connection.ExecuteAsync
            (
                new CommandDefinition
                (
                    _commandText,
                    batch,
                    transaction,
                    CommandTimeoutSeconds,
                    CommandType,
                    cancellationToken: token
                )
            ).ConfigureAwait(false);
        }

        for (var i = 0; i < batch.Count; i++)
        {
            IncrementCurrentItemCount();
            LogDebugRecordLoaded();
        }

        batch.Clear();
    }



    private static Task DisposeTransactionAsync(DbTransaction transaction)
    {
#if NET5_0_OR_GREATER
        return transaction.DisposeAsync().AsTask();
#else
#pragma warning disable CA1849, VSTHRD103
        transaction.Dispose();
#pragma warning restore CA1849, VSTHRD103
        return System.Threading.Tasks.Task.CompletedTask;
#endif
    }



    private async Task<DbTransaction> BeginAutoTransactionAsync(CancellationToken token)
    {
#if NET5_0_OR_GREATER
        var transaction = await _connection.BeginTransactionAsync(token).ConfigureAwait(false);
#else
        _ = token; // Suppress unused parameter warning — token is used in the #if branch above
        var transaction = _connection.BeginTransaction();
        await System.Threading.Tasks.Task.CompletedTask.ConfigureAwait(false);
#endif
        LogDebugTransactionCreated();
        return transaction;
    }



    private async Task CommitAutoTransactionAsync(DbTransaction transaction, CancellationToken token)
    {
#if NET5_0_OR_GREATER
        await transaction.CommitAsync(token).ConfigureAwait(false);
#else
        _ = token; // Suppress unused parameter warning — token is used in the #if branch above
        transaction.Commit();
        await System.Threading.Tasks.Task.CompletedTask.ConfigureAwait(false);
#endif
        LogDebugTransactionCommitted();
    }



    /// <summary>
    /// Attempts to roll back an auto-managed transaction after an error.
    /// If the rollback itself fails, the rollback exception is logged at Error level
    /// and the original exception (which triggered the rollback) is allowed to propagate
    /// unchanged. This avoids masking the root cause while still surfacing the rollback
    /// failure in logs.
    /// </summary>
    private async Task RollbackAutoTransactionAsync(DbTransaction transaction, CancellationToken token)
    {
        try
        {
#if NET5_0_OR_GREATER
            await transaction.RollbackAsync(token).ConfigureAwait(false);
#else
            _ = token; // Suppress unused parameter warning — token is used in the #if branch above
            transaction.Rollback();
            await System.Threading.Tasks.Task.CompletedTask.ConfigureAwait(false);
#endif
            LogDebugTransactionRolledBack();
        }
        catch (Exception rollbackEx)
        {
            _logger.LogError
            (
                rollbackEx,
                "Failed to rollback transaction after error. The original exception will propagate"
            );
        }
    }



    // ------------------------------------------------------------------
    // Logging helpers
    // ------------------------------------------------------------------

    private void LogLoadingStarted()
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation
            (
                "Loading started for {RecordType}. CommandText={CommandText}, " +
                "Transaction={TransactionMode}, SkipItemCount={SkipItemCount}, " +
                "MaximumItemCount={MaximumItemCount}",
                typeof(TRecord).Name,
                _commandText,
                _ownsTransaction ? "auto-managed" : "caller-managed",
                SkipItemCount,
                MaximumItemCount
            );
        }
    }



    private void LogLoadingCompleted()
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation
            (
                "Loading completed for {RecordType}: {ItemCount} items loaded, " +
                "{SkippedCount} skipped in {ElapsedMs}ms",
                typeof(TRecord).Name,
                CurrentItemCount,
                CurrentSkippedItemCount,
                _stopwatch.ElapsedMilliseconds
            );
        }
    }



    private void LogDebugTransactionCreated()
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Auto-managed transaction created");
        }
    }



    private void LogDebugTransactionCommitted()
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Auto-managed transaction committed");
        }
    }



    private void LogDebugTransactionRolledBack()
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Auto-managed transaction rolled back");
        }
    }



    private void LogDebugMaxReached()
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug
            (
                "MaximumItemCount ({MaximumItemCount}) reached, stopping loading",
                MaximumItemCount
            );
        }
    }



    private void LogDebugItemSkipped()
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug
            (
                "Skipping item ({SkippedCount}/{SkipItemCount})",
                CurrentSkippedItemCount,
                SkipItemCount
            );
        }
    }



    private void LogRowErrorSkipped(Exception ex, long itemIndex)
    {
        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning
            (
                ex,
                "Row {ItemIndex} failed and was skipped ({ErrorCount} errors so far). " +
                "Configured ErrorHandling=Skip; MaxErrorCount={MaxErrorCount}",
                itemIndex,
                CurrentErrorCount,
                _maxErrorCount
            );
        }
    }



    private void LogDebugRecordLoaded()
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug
            (
                "Loaded record (item #{ItemCount})",
                CurrentItemCount
            );
        }
    }
}
