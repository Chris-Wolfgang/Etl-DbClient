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
    /// Sets the rows to skip. An alias of <see cref="SkipItemCount"/>: it writes the same value.
    /// </summary>
    /// <remarks>
    /// Kept for source compatibility with 0.12.0. <see langword="null"/> means <c>0</c>. With a
    /// <see cref="PagingClauseTemplate"/> set, the skip is pushed into the query's offset.
    /// Because both methods write one value, whichever is called last wins.
    /// </remarks>
    IDbExtractorBuilder<T> ServerOffset(long? offset);


    /// <summary>
    /// Sets <see cref="DbExtractor{TRecord}.PageSize"/> — rows per round-trip.
    /// </summary>
    /// <param name="pageSize">Rows per round-trip, or <see langword="null"/> for a single query.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <remarks>
    /// Transport tuning, not a row filter. Requires
    /// <see cref="PagingClauseTemplate"/>; use <see cref="MaximumItemCount"/> to cap the total
    /// number of rows returned.
    /// </remarks>
    IDbExtractorBuilder<T> PageSize(int? pageSize);


    /// <summary>
    /// Sets the number of rows to pass over before the first yielded row.
    /// </summary>
    /// <param name="skip">Rows to skip.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <remarks>
    /// Pushed into the query's offset when <see cref="PagingClauseTemplate"/> is set, so the
    /// skipped rows are never fetched; applied client-side otherwise.
    /// </remarks>
    IDbExtractorBuilder<T> SkipItemCount(int skip);


    /// <summary>
    /// Sets the maximum number of rows to yield.
    /// </summary>
    /// <param name="maximum">The maximum number of rows.</param>
    /// <returns>The same builder, for chaining.</returns>
    IDbExtractorBuilder<T> MaximumItemCount(int maximum);


    /// <summary>
    /// Sets the total number of rows to return. An alias of <see cref="MaximumItemCount"/>: it
    /// writes the same value. For rows per round-trip use <see cref="PageSize"/>.
    /// </summary>
    /// <remarks>
    /// Kept for source compatibility with 0.12.0, where it meant the total row count.
    /// <see langword="null"/> restores the default of no limit. Because both methods write one
    /// value, whichever is called last wins.
    /// </remarks>
    IDbExtractorBuilder<T> ServerLimit(long? limit);


    /// <summary>
    /// Sets <see cref="DbExtractor{TRecord}.PagingClauseTemplate"/> — the SQL
    /// snippet appended when paging is active (a skip, a maximum or a <see cref="PageSize"/>). Defaults to
    /// <see cref="PagingClauseTemplates.None"/>.
    /// </summary>
    /// <param name="template">
    /// The paging clause, normally a preset from <see cref="PagingClauseTemplates"/>.
    /// <see langword="null"/> is accepted and means <see cref="PagingClauseTemplates.None"/> —
    /// no dialect chosen — which is the default.
    /// </param>
    /// <remarks>
    /// The clause is dialect-specific and there is no portable form, so this library does not
    /// guess one. Setting <see cref="PageSize"/> while no template has
    /// been chosen throws
    /// <see cref="System.InvalidOperationException"/> rather than emitting SQL that only some
    /// engines accept. Use a preset, e.g.
    /// <c>.PagingClauseTemplate(PagingClauseTemplates.SqlServer)</c>.
    /// <para>
    /// The <c>OFFSET … FETCH</c> form additionally requires an <c>ORDER BY</c> at the end of the
    /// command text <i>you supply</i> — the paging clause is appended after it, so the finished
    /// statement ends with the paging clause, not the <c>ORDER BY</c>. SQL Server and Oracle both
    /// reject the form otherwise.
    /// </para>
    /// </remarks>
    IDbExtractorBuilder<T> PagingClauseTemplate(string? template);


    /// <summary>
    /// Sets <see cref="DbExtractor{TRecord}.TotalCountQuery"/> — an optional
    /// async delegate the extractor calls once at the start of enumeration to
    /// snapshot the total row count for progress reporting.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="countQuery"/> is <see langword="null"/>.</exception>
    IDbExtractorBuilder<T> TotalCountQuery(Func<CancellationToken, Task<int>> countQuery);
}
