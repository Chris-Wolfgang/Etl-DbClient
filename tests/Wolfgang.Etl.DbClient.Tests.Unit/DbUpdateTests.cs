using Wolfgang.Etl.Abstractions;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

public class DbUpdateTests
{
    // ------------------------------------------------------------------
    // WriteMode.Update — auto-generated
    // ------------------------------------------------------------------

    [Fact]
    public async Task LoadAsync_with_WriteMode_Update_updates_existing_rows()
    {
        using var conn = await TestDb.CreateConnectionWithDataAsync(3);

        var loader = new DbLoader<PersonRecord>(conn, WriteMode.Update);

        var updates = new[]
        {
            new PersonRecord { Id = 1, FirstName = "Updated1", LastName = "Last1", Age = 99 },
            new PersonRecord { Id = 2, FirstName = "Updated2", LastName = "Last2", Age = 88 },
        };

        await loader.LoadAsync(updates.ToAsyncEnumerable());

        // Verify updates
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT first_name FROM People WHERE id = 1";
        var result = await cmd.ExecuteScalarAsync();
        Assert.Equal("Updated1", result);

        cmd.CommandText = "SELECT age FROM People WHERE id = 2";
        result = await cmd.ExecuteScalarAsync();
        Assert.Equal(88L, result);
    }



    [Fact]
    public void CommandText_with_WriteMode_Update_contains_update_and_where()
    {
        using var conn = TestDb.CreateConnection();
        var loader = new DbLoader<PersonRecord>(conn, WriteMode.Update);

        Assert.Contains("UPDATE", loader.CommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WHERE", loader.CommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("People", loader.CommandText, StringComparison.Ordinal);
    }



    [Fact]
    public void CommandText_with_WriteMode_Update_excludes_key_from_set_clause()
    {
        using var conn = TestDb.CreateConnection();
        var loader = new DbLoader<PersonRecord>(conn, WriteMode.Update);

        // id should be in WHERE, not in SET
        var setClause = loader.CommandText.Split(new[] { "WHERE" }, StringSplitOptions.None)[0];
        Assert.DoesNotContain("id =", setClause, StringComparison.Ordinal);
    }
}
