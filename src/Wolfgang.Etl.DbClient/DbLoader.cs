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
/// </remarks>
public class DbLoader<TRecord> : LoaderBase<TRecord, DbReport>
    where TRecord : notnull
{
    // ------------------------------------------------------------------
    // Fields
    // ------------------------------------------------------------------

    private readonly DbConnection _connection;
    private readonly string _commandText;
    private readonly DbTransaction? _callerTransaction;
    private readonly bool _ownsTransaction;
    private readonly ILogger _logger;
    private readonly IProgressTimer? _progressTimer;
    private readonly Stopwatch _stopwatch = new();
    private bool _progressTimerWired;



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
            if (!_progressTimerWired)
            {
                _progressTimerWired = true;
                _progressTimer.Elapsed += () => progress.Report(CreateProgressReport());
            }

            return _progressTimer;
        }

        return base.CreateProgressTimer(progress);
    }



    /// <inheritdoc/>
    protected override async Task LoadWorkerAsync
    (
        IAsyncEnumerable<TRecord> items,
        CancellationToken token
    )
    {
        _stopwatch.Restart();
        LogLoadingStarted();

        var ownsTransaction = _ownsTransaction && _callerTransaction == null;
        var transaction = ownsTransaction
            ? await BeginAutoTransactionAsync(token).ConfigureAwait(false)
            : _callerTransaction;

        try
        {
            await ExecuteItemsAsync(items, transaction, token).ConfigureAwait(false);

            if (ownsTransaction && transaction != null)
            {
                await CommitAutoTransactionAsync(transaction, token).ConfigureAwait(false);
            }
        }
        catch
        {
            if (ownsTransaction && transaction != null)
            {
                await RollbackAutoTransactionAsync(transaction, token).ConfigureAwait(false);
            }

            throw;
        }
        finally
        {
            if (ownsTransaction && transaction != null)
            {
                await DisposeTransactionAsync(transaction).ConfigureAwait(false);
            }
        }

        LogLoadingCompleted();
    }



    private async Task ExecuteItemsAsync
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

            await _connection.ExecuteAsync
            (
                new CommandDefinition
                (
                    _commandText,
                    item,
                    transaction,
                    cancellationToken: token
                )
            ).ConfigureAwait(false);

            IncrementCurrentItemCount();
            LogDebugRecordLoaded();
        }
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
        _ = token; // Used on net8.0+
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
        _ = token; // Used on net8.0+
        transaction.Commit();
        await System.Threading.Tasks.Task.CompletedTask.ConfigureAwait(false);
#endif
        LogDebugTransactionCommitted();
    }



    private async Task RollbackAutoTransactionAsync(DbTransaction transaction, CancellationToken token)
    {
        try
        {
#if NET5_0_OR_GREATER
            await transaction.RollbackAsync(token).ConfigureAwait(false);
#else
            _ = token; // Used on net8.0+
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
                "Failed to rollback transaction after error"
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
