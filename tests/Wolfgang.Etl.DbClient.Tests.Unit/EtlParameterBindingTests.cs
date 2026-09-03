using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

public class EtlParameterBindingTests
{
    private static SqliteConnection Seeded()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE TABLE People (first_name TEXT, last_name TEXT, age INTEGER);" +
                          "INSERT INTO People VALUES ('Alice','Smith',30),('Bob','Jones',40);";
        cmd.ExecuteNonQuery();
        return conn;
    }


    [Fact]
    public async Task An_EtlParameter_input_binds_like_a_plain_value()
    {
        using var conn = Seeded();

        var sut = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT first_name, last_name, age FROM People WHERE age = @age",
            new Dictionary<string, object> { ["@age"] = new EtlParameter<int> { Value = 40 } },
            options: null
        );

        var rows = new List<PersonRecord>();
        await foreach (var r in sut.ExtractAsync()) rows.Add(r);

        Assert.Single(rows);
        Assert.Equal("Bob", rows[0].FirstName);
    }


    [Fact]
    public async Task A_plain_value_still_binds_unchanged()
    {
        using var conn = Seeded();

        var sut = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT first_name, last_name, age FROM People WHERE age = @age",
            new Dictionary<string, object> { ["@age"] = 30 },
            options: null
        );

        var rows = new List<PersonRecord>();
        await foreach (var r in sut.ExtractAsync()) rows.Add(r);

        Assert.Single(rows);
        Assert.Equal("Alice", rows[0].FirstName);
    }


    [Fact]
    public async Task A_caller_supplied_DbParameter_binds()
    {
        using var conn = Seeded();
        using var probe = conn.CreateCommand();
        var p = probe.CreateParameter();
        p.ParameterName = "@age";
        p.DbType = DbType.Int32;
        p.Value = 30;

        var sut = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT first_name, last_name, age FROM People WHERE age = @age",
            new Dictionary<string, object> { ["@age"] = p },
            options: null
        );

        var rows = new List<PersonRecord>();
        await foreach (var r in sut.ExtractAsync()) rows.Add(r);

        Assert.Single(rows);
        Assert.Equal("Alice", rows[0].FirstName);
    }


    [Fact]
    public async Task Mixed_plain_and_EtlParameter_values_bind_together()
    {
        using var conn = Seeded();

        var sut = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT first_name, last_name, age FROM People WHERE age = @age AND last_name = @last",
            new Dictionary<string, object>
            {
                ["@age"] = new EtlParameter<int> { Value = 30 },
                ["@last"] = "Smith"
            },
            options: null
        );

        var rows = new List<PersonRecord>();
        await foreach (var r in sut.ExtractAsync()) rows.Add(r);

        Assert.Single(rows);
        Assert.Equal("Alice", rows[0].FirstName);
    }


    [Fact]
    public async Task Server_paging_adds_its_parameters_without_mutating_the_caller_dictionary()
    {
        using var conn = Seeded();

        var supplied = new Dictionary<string, object>
        {
            ["@min"] = new EtlParameter<int> { Value = 0 }
        };

        var sut = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT first_name, last_name, age FROM People WHERE age > @min ORDER BY age",
            supplied,
            new DbExtractorOptions { ServerOffset = 1, ServerLimit = 1 }
        );

        var rows = new List<PersonRecord>();
        await foreach (var r in sut.ExtractAsync()) rows.Add(r);

        Assert.Single(rows);
        Assert.Equal("Bob", rows[0].FirstName);

        // the paging parameters must not have leaked into the caller's own dictionary
        Assert.Single(supplied);
        Assert.True(supplied.ContainsKey("@min"));
    }


    [Fact]
    public async Task A_caller_supplied_paging_parameter_name_is_rejected_rather_than_duplicated()
    {
        // Regression guard: Entries() yields the caller's dictionary then the paging overlay, and
        // AddParameters added both unconditionally - so supplying @PageOffset while paging is
        // configured emitted the name twice and the provider rejected the command.
        using var conn = Seeded();

        var sut = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT first_name, last_name, age FROM People WHERE age > @min ORDER BY age",
            new Dictionary<string, object>
            {
                // An EtlParameter is what routes binding through EtlParameterSet; with only plain
                // values the constructor builds a DynamicParameters instead and this path is never
                // reached. The collision is a property of the EtlParameterSet route.
                ["@min"] = new EtlParameter<int> { Value = 0 },
                ["@PageOffset"] = 99          // the name paging also generates
            },
            new DbExtractorOptions { ServerOffset = 1, ServerLimit = 1 }
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in sut.ExtractAsync()) { }
        });

        Assert.Contains("@PageOffset", ex.Message, StringComparison.Ordinal);
        Assert.Contains("ServerOffset", ex.Message, StringComparison.Ordinal);
    }


    [Fact]
    public async Task A_caller_supplied_paging_name_is_fine_when_paging_is_not_configured()
    {
        // The guard must not fire on a name that merely LOOKS like a paging parameter - it is only
        // a conflict when paging would actually generate it.
        using var conn = Seeded();

        var sut = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT first_name, last_name, age FROM People WHERE age > @PageOffset ORDER BY age",
            new Dictionary<string, object> { ["@PageOffset"] = 0 },
            options: null
        );

        var rows = new List<PersonRecord>();
        await foreach (var r in sut.ExtractAsync()) rows.Add(r);

        Assert.Equal(2, rows.Count);
    }


    [Fact]
    public async Task Paging_still_works_when_the_caller_supplies_no_conflicting_name()
    {
        using var conn = Seeded();

        var sut = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT first_name, last_name, age FROM People WHERE age > @min ORDER BY age",
            new Dictionary<string, object> { ["@min"] = 0 },
            new DbExtractorOptions { ServerOffset = 1, ServerLimit = 1 }
        );

        var rows = new List<PersonRecord>();
        await foreach (var r in sut.ExtractAsync()) rows.Add(r);

        Assert.Single(rows);
        Assert.Equal("Bob", rows[0].FirstName);
    }


    [Fact]
    public async Task A_caller_supplied_paging_name_collides_on_the_plain_value_path_too()
    {
        // The same conflict, but with only plain values - which routes through DynamicParameters
        // rather than EtlParameterSet. Documents whether that path is guarded as well.
        using var conn = Seeded();

        var sut = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT first_name, last_name, age FROM People WHERE age > @min ORDER BY age",
            new Dictionary<string, object>
            {
                ["@min"] = 0,
                ["@PageOffset"] = 99
            },
            new DbExtractorOptions { ServerOffset = 1, ServerLimit = 1 }
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in sut.ExtractAsync()) { }
        });

        Assert.Contains("@PageOffset", ex.Message, StringComparison.Ordinal);
    }
}
