using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

public class MultiRowInsertScanningTests
{
    [ExcludeFromCodeCoverage]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    private sealed class Thing
    {
        public int Id { get; set; }

        public string AllowedValues { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }



    private static SqliteConnection CreateConnection()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE TABLE T (Id INTEGER, AllowedValues TEXT, Name TEXT, Note TEXT)";
        cmd.ExecuteNonQuery();
        return conn;
    }



    private static IAsyncEnumerable<Thing> Things(int count) =>
        Enumerable.Range(1, count)
            .Select(i => new Thing { Id = i, AllowedValues = "v" + i, Name = "n" + i })
            .ToAsyncEnumerable();



    private static async Task<List<string>> ReadAsync(SqliteConnection conn, string column)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {column} FROM T ORDER BY Id";
        var values = new List<string>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }



    private static DbLoader<Thing> Loader(SqliteConnection conn, string sql) =>
        new(conn, sql, new DbLoaderOptions { InsertBatchSize = 2 });



    [Fact]
    public async Task LoadAsync_with_InsertBatchSize_when_a_parameter_name_ends_in_Values_finds_the_real_VALUES_keyword()
    {
        using var conn = CreateConnection();

        await Loader(conn, "INSERT INTO T (Id, AllowedValues) VALUES (@Id, @AllowedValues)").LoadAsync(Things(3));

        Assert.Equal(new[] { "v1", "v2", "v3" }, await ReadAsync(conn, "AllowedValues"));
    }



    [Fact]
    public async Task LoadAsync_with_InsertBatchSize_when_a_column_is_named_Values_finds_the_real_VALUES_keyword()
    {
        using var conn = CreateConnection();
        using (var rename = conn.CreateCommand())
        {
            rename.CommandText = "ALTER TABLE T ADD COLUMN \"Values\" TEXT";
            rename.ExecuteNonQuery();
        }

        await Loader(conn, "INSERT INTO T (Id, \"Values\") VALUES (@Id, @Name)").LoadAsync(Things(3));

        Assert.Equal(new[] { "n1", "n2", "n3" }, await ReadAsync(conn, "\"Values\""));
    }



    [Fact]
    public async Task LoadAsync_with_InsertBatchSize_ignores_at_signs_inside_string_literals()
    {
        using var conn = CreateConnection();

        await Loader(conn, "INSERT INTO T (Id, Name, Note) VALUES (@Id, @Name, 'it''s n/a@example.com')").LoadAsync(Things(3));

        Assert.Equal(new[] { "n1", "n2", "n3" }, await ReadAsync(conn, "Name"));
        Assert.Equal(new[] { "it's n/a@example.com", "it's n/a@example.com", "it's n/a@example.com" }, await ReadAsync(conn, "Note"));
    }



    [Fact]
    public async Task LoadAsync_with_InsertBatchSize_ignores_double_at_system_variables()
    {
        using var conn = CreateConnection();
        var sut = Loader(conn, "INSERT INTO T (Id, Name, Note) VALUES (@Id, @Name, @@ROWCOUNT)");

        // SQLite has no @@ variables, so execution fails at the provider; what matters is that the
        // template scan itself accepted @@ROWCOUNT instead of reporting a missing property.
        var ex = await Record.ExceptionAsync(() => sut.LoadAsync(Things(2)));

        Assert.IsNotType<InvalidOperationException>(ex);
    }



    [Fact]
    public async Task LoadAsync_with_InsertBatchSize_and_non_at_parameter_markers_throws_a_clear_error()
    {
        using var conn = CreateConnection();
        var sut = Loader(conn, "INSERT INTO T (Id, Name) VALUES (:Id, :Name)");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.LoadAsync(Things(2)));

        Assert.Contains("'@name' parameters", ex.Message, StringComparison.Ordinal);
    }
}
