using Dapper;
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
        using var conn = await TestDb.CreateConnectionWithDataAsync();
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
        using var conn = await TestDb.CreateConnectionWithDataAsync();
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
        using var conn = await TestDb.CreateConnectionWithDataAsync();
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



    // ------------------------------------------------------------------
    // TotalCountQuery
    // ------------------------------------------------------------------

    [Fact]
    public async Task TotalCountQuery_when_null_TotalItemCount_is_null()
    {
        using var conn = await TestDb.CreateConnectionWithDataAsync(3);
        var extractor = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT id, first_name, last_name, age FROM People"
        );

        await extractor.ExtractAsync().ToListAsync();

        Assert.Null(extractor.GetProgressReport().TotalItemCount);
    }



    [Fact]
    public async Task TotalCountQuery_using_default_TotalItemCount_equals_row_count()
    {
        using var conn = await TestDb.CreateConnectionWithDataAsync(3);
        var extractor = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT id, first_name, last_name, age FROM People"
        );
        extractor.TotalCountQuery = extractor.DefaultTotalCountQuery;

        await extractor.ExtractAsync().ToListAsync();

        Assert.Equal(3, extractor.GetProgressReport().TotalItemCount);
    }



    [Fact]
    public async Task TotalCountQuery_using_default_with_parameterized_query_returns_filtered_count()
    {
        using var conn = await TestDb.CreateConnectionWithDataAsync();
        var extractor = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT id, first_name, last_name, age FROM People WHERE age > @MinAge",
            new Dictionary<string, object>(StringComparer.Ordinal) { { "MinAge", 23 } }
        );
        extractor.TotalCountQuery = extractor.DefaultTotalCountQuery;

        await extractor.ExtractAsync().ToListAsync();

        Assert.Equal(2, extractor.GetProgressReport().TotalItemCount);
    }



    [Fact]
    public async Task TotalCountQuery_using_custom_func_returns_custom_count()
    {
        using var conn = await TestDb.CreateConnectionWithDataAsync(3);
        var extractor = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT id, first_name, last_name, age FROM People"
        );
        // Don't capture `conn` in the lambda — ReSharper's AccessToDisposedClosure
        // flag is technically correct: the using-scope could outlive the closure.
        // The test only needs to prove a custom TotalCountQuery's return value is
        // surfaced through GetProgressReport().TotalItemCount, so return a constant.
        extractor.TotalCountQuery = _ => Task.FromResult(3);

        await extractor.ExtractAsync().ToListAsync();

        Assert.Equal(3, extractor.GetProgressReport().TotalItemCount);
    }



    // ------------------------------------------------------------------
    // TotalCountQuery — SanitizeCommandTextForCount edge cases (review #15)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("SELECT id, first_name, last_name, age FROM People;")]
    [InlineData("SELECT id, first_name, last_name, age FROM People;;")]
    [InlineData("SELECT id, first_name, last_name, age FROM People; ; ;  ")]
    public async Task DefaultTotalCountQuery_strips_trailing_semicolons_and_whitespace(string commandText)
    {
        using var conn = await TestDb.CreateConnectionWithDataAsync(3);
        var extractor = new DbExtractor<PersonRecord>(conn, commandText);
        extractor.TotalCountQuery = extractor.DefaultTotalCountQuery;

        await extractor.ExtractAsync().ToListAsync();

        Assert.Equal(3, extractor.GetProgressReport().TotalItemCount);
    }



    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n\r")]
    public async Task DefaultTotalCountQuery_throws_when_command_text_is_blank(string blank)
    {
        using var conn = await TestDb.CreateConnectionWithDataAsync(1);
        var extractor = new DbExtractor<PersonRecord>(conn, blank);
        extractor.TotalCountQuery = extractor.DefaultTotalCountQuery;

        await Assert.ThrowsAsync<InvalidOperationException>
        (
            async () => await extractor.ExtractAsync().ToListAsync()
        );
    }



    // ------------------------------------------------------------------
    // CommandTimeout (#25)
    // ------------------------------------------------------------------

    [Fact]
    public void CommandTimeout_defaults_to_null()
    {
        using var conn = TestDb.CreateConnection();
        var extractor = new DbExtractor<PersonRecord>(conn, "SELECT 1");

        Assert.Null(extractor.CommandTimeout);
    }



    [Fact]
    public void CommandTimeout_set_and_get_roundtrips()
    {
        using var conn = TestDb.CreateConnection();
        var extractor = new DbExtractor<PersonRecord>(conn, "SELECT 1");

        extractor.CommandTimeout = TimeSpan.FromMinutes(5);

        Assert.Equal(TimeSpan.FromMinutes(5), extractor.CommandTimeout);
    }



    [Fact]
    public void CommandTimeout_when_set_to_negative_throws_ArgumentOutOfRangeException()
    {
        using var conn = TestDb.CreateConnection();
        var extractor = new DbExtractor<PersonRecord>(conn, "SELECT 1");

        Assert.Throws<ArgumentOutOfRangeException>
        (
            () => extractor.CommandTimeout = TimeSpan.FromSeconds(-1)
        );
    }



    // ------------------------------------------------------------------
    // CommandType (#26)
    // ------------------------------------------------------------------

    [Fact]
    public void CommandType_defaults_to_Text()
    {
        using var conn = TestDb.CreateConnection();
        var extractor = new DbExtractor<PersonRecord>(conn, "SELECT 1");

        Assert.Equal(System.Data.CommandType.Text, extractor.CommandType);
    }



    [Fact]
    public void CommandType_set_and_get_roundtrips()
    {
        using var conn = TestDb.CreateConnection();
        var extractor = new DbExtractor<PersonRecord>(conn, "usp_GetPeople");

        extractor.CommandType = System.Data.CommandType.StoredProcedure;

        Assert.Equal(System.Data.CommandType.StoredProcedure, extractor.CommandType);
    }



    [Fact]
    public async Task CommandType_Text_still_executes_normal_query()
    {
        // Explicitly setting to Text (the default) should be a no-op regression check.
        using var conn = await TestDb.CreateConnectionWithDataAsync(3);
        var extractor = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT id, first_name, last_name, age FROM People ORDER BY id"
        )
        {
            CommandType = System.Data.CommandType.Text
        };

        var results = await extractor.ExtractAsync().ToListAsync();

        Assert.Equal(3, results.Count);
    }



    // ------------------------------------------------------------------
    // DbProviderFactory ctor (#28)
    // ------------------------------------------------------------------

    [Fact]
    public void DbProviderFactory_ctor_when_factory_is_null_throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>
        (
            () => new DbExtractor<PersonRecord>(null!, "Data Source=:memory:", "SELECT 1")
        );
    }



    [Fact]
    public void DbProviderFactory_ctor_when_connectionString_is_null_throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>
        (
            () => new DbExtractor<PersonRecord>(Microsoft.Data.Sqlite.SqliteFactory.Instance, null!, "SELECT 1")
        );
    }



    [Fact]
    public void DbProviderFactory_ctor_when_commandText_is_null_throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>
        (
            () => new DbExtractor<PersonRecord>(Microsoft.Data.Sqlite.SqliteFactory.Instance, "Data Source=:memory:", null!)
        );
    }



    [Fact]
    public async Task DbProviderFactory_ctor_extractor_opens_and_disposes_connection()
    {
        // Owned-connection path: connection is created from SqliteFactory, opened
        // on first use inside ExtractWorkerAsync, and disposed when the iterator
        // completes. Smoke-test that the round-trip succeeds for an empty query
        // (in-memory SQLite, fresh schema, returns 0 rows).
        var extractor = new DbExtractor<PersonRecord>
        (
            Microsoft.Data.Sqlite.SqliteFactory.Instance,
            "Data Source=:memory:",
            "SELECT 1 AS id, 'x' AS first_name, 'y' AS last_name, 30 AS age WHERE 0=1"
        );

        var results = await extractor.ExtractAsync().ToListAsync();

        Assert.Empty(results);
    }



    [Fact]
    public async Task CommandTimeout_does_not_break_extraction_against_in_memory_db()
    {
        // SQLite in-memory ignores commandTimeout but the call path must still
        // succeed when a non-null timeout is supplied. Guards against accidentally
        // routing through a code path that doesn't pass the timeout cleanly.
        using var conn = await TestDb.CreateConnectionWithDataAsync(3);
        var extractor = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT id, first_name, last_name, age FROM People ORDER BY id"
        )
        {
            CommandTimeout = TimeSpan.FromMinutes(2)
        };

        var results = await extractor.ExtractAsync().ToListAsync();

        Assert.Equal(3, results.Count);
    }



    // ------------------------------------------------------------------
    // CountAsync (#32)
    // ------------------------------------------------------------------

    [Fact]
    public async Task CountAsync_uses_default_count_query_and_returns_row_count()
    {
        using var conn = await TestDb.CreateConnectionWithDataAsync(rowCount: 7);

        var extractor = new DbExtractor<PersonRecord>(conn, "SELECT first_name AS FirstName FROM People");

        var count = await extractor.CountAsync();

        Assert.Equal(7, count);
    }



    [Fact]
    public async Task CountAsync_with_custom_TotalCountQuery_returns_that_value()
    {
        using var conn = await TestDb.CreateConnectionWithDataAsync(rowCount: 4);

        var extractor = new DbExtractor<PersonRecord>(conn, "SELECT first_name AS FirstName FROM People")
        {
            TotalCountQuery = _ => Task.FromResult(42)
        };

        var count = await extractor.CountAsync();

        Assert.Equal(42, count);
    }



    // ------------------------------------------------------------------
    // Server-side paging (#33)
    // ------------------------------------------------------------------

    [Fact]
    public void Paging_defaults_disable_server_side_paging()
    {
        using var conn = TestDb.CreateConnection();
        var extractor = new DbExtractor<PersonRecord>(conn, "SELECT first_name AS FirstName FROM People");

        Assert.Null(extractor.ServerOffset);
        Assert.Null(extractor.ServerLimit);
        Assert.Equal("LIMIT @PageLimit OFFSET @PageOffset", extractor.PagingClauseTemplate);
    }



    [Fact]
    public async Task ExtractAsync_with_ServerLimit_caps_rows_at_the_database()
    {
        using var conn = await TestDb.CreateConnectionWithDataAsync(rowCount: 20);

        var extractor = new DbExtractor<PersonRecord>(conn, "SELECT first_name AS FirstName, last_name AS LastName, age AS Age FROM People ORDER BY id")
        {
            ServerOffset = 0,
            ServerLimit = 5
        };

        var records = await extractor.ExtractAsync().ToListAsync();

        Assert.Equal(5, records.Count);
        Assert.Equal("First1", records[0].FirstName);
        Assert.Equal("First5", records[4].FirstName);
    }



    [Fact]
    public async Task ExtractAsync_with_ServerOffset_and_ServerLimit_returns_a_page()
    {
        using var conn = await TestDb.CreateConnectionWithDataAsync(rowCount: 20);

        var extractor = new DbExtractor<PersonRecord>(conn, "SELECT first_name AS FirstName, last_name AS LastName, age AS Age FROM People ORDER BY id")
        {
            ServerOffset = 10,
            ServerLimit = 5
        };

        var records = await extractor.ExtractAsync().ToListAsync();

        // Skipped 10, took 5 → First11..First15.
        Assert.Equal(5, records.Count);
        Assert.Equal("First11", records[0].FirstName);
        Assert.Equal("First15", records[4].FirstName);
    }



    [Fact]
    public async Task ExtractAsync_with_only_ServerOffset_does_not_apply_paging()
    {
        // The clause is only appended when BOTH are set.
        using var conn = await TestDb.CreateConnectionWithDataAsync(rowCount: 10);

        var extractor = new DbExtractor<PersonRecord>(conn, "SELECT first_name AS FirstName, last_name AS LastName, age AS Age FROM People ORDER BY id")
        {
            ServerOffset = 5
        };

        var records = await extractor.ExtractAsync().ToListAsync();

        Assert.Equal(10, records.Count); // all rows — paging disabled
    }



    // ------------------------------------------------------------------
    // Parameters property override (#27)
    // ------------------------------------------------------------------

    [Fact]
    public void Parameters_defaults_to_null()
    {
        using var conn = TestDb.CreateConnection();
        var extractor = new DbExtractor<PersonRecord>(conn, "SELECT first_name AS FirstName FROM People");

        Assert.Null(extractor.Parameters);
    }



    [Fact]
    public async Task ExtractAsync_when_Parameters_is_set_uses_those_for_the_query()
    {
        using var conn = await TestDb.CreateConnectionWithDataAsync(rowCount: 10);

        // Build a DynamicParameters with a single input parameter; bind it
        // into a parameterized WHERE.
        var p = new DynamicParameters();
        p.Add("@Age", 25);

        var extractor = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT first_name AS FirstName, last_name AS LastName, age AS Age FROM People WHERE age >= @Age"
        )
        {
            Parameters = p
        };

        var records = await extractor.ExtractAsync().ToListAsync();

        // Seed rows have ages 21..30 (20 + i, i=1..10), so age >= 25 matches 6.
        Assert.Equal(6, records.Count);
        Assert.All(records, r => Assert.True(r.Age >= 25));
    }



    [Fact]
    public async Task ExtractAsync_Parameters_takes_precedence_over_constructor_dictionary()
    {
        using var conn = await TestDb.CreateConnectionWithDataAsync(rowCount: 10);

        var dictParams = new Dictionary<string, object> { ["@Age"] = 28 };
        var p = new DynamicParameters();
        p.Add("@Age", 22); // overrides the dictionary value

        var extractor = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT first_name AS FirstName, last_name AS LastName, age AS Age FROM People WHERE age >= @Age",
            dictParams
        )
        {
            Parameters = p
        };

        var records = await extractor.ExtractAsync().ToListAsync();

        // Ages 22..30 = 9 rows (overrode dict's 28→22). If the dictionary
        // had won we'd see 3 rows (28,29,30).
        Assert.Equal(9, records.Count);
    }



    // ------------------------------------------------------------------
    // ManageConnection (#31)
    // ------------------------------------------------------------------

    [Fact]
    public void ManageConnection_defaults_to_false()
    {
        using var conn = TestDb.CreateConnection();
        var extractor = new DbExtractor<PersonRecord>(conn, "SELECT first_name AS FirstName FROM People");

        Assert.False(extractor.ManageConnection);
    }



    [Fact]
    public async Task ExtractAsync_when_ManageConnection_is_true_opens_a_closed_connection_and_closes_it_after()
    {
        // Shared-cache in-memory SQLite so a Close()→Open() cycle preserves
        // schema + data (plain :memory: drops it).
        var connString = $"Data Source=mc_extractor_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        using var keeper = new Microsoft.Data.Sqlite.SqliteConnection(connString);
        await keeper.OpenAsync();
        await TestDb.CreateEmptyTableAsync(keeper);
        using (var seed = keeper.CreateCommand())
        {
            seed.CommandText = "INSERT INTO People (first_name, last_name, age) VALUES ('Ada','Lovelace',36),('Alan','Turing',41),('Grace','Hopper',85)";
            await seed.ExecuteNonQueryAsync();
        }

        using var conn = new Microsoft.Data.Sqlite.SqliteConnection(connString);
        Assert.Equal(System.Data.ConnectionState.Closed, conn.State);

        var extractor = new DbExtractor<PersonRecord>(conn, "SELECT first_name AS FirstName, last_name AS LastName, age AS Age FROM People")
        {
            ManageConnection = true
        };

        var records = await extractor.ExtractAsync().ToListAsync();

        Assert.Equal(3, records.Count);
        Assert.Equal(System.Data.ConnectionState.Closed, conn.State);
    }



    [Fact]
    public async Task ExtractAsync_when_ManageConnection_is_true_leaves_already_open_connections_open()
    {
        using var conn = await TestDb.CreateConnectionWithDataAsync(rowCount: 3);
        Assert.Equal(System.Data.ConnectionState.Open, conn.State);

        var extractor = new DbExtractor<PersonRecord>(conn, "SELECT first_name AS FirstName, last_name AS LastName, age AS Age FROM People")
        {
            ManageConnection = true
        };

        var records = await extractor.ExtractAsync().ToListAsync();

        // We only close what we opened. The caller had it open; it stays open.
        Assert.Equal(3, records.Count);
        Assert.Equal(System.Data.ConnectionState.Open, conn.State);
    }



    [Fact]
    public async Task CountAsync_does_not_affect_progress_state()
    {
        using var conn = await TestDb.CreateConnectionWithDataAsync(rowCount: 5);

        var extractor = new DbExtractor<PersonRecord>(conn, "SELECT first_name AS FirstName FROM People");

        var count = await extractor.CountAsync();

        // CountAsync runs the count query but does not touch the progress
        // counters — those advance only when ExtractAsync streams rows.
        Assert.Equal(5, count);
        Assert.Equal(0, extractor.CurrentItemCount);
    }
}
