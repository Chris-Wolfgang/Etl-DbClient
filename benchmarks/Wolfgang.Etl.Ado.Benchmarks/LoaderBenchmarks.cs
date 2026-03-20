using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.Data.Sqlite;
using Wolfgang.Etl.Abstractions;
using Wolfgang.Etl.Ado;

namespace Wolfgang.Etl.Ado.Benchmarks;

[MemoryDiagnoser]
public class LoaderBenchmarks
{
    private BenchmarkRecord[] _records = Array.Empty<BenchmarkRecord>();



    [Params(100, 1_000, 10_000)]
    public int RecordCount { get; set; }



    [GlobalSetup]
    public void Setup()
    {
        _records = new BenchmarkRecord[RecordCount];
        for (var i = 0; i < RecordCount; i++)
        {
            _records[i] = new BenchmarkRecord
            {
                FirstName = $"First{i}",
                LastName = $"Last{i}",
                Age = 20 + (i % 60),
            };
        }
    }



    [Benchmark]
    public async Task Load()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE People (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                first_name TEXT NOT NULL,
                last_name TEXT NOT NULL,
                age INTEGER NOT NULL
            )";
        await cmd.ExecuteNonQueryAsync();

        var loader = new AdoLoader<BenchmarkRecord, Report>
        (
            connection,
            "INSERT INTO People (first_name, last_name, age) VALUES (@FirstName, @LastName, @Age)"
        );

        await loader.LoadAsync(ToAsyncEnumerable(_records));
    }



    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }
}
