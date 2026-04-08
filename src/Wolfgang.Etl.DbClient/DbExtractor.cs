using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
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

    private readonly DbConnection _connection;
    private readonly string _commandText;
    private readonly IDictionary<string, object>? _parameters;
    private readonly DbTransaction? _transaction;
    private readonly ILogger _logger;
    private readonly IProgressTimer? _progressTimer;
    private readonly Stopwatch _stopwatch = new();
    private bool _progressTimerWired;



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
    /// <param name="parameters">Named parameters for the query.</param>
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
        _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
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



#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER || NET5_0_OR_GREATER
    /// <inheritdoc/>
#pragma warning disable MA0051
    protected override async IAsyncEnumerable<TRecord> ExtractWorkerAsync([EnumeratorCancellation] CancellationToken token)
#else
    /// <inheritdoc/>
#pragma warning disable MA0051
    protected override async IAsyncEnumerable<TRecord> ExtractWorkerAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
#endif
#pragma warning restore MA0051
    {
        _stopwatch.Restart();
        LogExtractionStarted();

        var param = _parameters != null ? new DynamicParameters(_parameters) : null;
        long rowIndex = 0;

        await foreach (var record in _connection.QueryUnbufferedAsync<TRecord>(_commandText, param, _transaction))
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
