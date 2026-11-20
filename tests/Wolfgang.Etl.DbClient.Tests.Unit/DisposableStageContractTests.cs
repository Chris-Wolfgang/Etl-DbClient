using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wolfgang.Etl.TestKit.Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

/// <summary>
/// Adopts <see cref="DisposableStageContractTests{TSut}"/> for <see cref="DbExtractor{TRecord}"/>:
/// using the extractor after <c>Dispose</c>/<c>DisposeAsync</c> throws
/// <see cref="ObjectDisposedException"/>, and disposing twice is a no-op.
/// </summary>
public sealed class DbExtractorDisposableStageContractTests
    : DisposableStageContractTests<DbExtractor<ContractRecord>>
{
    /// <inheritdoc/>
    protected override DbExtractor<ContractRecord> CreateSut()
        => new DbExtractor<ContractRecord>
        (
            TestDb.CreateContractConnection(3),
            "SELECT Name, Value FROM ContractItems ORDER BY Value",
            new DbExtractorOptions()
        );



    /// <inheritdoc/>
    protected override async Task<bool> InvokeReportsObjectDisposedAsync(bool disposeFirst, bool useAsyncDispose)
    {
        var sut = CreateSut();
        if (disposeFirst)
        {
            await DisposeAsync(sut, useAsyncDispose);
        }

        try
        {
            await foreach (var _ in sut.ExtractAsync())
            {
                // Drain the sequence: the guard runs when enumeration starts.
            }

            return false;
        }
        catch (ObjectDisposedException)
        {
            return true;
        }
    }



    private static async Task DisposeAsync(DbExtractor<ContractRecord> sut, bool useAsyncDispose)
    {
        if (useAsyncDispose)
        {
            await sut.DisposeAsync();
        }
        else
        {
            sut.Dispose();
        }
    }
}



/// <summary>
/// Adopts <see cref="DisposableStageContractTests{TSut}"/> for <see cref="DbLoader{TRecord}"/>.
/// </summary>
public sealed class DbLoaderDisposableStageContractTests
    : DisposableStageContractTests<DbLoader<ContractRecord>>
{
    /// <inheritdoc/>
    protected override DbLoader<ContractRecord> CreateSut()
        => new DbLoader<ContractRecord>
        (
            TestDb.CreateContractLoaderConnection(),
            "INSERT INTO ContractItems (Name, Value) VALUES (@Name, @Value)",
            new DbLoaderOptions()
        );



    /// <inheritdoc/>
    protected override async Task<bool> InvokeReportsObjectDisposedAsync(bool disposeFirst, bool useAsyncDispose)
    {
        var sut = CreateSut();
        if (disposeFirst)
        {
            if (useAsyncDispose)
            {
                await sut.DisposeAsync();
            }
            else
            {
                sut.Dispose();
            }
        }

        try
        {
            await sut.LoadAsync(OneRowAsync());
            return false;
        }
        catch (ObjectDisposedException)
        {
            return true;
        }
    }



    private static async IAsyncEnumerable<ContractRecord> OneRowAsync()
    {
        yield return new ContractRecord { Name = "Item1", Value = 10 };
        await Task.CompletedTask;
    }
}
