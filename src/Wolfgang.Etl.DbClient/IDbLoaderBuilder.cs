using System.Data;
using Wolfgang.Etl.Abstractions;

namespace Wolfgang.Etl.DbClient;

/// <summary>
/// Fluent builder for a <see cref="DbLoader{TRecord}"/> that terminates a
/// generic <see cref="EtlPipeline"/> chain. Extends <see cref="IEtlPipelineSink"/>,
/// so a consumer transitions from configuring the loader to running the
/// pipeline simply by calling
/// <see cref="IEtlPipelineSink.RunAsync(System.IProgress{EtlPipelineProgress}, System.Threading.CancellationToken)"/>
/// — no explicit <c>Build()</c> step.
/// </summary>
/// <typeparam name="T">The record type consumed by the loader.</typeparam>
/// <remarks>
/// Each setter maps 1:1 to a public property on <see cref="DbLoader{TRecord}"/>;
/// no new configuration surface is introduced. Setters return the builder for
/// chaining and take effect on <see cref="IEtlPipelineSink.RunAsync"/>.
/// </remarks>
public interface IDbLoaderBuilder<T> : IEtlPipelineSink
    where T : notnull
{
    /// <summary>
    /// Sets <see cref="DbLoader{TRecord}.CommandType"/>. Default: <see cref="System.Data.CommandType.Text"/>.
    /// </summary>
    IDbLoaderBuilder<T> CommandType(CommandType commandType);


    /// <summary>
    /// Sets <see cref="DbLoader{TRecord}.ManageConnection"/>. When <see langword="true"/>,
    /// the loader opens the connection before writing and closes it after the
    /// run completes.
    /// </summary>
    IDbLoaderBuilder<T> ManageConnection(bool manage);


    /// <summary>
    /// Sets <see cref="DbLoader{TRecord}.ErrorHandling"/>. Controls per-row
    /// error behaviour: <see cref="RowErrorHandling.Abort"/> stops the run at
    /// the first failure (default); <see cref="RowErrorHandling.Skip"/>
    /// silently drops the failing row and continues (per-record path only —
    /// see the enum's remarks).
    /// </summary>
    IDbLoaderBuilder<T> ErrorHandling(RowErrorHandling handling);
}
