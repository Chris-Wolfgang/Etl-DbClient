using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.Data.Sqlite;
using Wolfgang.Etl.DbClient;

namespace Wolfgang.Etl.DbClient.Benchmarks;

/// <summary>
/// Compares per-record <c>ExecuteAsync</c> against the new <see cref="DbLoader{TRecord}.BatchSize"/>
/// path. Runs against an in-memory SQLite connection — the gap on networked DBs
/// (SQL Server / PostgreSQL / MySQL) is much wider because each per-row round-trip
/// pays full network latency, but in-memory SQLite gives a reproducible lower bound.
/// </summary>
[MemoryDiagnoser]
public class DbLoaderBatchSizeBenchmarks : IDisposable
{
    [Table("benchmark_people")]
    public class BenchmarkPerson
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public int Id { get; set; }

        [Column("first_name")]
        public string FirstName { get; set; } = string.Empty;

        [Column("last_name")]
        public string LastName { get; set; } = string.Empty;

        [Column("age")]
        public int Age { get; set; }

        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    private const int RowCount = 1000;
    private DbConnection _conn = null!;
    private List<BenchmarkPerson> _records = null!;

    [Params(1, 50, 500)]
    public int BatchSize { get; set; }


    [GlobalSetup]
    public void Setup()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();

        using (var cmd = _conn.CreateCommand())
        {
            cmd.CommandText =
                "CREATE TABLE benchmark_people (" +
                " id INTEGER PRIMARY KEY AUTOINCREMENT," +
                " first_name TEXT, last_name TEXT, age INTEGER," +
                " email TEXT, created_at TEXT)";
            cmd.ExecuteNonQuery();
        }

        _records = Enumerable.Range(0, RowCount).Select(i => new BenchmarkPerson
        {
            FirstName = "First" + i,
            LastName = "Last" + i,
            Age = 20 + (i % 50),
            Email = "user" + i + "@example.com",
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        }).ToList();
    }


    [IterationSetup]
    public void IterationSetup()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "DELETE FROM benchmark_people";
        cmd.ExecuteNonQuery();
    }


    [GlobalCleanup]
    public void Cleanup()
    {
        _conn.Dispose();
    }


    public void Dispose() => _conn?.Dispose();


    [Benchmark]
    public async Task Load1000Rows()
    {
        var loader = new DbLoader<BenchmarkPerson>(_conn, WriteMode.Insert);
        loader.BatchSize = BatchSize;
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
