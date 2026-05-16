using System;
using System.Data.Common;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Wolfgang.Etl.DbClient;

namespace Wolfgang.Etl.DbClient.Benchmarks;

/// <summary>
/// Streams <see cref="BenchmarkRecord"/> rows out of the configured RDBMS.
/// Selected by <c>ETL_DBCLIENT_BENCHMARK_RDBMS</c> — defaults to in-memory SQLite.
/// </summary>
[MemoryDiagnoser]
public class ExtractorBenchmarks : IDisposable
{
    private DbConnection _connection = null!;



    [Params(100, 1_000, 10_000)]
    public int RecordCount { get; set; }



    [GlobalSetup]
    public async Task SetupAsync()
    {
        _connection = BenchmarkContext.OpenConnection();
        await BenchmarkContext.ResetSchemaAsync(_connection).ConfigureAwait(false);
        await BenchmarkContext.SeedAsync(_connection, RecordCount).ConfigureAwait(false);
    }



    [GlobalCleanup]
    public void Cleanup() => Dispose();



    public void Dispose()
    {
        _connection?.Dispose();
    }



    [Benchmark]
    public async Task<int> ExtractAsync()
    {
        var extractor = new DbExtractor<BenchmarkRecord>
        (
            _connection,
            "SELECT name AS Name, value AS Value FROM contract_items"
        );

        var count = 0;
        await foreach (var _ in extractor.ExtractAsync().ConfigureAwait(false))
        {
            count++;
        }

        return count;
    }
}
