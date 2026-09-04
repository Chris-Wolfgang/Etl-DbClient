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
    // Either the library's own dictionary handling (plain values only) or EtlParameterSet when
    // the caller supplied EtlParameter / DbParameter values. Typed as the interface so both fit.
    private readonly SqlMapper.IDynamicParameters? _dynamicParameters;
    private readonly DbTransaction? _transaction;
    private readonly ILogger _logger;
    private readonly Stopwatch _stopwatch = new();
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
    [Obsolete("Use the constructor that takes DbExtractorOptions. This constructor will be removed in a future release.")]
    public DbExtractor
    (
        DbConnection connection,
        string commandText,
        DbTransaction? transaction = null,
        ILogger<DbExtractor<TRecord>>? logger = null
    )
        : this
        (
            connection ?? throw new ArgumentNullException(nameof(connection)),
            commandText ?? throw new ArgumentNullException(nameof(commandText)),
            transaction,
            ownsConnection: false,
            logger
        )
    {
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
    [Obsolete("Use the constructor that takes DbExtractorOptions. This constructor will be removed in a future release.")]
    public DbExtractor
    (
        DbConnection connection,
        string commandText,
        IDictionary<string, object> parameters,
        DbTransaction? transaction = null,
        ILogger<DbExtractor<TRecord>>? logger = null
    )
        : this
        (
            connection ?? throw new ArgumentNullException(nameof(connection)),
            commandText ?? throw new ArgumentNullException(nameof(commandText)),
            transaction,
            ownsConnection: false,
            logger
        )
    {
        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        // Defensive copy — see the field-level comment on _parameters.
        _parameters = new Dictionary<string, object>(parameters, StringComparer.Ordinal);
        _dynamicParameters = EtlParameterSet.IsNeededFor(_parameters)
            ? new EtlParameterSet(_parameters)
            : new DynamicParameters(_parameters);
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
    [Obsolete("Use the constructor that takes DbExtractorOptions. This constructor will be removed in a future release.")]
    public DbExtractor
    (
        DbConnection connection,
        DbTransaction? transaction = null,
        ILogger<DbExtractor<TRecord>>? logger = null
    )
        : this
        (
            connection ?? throw new ArgumentNullException(nameof(connection)),
            DbCommandBuilder.BuildSelect<TRecord>(),
            transaction,
            ownsConnection: false,
            logger
        )
    {
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
    [Obsolete("Use the constructor that takes DbExtractorOptions. This constructor will be removed in a future release.")]
    public DbExtractor
    (
        DbProviderFactory factory,
        string connectionString,
        string commandText,
        ILogger<DbExtractor<TRecord>>? logger = null
    )
        : this
        (
            CreateOwnedConnection(factory, connectionString, commandText),
            commandText,
            transaction: null,
            ownsConnection: true,
            logger
        )
    {
    }




    /// <summary>
    /// Initializes a new instance of the <see cref="DbExtractor{TRecord}"/> class configured from an
    /// options record.
    /// </summary>
    /// <param name="connection">The connection to read from.</param>
    /// <param name="commandText">The command to execute.</param>
    /// <param name="transaction">An optional ambient transaction.</param>
    /// <param name="options">The configuration to apply. When <c>null</c>, the documented defaults apply.</param>
    /// <param name="logger">
    /// An optional logger instance for diagnostic output. When <c>null</c> — or omitted —
    /// <see cref="NullLogger.Instance"/> is used and logging is disabled.
    /// </param>
    public DbExtractor
    (
        DbConnection connection,
        string commandText,
        DbExtractorOptions? options,
        DbTransaction? transaction = null,
        ILogger<DbExtractor<TRecord>>? logger = null
    )
#pragma warning disable CS0618 // Chains into the deprecated ctor deliberately: it is the single initialization path.
        : this(connection, commandText, transaction, logger)
    {
        ApplyOptions(options);
    }
#pragma warning restore CS0618



    /// <summary>
    /// Initializes a new instance of the <see cref="DbExtractor{TRecord}"/> class configured from an
    /// options record.
    /// </summary>
    /// <param name="connection">The connection to read from.</param>
    /// <param name="commandText">The command to execute.</param>
    /// <param name="parameters">The command parameters.</param>
    /// <param name="transaction">An optional ambient transaction.</param>
    /// <param name="options">The configuration to apply. When <c>null</c>, the documented defaults apply.</param>
    /// <param name="logger">
    /// An optional logger instance for diagnostic output. When <c>null</c> — or omitted —
    /// <see cref="NullLogger.Instance"/> is used and logging is disabled.
    /// </param>
    public DbExtractor
    (
        DbConnection connection,
        string commandText,
        IDictionary<string, object> parameters,
        DbExtractorOptions? options,
        DbTransaction? transaction = null,
        ILogger<DbExtractor<TRecord>>? logger = null
    )
#pragma warning disable CS0618 // Chains into the deprecated ctor deliberately: it is the single initialization path.
        : this(connection, commandText, parameters, transaction, logger)
    {
        ApplyOptions(options);
    }
#pragma warning restore CS0618



    /// <summary>
    /// Initializes a new instance of the <see cref="DbExtractor{TRecord}"/> class configured from an
    /// options record.
    /// </summary>
    /// <param name="connection">The connection to read from.</param>
    /// <param name="transaction">An optional ambient transaction.</param>
    /// <param name="options">The configuration to apply. When <c>null</c>, the documented defaults apply.</param>
    /// <param name="logger">
    /// An optional logger instance for diagnostic output. When <c>null</c> — or omitted —
    /// <see cref="NullLogger.Instance"/> is used and logging is disabled.
    /// </param>
    public DbExtractor
    (
        DbConnection connection,
        DbExtractorOptions? options,
        DbTransaction? transaction = null,
        ILogger<DbExtractor<TRecord>>? logger = null
    )
#pragma warning disable CS0618 // Chains into the deprecated ctor deliberately: it is the single initialization path.
        : this(connection, transaction, logger)
    {
        ApplyOptions(options);
    }
#pragma warning restore CS0618



    /// <summary>
    /// Initializes a new instance of the <see cref="DbExtractor{TRecord}"/> class configured from an
    /// options record.
    /// </summary>
    /// <param name="factory">The provider factory used to create the connection.</param>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="commandText">The command to execute.</param>
    /// <param name="options">The configuration to apply. When <c>null</c>, the documented defaults apply.</param>
    /// <param name="logger">
    /// An optional logger instance for diagnostic output. When <c>null</c> — or omitted —
    /// <see cref="NullLogger.Instance"/> is used and logging is disabled.
    /// </param>
    public DbExtractor
    (
        DbProviderFactory factory,
        string connectionString,
        string commandText,
        DbExtractorOptions? options,
        ILogger<DbExtractor<TRecord>>? logger = null
    )
#pragma warning disable CS0618 // Chains into the deprecated ctor deliberately: it is the single initialization path.
        : this(factory, connectionString, commandText, logger)
    {
        ApplyOptions(options);
    }
#pragma warning restore CS0618

    /// <summary>
    /// Validates the provider-factory arguments and produces the connection this extractor owns.
    /// </summary>
    /// <remarks>
    /// The validation lives here rather than in the constructor body because a constructor's
    /// <c>this(...)</c> arguments are evaluated before its body runs. Keeping the checks in this
    /// order preserves both the original <c>ParamName</c> for each argument and the guarantee that
    /// no connection is created when any argument is null.
    /// </remarks>
    /// <param name="factory">The provider factory used to create the connection.</param>
    /// <param name="connectionString">The connection string applied to the new connection.</param>
    /// <param name="commandText">The command text, validated here to preserve argument order.</param>
    /// <returns>A new <see cref="DbConnection"/> owned by this extractor.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="factory"/>, <paramref name="connectionString"/> or
    /// <paramref name="commandText"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="factory"/> produced a <c>null</c> connection.
    /// </exception>
    private static DbConnection CreateOwnedConnection
    (
        DbProviderFactory factory,
        string connectionString,
        string commandText
    )
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        if (connectionString == null) throw new ArgumentNullException(nameof(connectionString));
        if (commandText == null) throw new ArgumentNullException(nameof(commandText));

        var conn = factory.CreateConnection()
            ?? throw new InvalidOperationException
            (
                $"{factory.GetType().FullName}.CreateConnection() returned null. " +
                "The provider factory does not produce DbConnection instances."
            );
        conn.ConnectionString = connectionString;
        return conn;
    }



    /// <summary>
    /// The single initialization path. Every other constructor chains into this one, so the shared
    /// fields are assigned in exactly one place and cannot drift between input shapes.
    /// </summary>
    private DbExtractor
    (
        DbConnection connection,
        string commandText,
        DbTransaction? transaction,
        bool ownsConnection,
        ILogger? logger
    )
    {
        _connection = connection;
        _commandText = commandText;
        _transaction = transaction;
        _ownsConnection = ownsConnection;
        _logger = logger ?? NullLogger.Instance;
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
        [Obsolete("Configure CommandTimeout through DbExtractorOptions passed to the constructor instead. This setter will be removed in a future release.")]
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
        : null;



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
    public CommandType CommandType { get; [Obsolete("Configure CommandType through DbExtractorOptions passed to the constructor instead. This setter will be removed in a future release.")] set; } = CommandType.Text;



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
    public bool ManageConnection { get; [Obsolete("Configure ManageConnection through DbExtractorOptions passed to the constructor instead. This setter will be removed in a future release.")] set; }



    /// <summary>
    /// When <see langword="true"/>, the extractor calls
    /// <see cref="DbSchemaValidator.ValidateAsync{TRecord}(System.Data.Common.DbConnection, System.Threading.CancellationToken)"/>
    /// before the first row is fetched. If the mapped
    /// <c>[Table]</c>/<c>[Column]</c> names don't match the database,
    /// the extractor throws <see cref="InvalidOperationException"/>
    /// before touching production data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Default: <see langword="false"/>. Opting in adds a single
    /// zero-row round-trip at the top of each <c>ExtractAsync</c>
    /// call — negligible compared to a real extract, but not free.
    /// Use it in a first-run smoke path or a health check, not
    /// inside every loop iteration on a per-message pipeline.
    /// </para>
    /// <para>
    /// Refs <see href="https://github.com/Chris-Wolfgang/Etl-DbClient/issues/20">#20</see>.
    /// </para>
    /// </remarks>
    public bool ValidateSchemaOnStart { get; [Obsolete("Configure ValidateSchemaOnStart through DbExtractorOptions passed to the constructor instead. This setter will be removed in a future release.")] set; }



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
    public DynamicParameters? Parameters { get; [Obsolete("Configure Parameters through DbExtractorOptions passed to the constructor instead. This setter will be removed in a future release.")] set; }



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
    /// <para>
    /// Defaults to <c>0</c>. Paging is switched on by <see cref="ServerLimit"/>; an offset with no limit throws, since no page size can be inferred.
    /// </para>
    /// </remarks>
    public long? ServerOffset { get; [Obsolete("Configure ServerOffset through DbExtractorOptions passed to the constructor instead. This setter will be removed in a future release.")] set; }



    /// <summary>Page size in rows. See <see cref="ServerOffset"/>.</summary>
    /// <remarks>Setting this switches server-side paging on. <see cref="ServerOffset"/> defaults to <c>0</c> when not set.</remarks>
    public long? ServerLimit { get; [Obsolete("Configure ServerLimit through DbExtractorOptions passed to the constructor instead. This setter will be removed in a future release.")] set; }



    /// <summary>
    /// SQL fragment appended to the command text when both
    /// <see cref="ServerOffset"/> and <see cref="ServerLimit"/> are set.
    /// Bound as Dapper parameters <c>@PageOffset</c> and <c>@PageLimit</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Defaults to <see cref="PagingClauseTemplates.None"/>: no dialect is assumed, because
    /// there is no portable paging syntax. Activating paging without choosing a template throws.
    /// </para>
    /// <para>
    /// For SQL Server, set to <c>OFFSET @PageOffset ROWS FETCH NEXT @PageLimit ROWS ONLY</c>
    /// (and ensure the base SQL ends with an <c>ORDER BY</c>).
    /// </para>
    /// <para>
    /// Prefer the presets on <see cref="PagingClauseTemplates"/> over writing the clause by hand.
    /// </para>
    /// </remarks>
    public string? PagingClauseTemplate { get; [Obsolete("Configure PagingClauseTemplate through DbExtractorOptions passed to the constructor instead. This setter will be removed in a future release.")] set; } = PagingClauseTemplates.None;



    /// <summary>
    /// When non-null, this function is invoked before extraction begins to determine
    /// the total record count, which is then reported via <see cref="Report.TotalItemCount"/>.
    /// Assign <see cref="DefaultTotalCountQuery"/> to use the library's built-in
    /// <c>SELECT COUNT(*)</c> subquery, or supply a custom function for a more efficient
    /// query. Defaults to <c>null</c> (total count is not fetched).
    /// </summary>
    public Func<CancellationToken, Task<int>>? TotalCountQuery { get; [Obsolete("Configure TotalCountQuery through DbExtractorOptions passed to the constructor instead. This setter will be removed in a future release.")] set; }



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
    /// <c>DbReport.TotalItemCount</c>, the stopwatch, or any of the
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
            if (ValidateSchemaOnStart)
            {
                await DbSchemaValidator.ValidateAsync<TRecord>(_connection, token).ConfigureAwait(false);
            }

            ApplyServerPaging(_commandText, Parameters ?? _dynamicParameters, out var commandText, out var param);

            if (TotalCountQuery != null)
            {
                _totalItemCount = await TotalCountQuery(token).ConfigureAwait(false);
            }

            long rowIndex = 0;

            // The reader is driven with a manual ReadAsync loop, and row mapping
            // goes through a standalone Dapper row-parser delegate, rather than
            // Dapper's own QueryUnbufferedAsync<T> IAsyncEnumerable. That matters:
            // QueryUnbufferedAsync's read-and-map loop lives inside ONE compiler-
            // generated async-iterator state machine. When mapping throws mid-loop,
            // the iterator's finally block runs and the state machine transitions
            // to "finished" — so even catching the exception at the call site,
            // the NEXT MoveNextAsync just returns false. Skip would silently
            // truncate the result set after the first bad row instead of
            // continuing past it. Reading via our own loop keeps ReadAsync's
            // cursor alive across a caught mapping failure, so Skip actually
            // skips-and-continues.
            var command = new CommandDefinition(commandText, param, _transaction, CommandTimeoutSeconds, CommandType, cancellationToken: token);
            using var reader = await _connection.ExecuteReaderAsync(command).ConfigureAwait(false);
            var parseRow = reader.GetRowParser<TRecord>();

            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                token.ThrowIfCancellationRequested();

                TRecord record;
                try
                {
                    record = parseRow(reader);
                }
                catch (System.Data.DataException ex)
                {
                    // Scoped to DataException — the type Dapper wraps row-materialization
                    // failures in (e.g. "Error parsing column N") — so ErrorPolicy only ever
                    // sees per-row failures. Catching every exception type here would also
                    // catch connection-level failures (a dropped connection, a syntax error
                    // surfacing lazily); those aren't per-row, and ReadAsync would likely keep
                    // throwing the same fault on every subsequent call, so routing them
                    // through ItemErrorAction.Skip would spin the loop instead of terminating.
                    rowIndex++;
                    var action = HandleItemError
                    (
                        new ItemErrorContext
                        (
                            rowIndex,
                            ex,
                            rawContent: null
                        )
                    );
                    if (action == ItemErrorAction.Abort)
                    {
                        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
                    }
                    // ItemErrorAction.Skip — HandleItemError already incremented
                    // the error-item counter on the base. Log and continue.
                    LogDebugRowErrorSkipped(rowIndex, ex);
                    continue;
                }

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
    /// Verifies a paging dialect was chosen before server-side paging is applied.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Paging is active but <see cref="PagingClauseTemplate"/> is
    /// <see cref="PagingClauseTemplates.None"/>.
    /// </exception>
    private void EnsurePagingClauseTemplateChosen()
    {
        if (!string.IsNullOrWhiteSpace(PagingClauseTemplate))
        {
            return;
        }

        throw new InvalidOperationException
        (
            "Server-side paging requires PagingClauseTemplate to be set, because paging syntax " +
            "is dialect-specific and no portable form exists. Choose a preset from " +
            "PagingClauseTemplates (for example PagingClauseTemplates.SqlServer, .PostgreSql, " +
            ".MySql, .Sqlite, .Oracle or .Db2), or supply your own clause referencing " +
            "@PageOffset and @PageLimit. To disable paging instead, clear ServerOffset and " +
            "ServerLimit."
        );
    }



    /// <summary>
    /// Rejects a caller-supplied parameter whose name server-side paging also generates.
    /// </summary>
    /// <remarks>
    /// This cannot live inside either branch of <see cref="ApplyServerPaging"/>.
    /// <c>EtlParameterSet</c> can detect a collision itself, but the <c>DynamicParameters</c>
    /// branch cannot — its <c>Add</c> silently overwrites, so the caller's value would disappear
    /// without a word.
    /// <para>
    /// Two routes can carry a caller's parameters and both are checked: the constructor
    /// dictionary, and the obsolete <see cref="Parameters"/> property — which takes
    /// <em>precedence</em> over the dictionary where the parameter set is resolved, so checking
    /// only the dictionary would miss it entirely. Dapper stores names without the leading
    /// <c>@</c>, hence both spellings are compared.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// A generated paging parameter name was already supplied by the caller.
    /// </exception>
    private void EnsurePagingParametersNotAlreadySupplied()
    {
        foreach (var generated in new[] { "@PageOffset", "@PageLimit" })
        {
            // ContainsKey resolves against _parameters' own comparer, which is deliberately
            // StringComparer.Ordinal, so it would miss "@pagelimit" and the leading-@ variants.
            // Scan explicitly with the collision-safe comparison instead.
            var suppliedByDictionary = false;
            if (_parameters is not null)
            {
                foreach (var key in _parameters.Keys)
                {
                    if (ParameterName.Matches(key, generated))
                    {
                        suppliedByDictionary = true;
                        break;
                    }
                }
            }

            var suppliedByProperty = false;
            var names = Parameters?.ParameterNames;
            if (names is not null)
            {
                foreach (var name in names)
                {
                    if (ParameterName.Matches(name, generated))
                    {
                        suppliedByProperty = true;
                        break;
                    }
                }
            }

            if (suppliedByDictionary || suppliedByProperty)
            {
                throw new InvalidOperationException
                (
                    $"Parameter '{generated}' was supplied by the caller and is also generated by " +
                    "server-side paging, so it cannot be applied twice. Either stop supplying " +
                    $"'{generated}' and let paging provide it, or clear ServerOffset and " +
                    "ServerLimit and page through the command text yourself."
                );
            }
        }
    }


    /// <summary>
    /// If <see cref="ServerOffset"/> and <see cref="ServerLimit"/> are both
    /// set, append <see cref="PagingClauseTemplate"/> to <paramref name="commandText"/>
    /// (returned via <paramref name="pagedCommandText"/>) and add
    /// <c>@PageOffset</c> / <c>@PageLimit</c> to the parameter set (returned
    /// via <paramref name="pagedParam"/>). Otherwise returns the inputs
    /// unchanged.
    /// </summary>
    /// <remarks>
    /// out parameters instead of a tuple — net462 doesn't ship
    /// <c>System.ValueTuple</c> in the base targeting pack and we avoid the
    /// extra package reference.
    /// </remarks>
    /// <param name="commandText">The command text to page.</param>
    /// <param name="param">The parameter set to add the paging parameters to, or <c>null</c>.</param>
    /// <param name="pagedCommandText">The command text with the paging clause appended.</param>
    /// <param name="pagedParam">The parameter set including the paging parameters.</param>
    /// <exception cref="InvalidOperationException">
    /// The caller's parameter dictionary already contains <c>@PageOffset</c> or <c>@PageLimit</c>,
    /// which server-side paging also generates. Emitting both would duplicate the name, and
    /// silently preferring one would discard either the caller's value or the paging.
    /// </exception>
    private void ApplyServerPaging(string commandText, SqlMapper.IDynamicParameters? param, out string pagedCommandText, out SqlMapper.IDynamicParameters? pagedParam)
    {
        if (!ServerLimit.HasValue)
        {
            // An offset with no limit is the mirror of the bug this default fixes: the caller
            // plainly wants paging, and no limit can be inferred (every template references
            // @PageLimit). Silently returning every row from the top would ignore what they asked
            // for, so say so instead.
            if (ServerOffset.HasValue)
            {
                throw new InvalidOperationException
                (
                    "ServerOffset was set without ServerLimit, so server-side paging cannot be " +
                    "applied — a page size is required and cannot be inferred. Set ServerLimit " +
                    "to the number of rows per page, or clear ServerOffset."
                );
            }

            pagedCommandText = commandText;
            pagedParam = param;
            return;
        }

        // ServerLimit alone is enough: an unspecified offset can only mean "start at the top".
        var serverOffset = ServerOffset ?? 0L;

        EnsurePagingClauseTemplateChosen();
        EnsurePagingParametersNotAlreadySupplied();

        // Both parameter shapes accept additions, by different methods.
        switch (param)
        {
            case EtlParameterSet set:
                set.Add("@PageOffset", serverOffset);
                set.Add("@PageLimit", ServerLimit.Value);
                pagedParam = set;
                break;

            default:
                var dynamic = param as DynamicParameters ?? new DynamicParameters();
                dynamic.Add("@PageOffset", serverOffset);
                dynamic.Add("@PageLimit", ServerLimit.Value);
                pagedParam = dynamic;
                break;
        }

        pagedCommandText = commandText + " " + PagingClauseTemplate;
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



    private void LogDebugRowErrorSkipped(long rowIndex, Exception exception)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug
            (
                exception,
                "Row {RowIndex} skipped by ErrorPolicy: {ExceptionMessage}",
                rowIndex,
                exception.Message
            );
        }
    }

    /// <summary>
    /// Copies <paramref name="options"/> onto this instance. A <c>null</c> options object leaves
    /// every property at its default.
    /// </summary>
    /// <param name="options">The configuration to apply, or <c>null</c>.</param>
    private void ApplyOptions(DbExtractorOptions? options)
    {
#pragma warning disable CS0618 // ApplyOptions is the supported replacement for these setters.
        if (options is null)
        {
            return;
        }

        CommandTimeout = options.CommandTimeout;
        CommandType = options.CommandType;
        ManageConnection = options.ManageConnection;
        ValidateSchemaOnStart = options.ValidateSchemaOnStart;
        ServerOffset = options.ServerOffset;
        ServerLimit = options.ServerLimit;
        PagingClauseTemplate = options.PagingClauseTemplate;
        TotalCountQuery = options.TotalCountQuery;
#pragma warning restore CS0618
    }
}
