// Tests for the opt-in ValidateSchemaOnStart property on
// DbExtractor and DbLoader (#20, option B).
//
// The contract:
//   * Default is false: mapping mismatches don't throw until the real
//     query runs.
//   * When true: extractor/loader invoke DbSchemaValidator BEFORE the
//     first row is touched, so a copy-paste column typo fails fast
//     with the validator's clear message rather than a driver-level
//     "no such column" surfaced deep inside the read loop.
//   * When true and mapping is correct: extract/load succeed normally.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

public class ValidateSchemaOnStartTests
{
    [ExcludeFromCodeCoverage]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    [Table("widget")]
    public sealed class Widget
    {
        [Key]
        [Column("id")] public int Id { get; set; }
        [Column("name")] public string Name { get; set; } = "";
        [Column("price")] public decimal Price { get; set; }
    }

    [ExcludeFromCodeCoverage]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    [Table("widget")]
    public sealed class BadWidget
    {
        [Column("id")] public int Id { get; set; }
        [Column("name")] public string Name { get; set; } = "";
        [Column("does_not_exist")] public string Ghost { get; set; } = "";
    }

    private static SqliteConnection CreateSeededConnection()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE widget (id INTEGER PRIMARY KEY, name TEXT NOT NULL, price REAL NOT NULL);
            INSERT INTO widget (id, name, price) VALUES
                (1, 'a', 1.0),
                (2, 'b', 2.0);
        ";
        cmd.ExecuteNonQuery();
        return conn;
    }



    [Fact]
    public void Extractor_ValidateSchemaOnStart_defaults_to_false()
    {
        using var conn = CreateSeededConnection();
        var sut = new DbExtractor<Widget>(conn, "SELECT id AS Id, name AS Name, price AS Price FROM widget");
        Assert.False(sut.ValidateSchemaOnStart);
    }



    [Fact]
    public async Task Extractor_ValidateSchemaOnStart_true_throws_before_first_row_when_mapping_wrong()
    {
        using var conn = CreateSeededConnection();
        var sut = new DbExtractor<BadWidget>(conn, "SELECT id AS Id, name AS Name FROM widget")
        {
            ValidateSchemaOnStart = true,
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in sut.ExtractAsync().ConfigureAwait(false))
            {
                Assert.Fail("Validator should have thrown before yielding any row.");
            }
        }).ConfigureAwait(false);

        Assert.Contains("does_not_exist", ex.Message, StringComparison.Ordinal);
        Assert.Equal(0, sut.CurrentItemCount);
    }



    [Fact]
    public async Task Extractor_ValidateSchemaOnStart_true_succeeds_when_mapping_correct()
    {
        using var conn = CreateSeededConnection();
        var sut = new DbExtractor<Widget>(conn, "SELECT id AS Id, name AS Name, price AS Price FROM widget")
        {
            ValidateSchemaOnStart = true,
        };

        var rows = new List<Widget>();
        await foreach (var row in sut.ExtractAsync().ConfigureAwait(false))
        {
            rows.Add(row);
        }

        Assert.Equal(2, rows.Count);
    }



    [Fact]
    public void Loader_ValidateSchemaOnStart_defaults_to_false()
    {
        using var conn = CreateSeededConnection();
        var sut = new DbLoader<Widget>(conn, "INSERT INTO widget (id, name, price) VALUES (@Id, @Name, @Price)");
        Assert.False(sut.ValidateSchemaOnStart);
    }



    [Fact]
    public async Task Loader_ValidateSchemaOnStart_true_throws_before_first_write_when_mapping_wrong()
    {
        using var conn = CreateSeededConnection();
        var sut = new DbLoader<BadWidget>
        (
            conn,
            "INSERT INTO widget (id, name) VALUES (@Id, @Name)"
        )
        {
            ValidateSchemaOnStart = true,
        };

        var items = ToAsync(new[]
        {
            new BadWidget { Id = 10, Name = "x", Ghost = "g" },
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await sut.LoadAsync(items).ConfigureAwait(false)).ConfigureAwait(false);

        Assert.Contains("does_not_exist", ex.Message, StringComparison.Ordinal);

        using var count = conn.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM widget";
        var after = Convert.ToInt64(count.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(2L, after);
    }



    [Fact]
    public async Task Loader_ValidateSchemaOnStart_true_succeeds_when_mapping_correct()
    {
        using var conn = CreateSeededConnection();
        var sut = new DbLoader<Widget>
        (
            conn,
            "INSERT INTO widget (id, name, price) VALUES (@Id, @Name, @Price)"
        )
        {
            ValidateSchemaOnStart = true,
        };

        await sut.LoadAsync(ToAsync(new[]
        {
            new Widget { Id = 10, Name = "x", Price = 9m },
        })).ConfigureAwait(false);

        using var count = conn.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM widget";
        var after = Convert.ToInt64(count.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(3L, after);
    }



    private static async IAsyncEnumerable<T> ToAsync<T>(IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            yield return item;
            await Task.Yield();
        }
    }
}
