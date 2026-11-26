using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

/// <summary>
/// Pins how the multi-row INSERT path (<c>InsertBatchSize &gt; 1</c>) finds the <c>VALUES</c>
/// keyword and validates its template, for the surviving mutants the Stryker run on #474 reported
/// in <c>DbLoader.FindValuesKeyword</c>, <c>IsIdentifierPart</c> and <c>ExtractInsertPrefix</c>.
/// </summary>
public class MultiRowInsertKeywordSurvivorTests
{
    [ExcludeFromCodeCoverage]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    private sealed class Thing
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }



    private static SqliteConnection CreateConnection()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE TABLE T (Id INTEGER, Name TEXT, Note TEXT)";
        cmd.ExecuteNonQuery();
        return conn;
    }



    private static IAsyncEnumerable<Thing> Things(int count) =>
        Enumerable
            .Range(1, count)
            .Select(i => new Thing { Id = i, Name = "n" + i })
            .ToAsyncEnumerable();



    private static async Task<List<string>> ReadNotesAsync(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Note FROM T ORDER BY Id";
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



    [Theory]
    [InlineData("_")]
    [InlineData("@")]
    [InlineData(":")]
    [InlineData("?")]
    [InlineData("$")]
    [InlineData("#")]
    [InlineData(".")]
    [InlineData("\"")]
    [InlineData("[")]
    [InlineData("]")]
    [InlineData("`")]
    public async Task LoadAsync_with_InsertBatchSize_when_the_template_holds_VALUES_after_an_identifier_character_finds_the_real_keyword(string neighbour)
    {
        using var conn = CreateConnection();
        var note = neighbour + "values";

        await Loader(conn, $"INSERT INTO T (Id, Name, Note) VALUES (@Id, @Name, '{note}')").LoadAsync(Things(3));

        Assert.Equal(new[] { note, note, note }, await ReadNotesAsync(conn));
    }



    [Fact]
    public async Task LoadAsync_with_InsertBatchSize_when_CommandText_starts_with_VALUES_uses_it()
    {
        using var conn = CreateConnection();
        var sut = Loader(conn, "VALUES (@Id, @Name)");

        await sut.LoadAsync(Things(3));

        Assert.Equal(3, sut.CurrentItemCount);
    }



    [Fact]
    public async Task LoadAsync_with_InsertBatchSize_when_CommandText_ends_with_VALUES_throws_the_template_error()
    {
        using var conn = CreateConnection();
        var sut = Loader(conn, "INSERT INTO T (Id, Name) VALUES");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.LoadAsync(Things(2)));

        Assert.Equal
        (
            "InsertBatchSize > 1 requires the VALUES clause to be a single parenthesized parameter template such as '(@A, @B, @C)'. Got: ",
            ex.Message
        );
    }



    [Fact]
    public async Task LoadAsync_with_InsertBatchSize_when_VALUES_is_only_part_of_a_name_throws_the_missing_VALUES_error()
    {
        using var conn = CreateConnection();
        const string sql = "INSERT INTO T (Id) SELECT @AllowedValues";
        var sut = Loader(conn, sql);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.LoadAsync(Things(2)));

        Assert.Equal
        (
            "InsertBatchSize > 1 requires CommandText to contain 'VALUES (template)'. CommandText was: " + sql,
            ex.Message
        );
    }



    [Fact]
    public async Task LoadAsync_with_InsertBatchSize_when_the_template_is_empty_parentheses_throws_the_no_parameters_error()
    {
        using var conn = CreateConnection();
        var sut = Loader(conn, "INSERT INTO T VALUES ()");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.LoadAsync(Things(2)));

        Assert.Equal
        (
            "InsertBatchSize > 1 found no '@name' parameters in the VALUES template. Only '@' parameter markers " +
            "are supported for multi-row inserts (not ':name' or '?'). Template: ()",
            ex.Message
        );
    }
}
