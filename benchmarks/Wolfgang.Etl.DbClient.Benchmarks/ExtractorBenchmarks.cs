using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.Data.Sqlite;
using Wolfgang.Etl.Abstractions;
using Wolfgang.Etl.DbClient;

namespace Wolfgang.Etl.DbClient.Benchmarks;

[MemoryDiagnoser]
public class ExtractorBenchmarks
{
    private SqliteConnection _connection = null!;



    [Params(100, 1_000, 10_000)]
    public int RecordCount { get; set; }



    [GlobalSetup]
    public async Task Setup()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE People (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                first_name TEXT NOT NULL,
                last_name TEXT NOT NULL,
                age INTEGER NOT NULL
            )";
        await cmd.ExecuteNonQueryAsync();

        for (var i = 0; i < RecordCount; i++)
        {
            using var insert = _connection.CreateCommand();
            insert.CommandText = "INSERT INTO People (first_name, last_name, age) VALUES (@fn, @ln, @age)";
            insert.Parameters.AddWithValue("@fn", $"First{i}");
            insert.Parameters.AddWithValue("@ln", $"Last{i}");
            insert.Parameters.AddWithValue("@age", 20 + (i % 60));
            await insert.ExecuteNonQueryAsync();
        }
    }



    [GlobalCleanup]
    public void Cleanup()
    {
        _connection?.Dispose();
    }



    [Benchmark]
    public async Task<int> Extract()
    {
        var extractor = new DbExtractor<BenchmarkRecord, Report>
        (
            _connection,
            "SELECT id AS Id, first_name AS FirstName, last_name AS LastName, age AS Age FROM People"
        );

        var count = 0;
        await foreach (var _ in extractor.ExtractAsync())
        {
            count++;
        }

        return count;
    }
}



public class BenchmarkRecord
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int Age { get; set; }
}
