using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Wolfgang.Etl.DbClient;

namespace Wolfgang.Etl.DbClient.Benchmarks;

/// <summary>
/// Bulk-loads <see cref="BenchmarkRecord"/> rows into the configured RDBMS.
/// Selected by <c>ETL_DBCLIENT_BENCHMARK_RDBMS</c> — defaults to in-memory SQLite.
/// </summary>
[MemoryDiagnoser]
public class LoaderBenchmarks : IDisposable
{
    private DbConnection _connection = null!;
    private BenchmarkRecord[] _records = Array.Empty<BenchmarkRecord>();



    [Params(100, 1_000, 10_000)]
    public int RecordCount { get; set; }



    [GlobalSetup]
    public async Task SetupAsync()
    {
        _connection = BenchmarkContext.OpenConnection();
        await BenchmarkContext.ResetSchemaAsync(_connection).ConfigureAwait(false);

        _records = new BenchmarkRecord[RecordCount];
        for (var i = 0; i < RecordCount; i++)
        {
            _records[i] = new BenchmarkRecord
            {
                Name = $"Item{i + 1}",
                Value = (i + 1) * 10,
            };
        }
    }



    [IterationSetup]
    public void IterationSetup()
    {
        // Each Benchmark invocation should start from an empty table so timing
        // is comparable across iterations.
        BenchmarkContext.ResetSchemaAsync(_connection).GetAwaiter().GetResult();
    }



    [GlobalCleanup]
    public void Cleanup() => Dispose();



    public void Dispose()
    {
        _connection?.Dispose();
    }



    [Benchmark]
    public async Task LoadAsync()
    {
        var loader = new DbLoader<BenchmarkRecord>
        (
            _connection,
            "INSERT INTO contract_items (name, value) VALUES (@Name, @Value)"
        );

        await loader.LoadAsync(ToAsyncEnumerable(_records)).ConfigureAwait(false);
    }



    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            yield return item;
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }
}
