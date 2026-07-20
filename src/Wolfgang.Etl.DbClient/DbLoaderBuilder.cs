using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Wolfgang.Etl.Abstractions;

namespace Wolfgang.Etl.DbClient;

/// <summary>
/// Default <see cref="IDbLoaderBuilder{T}"/> implementation. Configuration
/// setters mutate the wrapped <see cref="DbLoader{TRecord}"/>;
/// <see cref="IEtlPipelineSink.RunAsync"/> executes the pipeline via that
/// loader.
/// </summary>
/// <remarks>
/// Since the sink is materialized as <c>pipeline.To(loader)</c> when this
/// builder is constructed, further setter calls still take effect: the
/// underlying <see cref="EtlPipelineSink{T, TProgress}"/> reads the loader's
/// properties at <see cref="IEtlPipelineSink.RunAsync"/> time, not at
/// construction.
/// </remarks>
internal sealed class DbLoaderBuilder<T> : IDbLoaderBuilder<T>
    where T : notnull
{
    private readonly IEtlPipelineSink _sink;
    private readonly DbLoader<T> _loader;



    internal DbLoaderBuilder(IEtlPipelineSink sink, DbLoader<T> loader)
    {
        _sink = sink;
        _loader = loader;
    }



    // -----------------------------------------------------------------
    // Fluent setters
    // -----------------------------------------------------------------

    public IDbLoaderBuilder<T> CommandType(CommandType commandType)
    {
        _loader.CommandType = commandType;
        return this;
    }



    public IDbLoaderBuilder<T> ManageConnection(bool manage)
    {
        _loader.ManageConnection = manage;
        return this;
    }



    public IDbLoaderBuilder<T> ErrorHandling(RowErrorHandling handling)
    {
        _loader.ErrorHandling = handling;
        return this;
    }



    // -----------------------------------------------------------------
    // IEtlPipelineSink
    // -----------------------------------------------------------------

    public Task RunAsync(IProgress<EtlPipelineProgress>? progress = null, CancellationToken token = default)
    {
        return _sink.RunAsync(progress, token);
    }
}
