using Wolfgang.Etl.Abstractions;
using Wolfgang.Etl.TestKit.Xunit;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

public class DbExtractorTests
    : ExtractorBaseContractTests<
        DbExtractor<ContractRecord>,
        ContractRecord,
        DbReport>
{
    // ------------------------------------------------------------------
    // Contract test factory methods
    // ------------------------------------------------------------------

    private static readonly IReadOnlyList<ContractRecord> ExpectedItems = new[]
    {
        new ContractRecord { Name = "Item1", Value = 10 },
        new ContractRecord { Name = "Item2", Value = 20 },
        new ContractRecord { Name = "Item3", Value = 30 },
        new ContractRecord { Name = "Item4", Value = 40 },
        new ContractRecord { Name = "Item5", Value = 50 },
    };



    /// <inheritdoc/>
    protected override DbExtractor<ContractRecord> CreateSut(int itemCount)
    {
        var conn = TestDb.CreateContractConnection(itemCount);
        return new DbExtractor<ContractRecord>
        (
            conn,
            "SELECT Name, Value FROM ContractItems ORDER BY Value"
        );
    }



    /// <inheritdoc/>
    protected override IReadOnlyList<ContractRecord> CreateExpectedItems() => ExpectedItems;



    /// <inheritdoc/>
    protected override DbExtractor<ContractRecord> CreateSutWithTimer(IProgressTimer timer)
    {
        var conn = TestDb.CreateContractConnection(5);
        return new DbExtractor<ContractRecord>
        (
            conn,
            "SELECT Name, Value FROM ContractItems ORDER BY Value",
            timer
        );
    }


    // ------------------------------------------------------------------
    // Constructor validation
    // ------------------------------------------------------------------

    [Fact]
    public void Constructor_when_connection_is_null_throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>
        (
            () => new DbExtractor<PersonRecord>(null!, "SELECT 1")
        );
    }



    [Fact]
    public void Constructor_when_commandText_is_null_throws_ArgumentNullException()
    {
        using var conn = TestDb.CreateConnection();
        Assert.Throws<ArgumentNullException>
        (
            () => new DbExtractor<PersonRecord>(conn, (string)null!)
        );
    }



    [Fact]
    public void Constructor_with_parameters_when_parameters_is_null_throws_ArgumentNullException()
    {
        using var conn = TestDb.CreateConnection();
        Assert.Throws<ArgumentNullException>
        (
            () => new DbExtractor<PersonRecord>(conn, "SELECT 1", (Dictionary<string, object>)null!)
        );
    }



    // ------------------------------------------------------------------
    // Basic extraction
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExtractAsync_returns_all_rows()
    {
        using var conn = await TestDb.CreateConnectionWithDataAsync(3);
        var extractor = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT id, first_name, last_name, age FROM People ORDER BY id"
        );

        var results = await extractor.ExtractAsync().ToListAsync();

        Assert.Equal(3, results.Count);
        Assert.Equal("First1", results[0].FirstName);
        Assert.Equal("Last3", results[2].LastName);
        Assert.Equal(22, results[1].Age);
    }



    [Fact]
    public async Task ExtractAsync_with_empty_result_set_returns_no_items()
    {
        using var conn = await TestDb.CreateConnectionWithDataAsync(0);
        var extractor = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT id, first_name, last_name, age FROM People"
        );

        var results = await extractor.ExtractAsync().ToListAsync();

        Assert.Empty(results);
    }



    // ------------------------------------------------------------------
    // Parameterized queries
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExtractAsync_with_parameters_filters_correctly()
    {
        using var conn = await TestDb.CreateConnectionWithDataAsync(5);
        var extractor = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT id, first_name, last_name, age FROM People WHERE age > @MinAge",
            new Dictionary<string, object>(StringComparer.Ordinal) { { "MinAge", 23 } }
        );

        var results = await extractor.ExtractAsync().ToListAsync();

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.Age > 23));
    }



    // ------------------------------------------------------------------
    // Auto-generated SELECT from attributes
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExtractAsync_with_auto_generated_select_returns_all_rows()
    {
        using var conn = await TestDb.CreateConnectionWithDataAsync(3);
        var extractor = new DbExtractor<PersonRecord>(conn);

        var results = await extractor.ExtractAsync().ToListAsync();

        Assert.Equal(3, results.Count);
    }



    [Fact]
    public void CommandText_with_auto_generated_select_contains_table_name()
    {
        using var conn = TestDb.CreateConnection();
        var extractor = new DbExtractor<PersonRecord>(conn);

        Assert.Contains("People", extractor.CommandText, StringComparison.Ordinal);
    }



    // ------------------------------------------------------------------
    // SkipItemCount / MaximumItemCount
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExtractAsync_when_SkipItemCount_is_set_skips_rows()
    {
        using var conn = await TestDb.CreateConnectionWithDataAsync(5);
        var extractor = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT id, first_name, last_name, age FROM People ORDER BY id"
        );
        extractor.SkipItemCount = 2;

        var results = await extractor.ExtractAsync().ToListAsync();

        Assert.Equal(3, results.Count);
        Assert.Equal("First3", results[0].FirstName);
    }



    [Fact]
    public async Task ExtractAsync_when_MaximumItemCount_is_set_stops_early()
    {
        using var conn = await TestDb.CreateConnectionWithDataAsync(5);
        var extractor = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT id, first_name, last_name, age FROM People ORDER BY id"
        );
        extractor.MaximumItemCount = 2;

        var results = await extractor.ExtractAsync().ToListAsync();

        Assert.Equal(2, results.Count);
    }



    // ------------------------------------------------------------------
    // Transaction support
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExtractAsync_with_transaction_uses_provided_transaction()
    {
        using var conn = await TestDb.CreateConnectionWithDataAsync(3);
#if NETFRAMEWORK
        using var transaction = conn.BeginTransaction();
#else
        using var transaction = await conn.BeginTransactionAsync();
#endif
        var extractor = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT id, first_name, last_name, age FROM People ORDER BY id",
            transaction
        );

        var results = await extractor.ExtractAsync().ToListAsync();

        Assert.Equal(3, results.Count);
    }



    // ------------------------------------------------------------------
    // Progress report
    // ------------------------------------------------------------------

    [Fact]
    public async Task GetProgressReport_returns_DbReport_with_counts()
    {
        using var conn = await TestDb.CreateConnectionWithDataAsync(3);
        var extractor = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT id, first_name, last_name, age FROM People"
        );

        await extractor.ExtractAsync().ToListAsync();

        var report = extractor.GetProgressReport();

        Assert.Equal(3, report.CurrentItemCount);
        Assert.Contains("People", report.CommandText, StringComparison.Ordinal);
        Assert.True(report.ElapsedMilliseconds >= 0);
    }
}
