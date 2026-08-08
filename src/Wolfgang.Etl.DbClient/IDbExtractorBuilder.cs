using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Wolfgang.Etl.Abstractions;

namespace Wolfgang.Etl.DbClient;

/// <summary>
/// Fluent builder for a <see cref="DbExtractor{TRecord}"/> hung off the generic
/// <see cref="EtlPipeline"/> chain. Extends <see cref="IEtlPipeline{T}"/>, so a
/// consumer transitions from configuring the extractor to appending pipeline
/// operators simply by calling one — no explicit <c>Build()</c> step.
/// </summary>
/// <typeparam name="T">The record type produced by the extractor.</typeparam>
/// <remarks>
/// Each setter maps 1:1 to a public property on <see cref="DbExtractor{TRecord}"/>;
/// no new configuration surface is introduced. Setters return the builder for
/// chaining and take effect on the next enumeration of the pipeline.
/// </remarks>
public interface IDbExtractorBuilder<T> : IEtlPipeline<T>
    where T : notnull
{
    /// <summary>
    /// Sets <see cref="DbExtractor{TRecord}.CommandType"/>. Default: <see cref="System.Data.CommandType.Text"/>.
    /// </summary>
    IDbExtractorBuilder<T> CommandType(CommandType commandType);


    /// <summary>
    /// Sets <see cref="DbExtractor{TRecord}.ManageConnection"/>. When <see langword="true"/>,
    /// the extractor opens the connection before enumerating and closes it after
    /// the enumeration finishes.
    /// </summary>
    IDbExtractorBuilder<T> ManageConnection(bool manage);


    /// <summary>
    /// Sets <see cref="DbExtractor{TRecord}.Parameters"/> — Dapper-style parameter
    /// bag for the query. Overrides any parameters supplied via the constructor.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is <see langword="null"/>.</exception>
    IDbExtractorBuilder<T> Parameters(DynamicParameters parameters);


    /// <summary>
    /// Sets <see cref="DbExtractor{TRecord}.ServerOffset"/> for server-side paging.
    /// Combined with <see cref="ServerLimit"/> and
    /// <see cref="PagingClauseTemplate"/>, the extractor appends the paging clause
    /// to the caller's SQL.
    /// </summary>
    IDbExtractorBuilder<T> ServerOffset(long? offset);


    /// <summary>
    /// Sets <see cref="DbExtractor{TRecord}.ServerLimit"/> for server-side paging.
    /// </summary>
    IDbExtractorBuilder<T> ServerLimit(long? limit);


    /// <summary>
    /// Sets <see cref="DbExtractor{TRecord}.PagingClauseTemplate"/> — the SQL
    /// snippet appended when <see cref="ServerOffset"/> and/or
    /// <see cref="ServerLimit"/> are set. Default:
    /// <c>LIMIT @PageLimit OFFSET @PageOffset</c>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="template"/> is <see langword="null"/>.</exception>
    IDbExtractorBuilder<T> PagingClauseTemplate(string template);


    /// <summary>
    /// Sets <see cref="DbExtractor{TRecord}.TotalCountQuery"/> — an optional
    /// async delegate the extractor calls once at the start of enumeration to
    /// snapshot the total row count for progress reporting.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="countQuery"/> is <see langword="null"/>.</exception>
    IDbExtractorBuilder<T> TotalCountQuery(Func<CancellationToken, Task<int>> countQuery);
}
