using System;
using System.Data.Common;
using Wolfgang.Etl.Abstractions;

namespace Wolfgang.Etl.DbClient;

/// <summary>
/// Class-named DbClient factories + terminators for the fluent
/// <see cref="EtlPipeline"/> chain. Enables
/// <c>EtlPipeline.Create().DbExtractor&lt;Order&gt;(conn, sql).DbLoader&lt;Order&gt;(destConn, insertSql).RunAsync()</c>
/// alongside the sibling factories shipped by other format packages
/// (<c>CsvExtractor</c>, <c>JsonLineLoader</c>, etc.).
/// </summary>
/// <remarks>
/// <para>
/// Connection lifetime follows the existing
/// <see cref="DbExtractor{TRecord}.ManageConnection"/> and
/// <see cref="DbLoader{TRecord}.ManageConnection"/> semantics — the pipeline
/// does <b>not</b> override ownership. When <c>ManageConnection</c> is
/// <see langword="true"/> the extractor / loader opens and closes the
/// connection; otherwise the caller owns it. There is no
/// <c>DisposingOwned</c>-style resource handoff, because unlike file loaders
/// there is no file handle created by the factory itself.
/// </para>
/// <para>
/// Dry-run behaviour on the loader flows through the shared
/// <see cref="ISupportDryRun"/> pipeline hook rather than a DbClient-specific
/// setter, consistent with the other loaders.
/// </para>
/// </remarks>
public static class EtlPipelineDbClientExtensions
{
    // -----------------------------------------------------------------
    // Source factories
    // -----------------------------------------------------------------

    /// <summary>
    /// Starts a pipeline that reads rows from a database query.
    /// </summary>
    /// <typeparam name="T">The record type produced by the extractor.</typeparam>
    /// <param name="pipeline">The pipeline seed from <see cref="EtlPipeline.Create"/>.</param>
    /// <param name="connection">
    /// The <see cref="DbConnection"/> the extractor reads from. Caller retains
    /// ownership unless <see cref="IDbExtractorBuilder{T}.ManageConnection"/>
    /// is set to <see langword="true"/>.
    /// </param>
    /// <param name="commandText">The SQL query to execute.</param>
    /// <param name="transaction">
    /// An optional <see cref="DbTransaction"/> for isolation-level control.
    /// The extractor never commits or rolls back the transaction.
    /// </param>
    /// <returns>An <see cref="IDbExtractorBuilder{T}"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="pipeline"/>, <paramref name="connection"/>, or
    /// <paramref name="commandText"/> is <see langword="null"/>.
    /// </exception>
    public static IDbExtractorBuilder<T> DbExtractor<T>
    (
        this EtlPipeline pipeline,
        DbConnection connection,
        string commandText,
        DbTransaction? transaction = null
    )
        where T : notnull
    {
        if (pipeline is null)
        {
            throw new ArgumentNullException(nameof(pipeline));
        }

        if (connection is null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        if (commandText is null)
        {
            throw new ArgumentNullException(nameof(commandText));
        }

        var extractor = new DbExtractor<T>(connection, commandText, transaction);
        return new DbExtractorBuilder<T>(pipeline, extractor);
    }



    /// <summary>
    /// Starts a pipeline from an existing <see cref="Wolfgang.Etl.DbClient.DbExtractor{TRecord}"/>
    /// instance — for callers that already configured an extractor and want
    /// to reuse it verbatim.
    /// </summary>
    /// <typeparam name="T">The record type produced by the extractor.</typeparam>
    /// <param name="pipeline">The pipeline seed from <see cref="EtlPipeline.Create"/>.</param>
    /// <param name="extractor">The pre-configured extractor.</param>
    /// <returns>An <see cref="IDbExtractorBuilder{T}"/> for chaining. Setter
    /// calls on the builder mutate <paramref name="extractor"/> in place.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="pipeline"/> or <paramref name="extractor"/> is <see langword="null"/>.
    /// </exception>
    public static IDbExtractorBuilder<T> DbExtractor<T>
    (
        this EtlPipeline pipeline,
        DbExtractor<T> extractor
    )
        where T : notnull
    {
        if (pipeline is null)
        {
            throw new ArgumentNullException(nameof(pipeline));
        }

        if (extractor is null)
        {
            throw new ArgumentNullException(nameof(extractor));
        }

        return new DbExtractorBuilder<T>(pipeline, extractor);
    }



    // -----------------------------------------------------------------
    // Sink terminators
    // -----------------------------------------------------------------

    /// <summary>
    /// Terminates the pipeline, writing each record to a database via an
    /// <c>INSERT</c> / <c>UPDATE</c> command.
    /// </summary>
    /// <typeparam name="T">The record type consumed by the loader.</typeparam>
    /// <param name="pipeline">The pipeline to terminate.</param>
    /// <param name="connection">
    /// The <see cref="DbConnection"/> the loader writes to. Caller retains
    /// ownership unless <see cref="IDbLoaderBuilder{T}.ManageConnection"/>
    /// is set to <see langword="true"/>.
    /// </param>
    /// <param name="commandText">The SQL command to execute per record.</param>
    /// <param name="transaction">
    /// An optional <see cref="DbTransaction"/>. If <see langword="null"/> the
    /// loader creates and manages its own transaction (commit on success,
    /// rollback on failure).
    /// </param>
    /// <returns>An <see cref="IDbLoaderBuilder{T}"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="pipeline"/>, <paramref name="connection"/>, or
    /// <paramref name="commandText"/> is <see langword="null"/>.
    /// </exception>
    public static IDbLoaderBuilder<T> DbLoader<T>
    (
        this IEtlPipeline<T> pipeline,
        DbConnection connection,
        string commandText,
        DbTransaction? transaction = null
    )
        where T : notnull
    {
        if (pipeline is null)
        {
            throw new ArgumentNullException(nameof(pipeline));
        }

        if (connection is null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        if (commandText is null)
        {
            throw new ArgumentNullException(nameof(commandText));
        }

        var loader = new DbLoader<T>(connection, commandText, transaction);
        var sink = pipeline.To(loader);
        return new DbLoaderBuilder<T>(sink, loader);
    }



    /// <summary>
    /// Terminates the pipeline with a pre-configured
    /// <see cref="Wolfgang.Etl.DbClient.DbLoader{TRecord}"/> instance — for callers that already
    /// configured a loader (e.g. via a <c>WriteMode</c> ctor overload) and
    /// want to reuse it verbatim.
    /// </summary>
    /// <typeparam name="T">The record type consumed by the loader.</typeparam>
    /// <param name="pipeline">The pipeline to terminate.</param>
    /// <param name="loader">The pre-configured loader.</param>
    /// <returns>An <see cref="IDbLoaderBuilder{T}"/> for chaining. Setter
    /// calls on the builder mutate <paramref name="loader"/> in place.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="pipeline"/> or <paramref name="loader"/> is <see langword="null"/>.
    /// </exception>
    public static IDbLoaderBuilder<T> DbLoader<T>
    (
        this IEtlPipeline<T> pipeline,
        DbLoader<T> loader
    )
        where T : notnull
    {
        if (pipeline is null)
        {
            throw new ArgumentNullException(nameof(pipeline));
        }

        if (loader is null)
        {
            throw new ArgumentNullException(nameof(loader));
        }

        var sink = pipeline.To(loader);
        return new DbLoaderBuilder<T>(sink, loader);
    }
}
