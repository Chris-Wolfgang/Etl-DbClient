using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace Wolfgang.Etl.DbClient;

/// <summary>
/// Options for the <see cref="DbExtractor{TRecord}"/> constructors.
/// </summary>
/// <remarks>
/// Supplied ahead of the optional transaction and logger. When the whole options object is
/// <see langword="null"/>, or an individual property is left unset, the documented defaults below
/// apply — defaults live on the property initializers here rather than in constructor bodies, so no
/// constructor can accidentally diverge from them.
/// <para>
/// The record is not generic: none of these settings depends on the record type being extracted.
/// </para>
/// <para>
/// This record deliberately exposes no <c>Parameters</c> property. Dapper's
/// <c>DynamicParameters</c> is a third-party type, and keeping it off the public surface is what
/// allows the ORM to be swapped later without a breaking change for consumers. Supply parameters
/// through the constructor overload that takes an <c>IDictionary&lt;string, object&gt;</c>.
/// </para>
/// </remarks>
public sealed record DbExtractorOptions
{
    /// <summary>
    /// Gets the command timeout. Defaults to <see langword="null"/>, meaning the provider's default.
    /// </summary>
    public TimeSpan? CommandTimeout { get; init; }



    /// <summary>
    /// Gets how the command text is interpreted. Defaults to <see cref="System.Data.CommandType.Text"/>.
    /// </summary>
    public CommandType CommandType { get; init; } = CommandType.Text;



    /// <summary>
    /// Gets a value indicating whether the extractor opens and closes the connection itself.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public bool ManageConnection { get; init; }



    /// <summary>
    /// Gets a value indicating whether the result-set schema is validated before the first row.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public bool ValidateSchemaOnStart { get; init; }




    /// <summary>
    /// Gets the server-side row offset for paging. Defaults to <see langword="null"/> (no paging).
    /// </summary>
    /// <remarks>Paging is applied only when <b>both</b> this and <see cref="ServerLimit"/> are set; setting one alone is a silent no-op.</remarks>
    public long? ServerOffset { get; init; }



    /// <summary>
    /// Gets the server-side row limit for paging. Defaults to <see langword="null"/> (no paging).
    /// </summary>
    /// <remarks>Paging is applied only when <b>both</b> this and <see cref="ServerOffset"/> are set; setting one alone is a silent no-op.</remarks>
    public long? ServerLimit { get; init; }



    /// <summary>
    /// Gets the dialect-specific paging clause appended when server paging is active.
    /// Defaults to <see cref="PagingClauseTemplates.Sqlite"/>
    /// (<c>"LIMIT @PageLimit OFFSET @PageOffset"</c>).
    /// </summary>
    /// <remarks>
    /// <b>This is dialect-specific, not standard SQL.</b> The default is the PostgreSQL / MySQL /
    /// SQLite form; <b>SQL Server rejects <c>LIMIT</c></b> and needs the SQL:2008
    /// <c>OFFSET … ROWS FETCH NEXT … ROWS ONLY</c> form, as do Oracle 12c+ and Db2. Leaving the
    /// default in place against those engines produces a runtime syntax error.
    /// <para>
    /// Use <see cref="PagingClauseTemplates"/> rather than writing the clause by hand:
    /// <c>PagingClauseTemplate = PagingClauseTemplates.SqlServer</c>. Any string is accepted, so a
    /// dialect with no preset can be supplied directly.
    /// </para>
    /// <para>
    /// The <c>OFFSET … FETCH</c> form additionally requires an <c>ORDER BY</c> at the end of the
    /// command text <i>you supply</i> — the paging clause is appended after it, so the finished
    /// statement ends with the paging clause, not the <c>ORDER BY</c>. SQL Server and Oracle both
    /// reject the form otherwise.
    /// </para>
    /// <para>
    /// A custom template must reference both <c>@PageOffset</c> and <c>@PageLimit</c> — those are
    /// the parameter names supplied when <see cref="ServerOffset"/> and <see cref="ServerLimit"/>
    /// are set.
    /// </para>
    /// </remarks>
    public string PagingClauseTemplate { get; init; } = PagingClauseTemplates.Sqlite;



    /// <summary>
    /// Gets a callback returning the total row count, used to populate progress reports.
    /// Defaults to <see langword="null"/>, meaning the total is not reported.
    /// </summary>
    public Func<CancellationToken, Task<int>>? TotalCountQuery { get; init; }
}
