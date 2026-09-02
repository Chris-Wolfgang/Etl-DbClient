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
}
