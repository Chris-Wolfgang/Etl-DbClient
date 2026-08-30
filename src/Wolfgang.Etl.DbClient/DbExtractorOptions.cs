using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Dapper;

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
    /// Gets the Dapper parameters supplied to the command. Defaults to <see langword="null"/>.
    /// </summary>
    public DynamicParameters? Parameters { get; init; }



    /// <summary>
    /// Gets the server-side row offset for paging. Defaults to <see langword="null"/> (no paging).
    /// </summary>
    public long? ServerOffset { get; init; }



    /// <summary>
    /// Gets the server-side row limit for paging. Defaults to <see langword="null"/> (no paging).
    /// </summary>
    public long? ServerLimit { get; init; }



    /// <summary>
    /// Gets the dialect-specific paging clause appended when server paging is active.
    /// Defaults to <c>"LIMIT @PageLimit OFFSET @PageOffset"</c>.
    /// </summary>
    public string PagingClauseTemplate { get; init; } = "LIMIT @PageLimit OFFSET @PageOffset";



    /// <summary>
    /// Gets a callback returning the total row count, used to populate progress reports.
    /// Defaults to <see langword="null"/>, meaning the total is not reported.
    /// </summary>
    public Func<CancellationToken, Task<int>>? TotalCountQuery { get; init; }
}
