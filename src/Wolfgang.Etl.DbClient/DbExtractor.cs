using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wolfgang.Etl.Abstractions;

namespace Wolfgang.Etl.DbClient;

/// <summary>
/// Extracts records from a database query as an asynchronous stream.
/// Uses Dapper for column-to-property mapping, supporting <c>[Column]</c> attribute
/// and convention-based name matching.
/// </summary>
/// <typeparam name="TRecord">
/// The POCO type representing a single row. Properties are mapped from result set
/// columns by name or <c>[Column("name")]</c> attribute.
/// </typeparam>
/// <remarks>
/// <para>
/// The caller owns the <see cref="DbConnection"/> lifetime — the extractor does not
/// open, close, or dispose it. The connection must be open before calling
/// <c>ExtractAsync</c>.
/// </para>
/// <para>
/// An optional <see cref="DbTransaction"/> can be provided for isolation level control.
/// The extractor never commits or rolls back the transaction.
/// </para>
/// <para>
/// <b>Thread safety.</b> A <see cref="DbExtractor{TRecord}"/> instance is not safe for
/// concurrent <c>ExtractAsync</c> calls. Internal state (stopwatch, total-count snapshot,
/// progress-counter increments) assumes a single extraction in flight. Build a separate
/// instance per concurrent extraction.
/// </para>
/// <para>
/// Command timeout uses the Dapper/ADO.NET default (typically 30 seconds).
/// A dedicated <c>CommandTimeout</c> property is planned (see GitHub issue #25).
/// </para>
/// </remarks>
public class DbExtractor<TRecord> : ExtractorBase<TRecord, DbReport>
    where TRecord : notnull
{
    // ------------------------------------------------------------------
    // Fields
    // ------------------------------------------------------------------

    // _connection is not `readonly` because the DbProviderFactory ctor overload
    // creates the connection itself; the caller-supplied-DbConnection ctors set
    // it once and never re-assign. _ownsConnection tracks whether ExtractWorkerAsync
    // is responsible for OpenAsync + Dispose.
    private readonly DbConnection _connection;
    private readonly bool _ownsConnection;
    private readonly string _commandText;

    // Defensive snapshot of the caller's parameter dictionary. Copying at
    // construction time guarantees the data query, the default total-count
    // query, and debug logging all see the same values, even if the caller
    // mutates the dictionary they passed in after construction.
    private readonly IDictionary<string, object>? _parameters;

    // Cached Dapper parameter wrapper. Built once at construction from the
    // defensive snapshot and reused across the data query and the default
    // total-count query. Debug logging still reads from _parameters (the
    // dictionary form) — both come from the same snapshot, so they cannot
    // diverge. Dapper treats input-parameter DynamicParameters as read-only
    // during execution, so sharing is safe across this type's documented
    // single-use lifetime.
    private readonly DynamicParameters? _dynamicParameters;
    private readonly DbTransaction? _transaction;
    private readonly ILogger _logger;
    private readonly IProgressTimer? _progressTimer;
    private readonly Stopwatch _stopwatch = new();
    private int _progressTimerWired;
    private int? _totalItemCount;



    // ------------------------------------------------------------------
    // Static initializer
    // ------------------------------------------------------------------

    static DbExtractor()
    {
        ColumnAttributeTypeMapper.Register<TRecord>();
    }



    // ------------------------------------------------------------------
    // Constructors
    // ------------------------------------------------------------------

    /// <summary>
    /// Initializes a new <see cref="DbExtractor{TRecord}"/> with a SQL command.
    /// </summary>
    /// <param name="connection">An open <see cref="DbConnection"/>. The caller owns its lifetime.</param>
    /// <param name="commandText">The SQL query to execute.</param>
    /// <param name="transaction">An optional <see cref="DbTransaction"/> for isolation control.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="connection"/> or <paramref name="commandText"/> is null.
    /// </exception>
    public DbExtractor
    (
        DbConnection connection,
        string commandText,
        DbTransaction? transaction = null,
        ILogger<DbExtractor<TRecord>>? logger = null
    )
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _commandText = commandText ?? throw new ArgumentNullException(nameof(commandText));
        _transaction = transaction;
        _logger = logger ?? (ILogger)NullLogger.Instance;
    }



    /// <summary>
    /// Initializes a new <see cref="DbExtractor{TRecord}"/> with a parameterized SQL command.
    /// </summary>
    /// <param name="connection">An open <see cref="DbConnection"/>. The caller owns its lifetime.</param>
    /// <param name="commandText">The SQL query to execute.</param>
    /// <param name="parameters">
    /// Named parameters for the query. A defensive copy is taken at construction time,
    /// so mutations to the supplied dictionary after construction do not affect the
    /// executed query or the values reported in debug logs.
    /// </param>
    /// <param name="transaction">An optional <see cref="DbTransaction"/> for isolation control.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="connection"/>, <paramref name="commandText"/>, or <paramref name="parameters"/> is null.
    /// </exception>
    public DbExtractor
    (
        DbConnection connection,
        string commandText,
        IDictionary<string, object> parameters,
        DbTransaction? transaction = null,
        ILogger<DbExtractor<TRecord>>? logger = null
    )
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _commandText = commandText ?? throw new ArgumentNullException(nameof(commandText));
        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        // Defensive copy — see the field-level comment on _parameters.
        _parameters = new Dictionary<string, object>(parameters, StringComparer.Ordinal);
        _dynamicParameters = new DynamicParameters(_parameters);
        _transaction = transaction;
        _logger = logger ?? (ILogger)NullLogger.Instance;
    }



    /// <summary>
    /// Initializes a new <see cref="DbExtractor{TRecord}"/> that auto-generates
    /// a SELECT statement from <c>[Table]</c> and <c>[Column]</c> attributes on
    /// <typeparamref name="TRecord"/>.
    /// </summary>
    /// <param name="connection">An open <see cref="DbConnection"/>. The caller owns its lifetime.</param>
    /// <param name="transaction">An optional <see cref="DbTransaction"/> for isolation control.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="TRecord"/> does not have a <c>[Table]</c> attribute.
    /// </exception>
    public DbExtractor
    (
        DbConnection connection,
        DbTransaction? transaction = null,
        ILogger<DbExtractor<TRecord>>? logger = null
    )
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _commandText = DbCommandBuilder.BuildSelect<TRecord>();
        _transaction = transaction;
        _logger = logger ?? (ILogger)NullLogger.Instance;
    }



    /// <summary>
    /// Initializes a new <see cref="DbExtractor{TRecord}"/> that owns the
    /// connection's lifetime. The connection is created from the supplied
    /// <see cref="DbProviderFactory"/>, opened lazily before extraction begins,
    /// and disposed when extraction completes (or throws).
    /// </summary>
    /// <param name="factory">
    /// The provider-specific factory (e.g. <c>Microsoft.Data.SqlClient
    /// .SqlClientFactory.Instance</c>, <c>Npgsql.NpgsqlFactory.Instance</c>).
    /// </param>
    /// <param name="connectionString">The provider-specific connection string.</param>
    /// <param name="commandText">The SQL query to execute.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="factory"/>, <paramref name="connectionString"/>, or
    /// <paramref name="commandText"/> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="factory"/> returned a null connection from
    /// <see cref="DbProviderFactory.CreateConnection"/>.
    /// </exception>
    public DbExtractor
    (
        DbProviderFactory factory,
        string connectionString,
        string commandText,
        ILogger<DbExtractor<TRecord>>? logger = null
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
        _logger = logger ?? (ILogger)NullLogger.Instance;
    }



    /// <summary>
    /// Internal constructor for timer injection (testing).
    /// </summary>
    internal DbExtractor
    (
        DbConnection connection,
        string commandText,
        IProgressTimer timer,
        ILogger<DbExtractor<TRecord>>? logger = null
    )
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _commandText = commandText ?? throw new ArgumentNullException(nameof(commandText));
        _progressTimer = timer ?? throw new ArgumentNullException(nameof(timer));
        _logger = logger ?? (ILogger)NullLogger.Instance;
    }



    // ------------------------------------------------------------------
    // Properties
    // ------------------------------------------------------------------

    /// <summary>
    /// The SQL command text being executed.
    /// </summary>
    public string CommandText => _commandText;



    /// <summary>
    /// How long each command (the extraction query and the
    /// <see cref="TotalCountQuery"/>) may execute before timing out. <c>null</c>
    /// (the default) means "use the ADO.NET provider's default", which is
    /// typically 30 seconds.
    /// </summary>
    /// <remarks>
    /// Maps onto Dapper's <c>commandTimeout</c> parameter (an <c>int?</c> count
    /// of seconds). Fractional seconds in the supplied <see cref="TimeSpan"/>
    /// are truncated. A negative <see cref="TimeSpan"/> is rejected.
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

    // Dapper's commandTimeout parameter is `int?` seconds. Centralized here so
    // every call site uses the same conversion (and so future "0 = infinite"
    // semantics, if needed, only have to flip in one place).
    private int? CommandTimeoutSeconds => _commandTimeout.HasValue
        ? (int)_commandTimeout.Value.TotalSeconds
        : (int?)null;



    /// <summary>
    /// How <see cref="CommandText"/> is interpreted by the ADO.NET provider.
    /// Defaults to <see cref="CommandType.Text"/> (a SQL statement). Set to
    /// <see cref="CommandType.StoredProcedure"/> to invoke a stored procedure
    /// by name; <see cref="CommandText"/> then holds the procedure name.
    /// </summary>
    /// <remarks>
    /// <see cref="CommandType.TableDirect"/> is supported by very few providers
    /// (notably OleDb). It's accepted on this property — Dapper passes it
    /// through — but most consumers should stick to <c>Text</c> or
    /// <c>StoredProcedure</c>.
    /// </remarks>
    public CommandType CommandType { get; set; } = CommandType.Text;



    /// <summary>
    /// When <see langword="true"/>, the extractor opens the connection before
    /// the first command runs and closes it after the enumeration ends. The
    /// connection is NOT disposed — it's returned to the pool for reuse,
    /// which plays better with connection-pool lifetime in web apps and
    /// hosted services.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Default <see langword="false"/> preserves the v0.4.0 behavior: the
    /// caller is responsible for opening the connection before calling
    /// <c>ExtractAsync</c>.
    /// </para>
    /// <para>
    /// Ignored on the owned-connection ctor path (the
    /// <c>(DbProviderFactory, connectionString, …)</c> overload). That path
    /// always manages and disposes the connection because it created it.
    /// </para>
    /// <para>
    /// If the connection is already open when <c>ExtractAsync</c> starts,
    /// it's left open — the extractor only closes connections it itself
    /// opened.
    /// </para>
    /// </remarks>
    public bool ManageConnection { get; set; }



    /// <summary>
    /// Optional override for the parameter set passed to Dapper. Setting this
    /// property takes precedence over any <c>IDictionary&lt;string,object&gt;</c>
    /// supplied via the constructor — useful when the command is a stored
    /// procedure with <c>OUT</c> / <c>INOUT</c> parameters that need to be
    /// declared with <see cref="ParameterDirection"/> values Dapper can't
    /// infer from a plain dictionary.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Caller-owned: the extractor never clones it. After
    /// <c>ExtractAsync</c> completes, read output values via
    /// <c>Parameters.Get&lt;T&gt;("@name")</c>.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// var p = new DynamicParameters();
    /// p.Add("@CustomerId", 42);
    /// p.Add("@TotalCount", dbType: DbType.Int32, direction: ParameterDirection.Output);
    /// var extractor = new DbExtractor&lt;Order&gt;(conn, "usp_GetOrdersForCustomer")
    /// {
    ///     CommandType = CommandType.StoredProcedure,
    ///     Parameters = p
    /// };
    /// var orders = await extractor.ExtractAsync().ToListAsync();
    /// var total = p.Get&lt;int&gt;("@TotalCount");
    /// </code>
    /// </para>
    /// </remarks>
    public DynamicParameters? Parameters { get; set; }



    /// <summary>
    /// When both <see cref="ServerOffset"/> and <see cref="ServerLimit"/> are
    /// set, the extractor appends <see cref="PagingClauseTemplate"/> to the
    /// command text before sending it. Default <see langword="null"/> disables
    /// server-side paging (the v0.4.0 behavior — the full result set comes
    /// back and <c>SkipItemCount</c>/<c>MaximumItemCount</c> filter
    /// client-side).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use server-side paging for very large tables where streaming
    /// everything to the client is wasteful. SQL Server requires an
    /// <c>ORDER BY</c> in the command text for paging to be deterministic;
    /// SQLite, PostgreSQL, and MySQL don't require it but you should still
    /// include one — without a stable order, page contents drift.
    /// </para>
    /// </remarks>
    public long? ServerOffset { get; set; }



    /// <summary>Page size in rows. See <see cref="ServerOffset"/>.</summary>
    public long? ServerLimit { get; set; }



    /// <summary>
    /// SQL fragment appended to the command text when both
    /// <see cref="ServerOffset"/> and <see cref="ServerLimit"/> are set.
    /// Bound as Dapper parameters <c>@PageOffset</c> and <c>@PageLimit</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Defaults to <c>LIMIT @PageLimit OFFSET @PageOffset</c> — the SQLite /
    /// PostgreSQL / MySQL syntax.
    /// </para>
    /// <para>
    /// For SQL Server, set to <c>OFFSET @PageOffset ROWS FETCH NEXT @PageLimit ROWS ONLY</c>
    /// (and ensure the base SQL ends with an <c>ORDER BY</c>).
    /// </para>
    /// </remarks>
    public string PagingClauseTemplate { get; set; } = "LIMIT @PageLimit OFFSET @PageOffset";



    /// <summary>
    /// When non-null, this function is invoked before extraction begins to determine
    /// the total record count, which is then reported via <see cref="DbReport.TotalItemCount"/>.
    /// Assign <see cref="DefaultTotalCountQuery"/> to use the library's built-in
    /// <c>SELECT COUNT(*)</c> subquery, or supply a custom function for a more efficient
    /// query. Defaults to <c>null</c> (total count is not fetched).
    /// </summary>
    public Func<CancellationToken, Task<int>>? TotalCountQuery { get; set; }



    /// <summary>
    /// The default total count implementation. Wraps <see cref="CommandText"/> in
    /// <c>SELECT COUNT(*) FROM (...) AS _count</c> and executes it using the same
    /// connection, parameters, and transaction as the extraction query.
    /// Assign this to <see cref="TotalCountQuery"/> to enable the built-in behavior.
    /// </summary>
    /// <remarks>
    /// Trailing semicolons are stripped automatically. If the command text contains
    /// an <c>ORDER BY</c> clause, some database providers (e.g. SQL Server) may reject
    /// it inside a derived table. Use a custom <see cref="TotalCountQuery"/> in that case.
    /// </remarks>
    public Func<CancellationToken, Task<int>> DefaultTotalCountQuery => ExecuteDefaultTotalCountQueryAsync;



    /// <summary>
    /// Runs the configured <see cref="TotalCountQuery"/> (or the built-in
    /// <see cref="DefaultTotalCountQuery"/> if none is assigned) and returns
    /// the result. Useful when the caller wants the total count without
    /// actually streaming the rows — for example, sizing a progress bar
    /// before kicking off the extract.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the count query.</param>
    /// <returns>The row count reported by the underlying query.</returns>
    /// <remarks>
    /// <para>
    /// Doesn't mutate any state on the extractor — does not touch
    /// <see cref="DbReport.TotalItemCount"/>, the stopwatch, or any of the
    /// progress counters. Safe to call any number of times before, during
    /// (different cancellation token), or after an <c>ExtractAsync</c>.
    /// </para>
    /// <para>
    /// Opens the connection on the owned-connection ctor path before running
    /// the query and disposes it after — same lifecycle as a full extraction.
    /// </para>
    /// </remarks>
    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        var query = TotalCountQuery ?? DefaultTotalCountQuery;
        var needsOpen = (_ownsConnection || ManageConnection) && _connection.State != ConnectionState.Open;

        if (!needsOpen)
        {
            return await query(cancellationToken).ConfigureAwait(false);
        }

        await _connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return await query(cancellationToken).ConfigureAwait(false);
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
            else
            {
#if NET5_0_OR_GREATER
                await _connection.CloseAsync().ConfigureAwait(false);
#else
                _connection.Close();
                await Task.CompletedTask.ConfigureAwait(false);
#endif
            }
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
            _stopwatch.ElapsedMilliseconds,
            _totalItemCount
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
            if (Interlocked.CompareExchange(ref _progressTimerWired, 1, 0) == 0)
            {
                _progressTimer.Elapsed += () => progress.Report(CreateProgressReport());
            }

            return _progressTimer;
        }

        return base.CreateProgressTimer(progress);
    }



    /// <inheritdoc/>
#pragma warning disable MA0051
    protected override async IAsyncEnumerable<TRecord> ExtractWorkerAsync([EnumeratorCancellation] CancellationToken token)
#pragma warning restore MA0051
    {
        _stopwatch.Restart();
        _totalItemCount = null;
        LogExtractionStarted();

        // Owned-connection ctor path: open before the first query, dispose after.
        // ManageConnection=true path: open before the first query, CLOSE (don't
        // dispose) after — connection returns to the pool, caller keeps it.
        // try/finally in an async iterator is OK in C# 8+; the iterator runtime
        // routes break/exception through the finally on Dispose.
        var openedHere = false;
        if ((_ownsConnection || ManageConnection) && _connection.State != ConnectionState.Open)
        {
            await _connection.OpenAsync(token).ConfigureAwait(false);
            openedHere = true;
        }

        try
        {
            var (commandText, param) = ApplyServerPaging(_commandText, Parameters ?? _dynamicParameters);

            if (TotalCountQuery != null)
            {
                _totalItemCount = await TotalCountQuery(token).ConfigureAwait(false);
            }

            long rowIndex = 0;

            await foreach (var record in _connection.QueryUnbufferedAsync<TRecord>(commandText, param, _transaction, CommandTimeoutSeconds, CommandType).ConfigureAwait(false))
            {
                token.ThrowIfCancellationRequested();
                rowIndex++;

                if (rowIndex <= SkipItemCount)
                {
                    IncrementCurrentSkippedItemCount();
                    LogDebugRowSkipped(rowIndex);
                    continue;
                }

                if (CurrentItemCount >= MaximumItemCount)
                {
                    LogDebugMaxReached();
                    LogExtractionCompleted();
                    yield break;
                }

                LogDebugRowExtracted(rowIndex);
                IncrementCurrentItemCount();
                yield return record;
            }

            LogExtractionCompleted();
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
            else if (openedHere)
            {
                // ManageConnection path — close (do NOT dispose; caller owns it).
#if NET5_0_OR_GREATER
                await _connection.CloseAsync().ConfigureAwait(false);
#else
                _connection.Close();
                await Task.CompletedTask.ConfigureAwait(false);
#endif
            }
        }
    }



    // ------------------------------------------------------------------
    // Private helpers
    // ------------------------------------------------------------------

    private Task<int> ExecuteDefaultTotalCountQueryAsync(CancellationToken token)
    {
        var sanitized = SanitizeCommandTextForCount(_commandText);
        var countSql = $"SELECT COUNT(*) FROM ({sanitized}) AS _count";
        var param = Parameters ?? _dynamicParameters;
        return _connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, param, _transaction, CommandTimeoutSeconds, cancellationToken: token));
    }



    /// <summary>
    /// If <see cref="ServerOffset"/> and <see cref="ServerLimit"/> are both
    /// set, append <see cref="PagingClauseTemplate"/> to <paramref name="commandText"/>
    /// and add <c>@PageOffset</c> / <c>@PageLimit</c> to the parameter set.
    /// Otherwise returns the inputs unchanged.
    /// </summary>
    private (string CommandText, DynamicParameters? Param) ApplyServerPaging(string commandText, DynamicParameters? param)
    {
        if (!ServerOffset.HasValue || !ServerLimit.HasValue)
        {
            return (commandText, param);
        }

        var pagingParam = param ?? new DynamicParameters();
        pagingParam.Add("@PageOffset", ServerOffset.Value);
        pagingParam.Add("@PageLimit", ServerLimit.Value);

        return (commandText + " " + PagingClauseTemplate, pagingParam);
    }



    /// <summary>
    /// Strips trailing semicolons from the command text so it can be safely
    /// wrapped in a <c>SELECT COUNT(*) FROM (...)</c> subquery.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The command text is empty or whitespace-only.
    /// </exception>
    private static string SanitizeCommandTextForCount(string commandText)
    {
        if (string.IsNullOrWhiteSpace(commandText))
        {
            throw new InvalidOperationException
            (
                "The default total count query requires a non-empty command text. " +
                "Provide a custom TotalCountQuery when the extractor command text cannot be wrapped safely."
            );
        }

        // Strip any trailing run of semicolons and *all* whitespace — including
        // non-breaking space and other Unicode whitespace that a hard-coded char
        // list would miss. Loop on TrimEnd() / TrimEnd(';') until both passes
        // become no-ops, so interleaved cases like "... FROM People; ; ;" (or
        // "; ;") fully collapse.
        var result = commandText;
        while (true)
        {
            var trimmed = result.TrimEnd().TrimEnd(';');
            if (trimmed.Length == result.Length)
            {
                return trimmed.TrimEnd();
            }

            result = trimmed;
        }
    }



    // ------------------------------------------------------------------
    // Logging helpers
    // ------------------------------------------------------------------

    private void LogExtractionStarted()
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation
            (
                "Extraction started for {RecordType}. CommandText={CommandText}, " +
                "SkipItemCount={SkipItemCount}, MaximumItemCount={MaximumItemCount}",
                typeof(TRecord).Name,
                _commandText,
                SkipItemCount,
                MaximumItemCount
            );
        }

        if (_logger.IsEnabled(LogLevel.Debug) && _parameters != null)
        {
            foreach (var kvp in _parameters)
            {
                _logger.LogDebug
                (
                    "Parameter @{Name} = {Value}",
                    kvp.Key,
                    kvp.Value
                );
            }
        }
    }



    private void LogExtractionCompleted()
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation
            (
                "Extraction completed for {RecordType}: {ItemCount} items extracted, " +
                "{SkippedCount} skipped in {ElapsedMs}ms",
                typeof(TRecord).Name,
                CurrentItemCount,
                CurrentSkippedItemCount,
                _stopwatch.ElapsedMilliseconds
            );
        }
    }



    private void LogDebugRowSkipped(long rowIndex)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug
            (
                "Skipping row {RowIndex} ({SkippedCount}/{SkipItemCount})",
                rowIndex,
                CurrentSkippedItemCount,
                SkipItemCount
            );
        }
    }



    private void LogDebugMaxReached()
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug
            (
                "MaximumItemCount ({MaximumItemCount}) reached, stopping extraction",
                MaximumItemCount
            );
        }
    }



    private void LogDebugRowExtracted(long rowIndex)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug
            (
                "Extracted row {RowIndex} (item #{ItemCount})",
                rowIndex,
                CurrentItemCount + 1
            );
        }
    }
}
