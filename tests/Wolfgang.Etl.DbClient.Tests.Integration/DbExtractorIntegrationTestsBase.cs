using System.Diagnostics.CodeAnalysis;
using Wolfgang.Etl.DbClient.Tests.Integration.Fixtures;
using Xunit;

// Constructs via the deprecated constructors. Migrating to the options overloads is
// follow-up work; the deprecation exists to warn consumers, and the options constructors
// are covered by DbOptionsDefaultsTests.
#pragma warning disable CS0618

namespace Wolfgang.Etl.DbClient.Tests.Integration;

/// <summary>
/// Reusable extractor contract: every concrete RDBMS test class derives from this
/// and supplies its own <see cref="IDbProviderFixture"/>. Tests are skipped (not
/// failed) when the fixture's container could not start.
/// </summary>
[ExcludeFromCodeCoverage]
public abstract class DbExtractorIntegrationTestsBase
{
    protected abstract IDbProviderFixture Fixture { get; }



    [SkippableFact]
    public async Task ExtractAsync_yields_all_seeded_rows()
    {
        Skip.IfNot(Fixture.Available, Fixture.UnavailableReason);

        using var conn = await Fixture.OpenConnectionAsync();
        await Fixture.ResetSchemaAsync(conn);
        await Fixture.SeedAsync(conn, rowCount: 5);

        var extractor = new DbExtractor<ContractItem>(conn, "SELECT name AS Name, value AS Value FROM contract_items ORDER BY value");

        var results = await extractor.ExtractAsync().ToListAsync();

        Assert.Equal(5, results.Count);
        Assert.Equal("Item1", results[0].Name);
        Assert.Equal(10, results[0].Value);
        Assert.Equal("Item5", results[4].Name);
        Assert.Equal(50, results[4].Value);
    }



    [SkippableFact]
    public async Task ExtractAsync_when_table_is_empty_yields_nothing()
    {
        Skip.IfNot(Fixture.Available, Fixture.UnavailableReason);

        using var conn = await Fixture.OpenConnectionAsync();
        await Fixture.ResetSchemaAsync(conn);

        var extractor = new DbExtractor<ContractItem>(conn, "SELECT name AS Name, value AS Value FROM contract_items");

        var results = await extractor.ExtractAsync().ToListAsync();

        Assert.Empty(results);
    }



    [SkippableFact]
    public async Task ExtractAsync_when_SkipItemCount_is_set_skips_initial_rows()
    {
        Skip.IfNot(Fixture.Available, Fixture.UnavailableReason);

        using var conn = await Fixture.OpenConnectionAsync();
        await Fixture.ResetSchemaAsync(conn);
        await Fixture.SeedAsync(conn, rowCount: 5);

        var extractor = new DbExtractor<ContractItem>(conn, "SELECT name AS Name, value AS Value FROM contract_items ORDER BY value")
        {
            SkipItemCount = 2
        };

        var results = await extractor.ExtractAsync().ToListAsync();

        Assert.Equal(3, results.Count);
        Assert.Equal("Item3", results[0].Name);
    }



    [SkippableFact]
    public async Task ExtractAsync_when_MaximumItemCount_is_set_stops_early()
    {
        Skip.IfNot(Fixture.Available, Fixture.UnavailableReason);

        using var conn = await Fixture.OpenConnectionAsync();
        await Fixture.ResetSchemaAsync(conn);
        await Fixture.SeedAsync(conn, rowCount: 5);

        var extractor = new DbExtractor<ContractItem>(conn, "SELECT name AS Name, value AS Value FROM contract_items ORDER BY value")
        {
            MaximumItemCount = 2
        };

        var results = await extractor.ExtractAsync().ToListAsync();

        Assert.Equal(2, results.Count);
        Assert.Equal("Item1", results[0].Name);
        Assert.Equal("Item2", results[1].Name);
    }


    // ---- Server-side paging -------------------------------------------------
    //
    // These run the fixture's own PagingClauseTemplate against its own engine, so a
    // preset that is syntactically wrong for an RDBMS fails here instead of in a
    // consumer's query. Every query orders by value: the OFFSET/FETCH form requires
    // an ORDER BY, and paging without a total order is not deterministic anyway.

    private const string OrderedSelect =
        "SELECT name AS Name, value AS Value FROM contract_items ORDER BY value";



    [SkippableFact]
    public async Task ExtractAsync_when_server_paging_is_configured_returns_the_expected_window()
    {
        Skip.IfNot(Fixture.Available, Fixture.UnavailableReason);

        using var conn = await Fixture.OpenConnectionAsync();
        await Fixture.ResetSchemaAsync(conn);
        await Fixture.SeedAsync(conn, rowCount: 5);

        var extractor = new DbExtractor<ContractItem>(conn, OrderedSelect)
        {
            PagingClauseTemplate = Fixture.PagingClauseTemplate,
            ServerOffset = 1,
            ServerLimit = 2
        };

        var results = await extractor.ExtractAsync().ToListAsync();

        Assert.Equal(2, results.Count);
        Assert.Equal("Item2", results[0].Name);
        Assert.Equal("Item3", results[1].Name);
    }



    [SkippableFact]
    public async Task ExtractAsync_when_paging_through_the_whole_table_yields_every_row_exactly_once()
    {
        Skip.IfNot(Fixture.Available, Fixture.UnavailableReason);

        using var conn = await Fixture.OpenConnectionAsync();
        await Fixture.ResetSchemaAsync(conn);
        await Fixture.SeedAsync(conn, rowCount: 7);

        // 7 rows in pages of 3 deliberately leaves a partial final page.
        const int pageSize = 3;
        var seen = new List<int>();

        for (var offset = 0; ; offset += pageSize)
        {
            var extractor = new DbExtractor<ContractItem>(conn, OrderedSelect)
            {
                PagingClauseTemplate = Fixture.PagingClauseTemplate,
                ServerOffset = offset,
                ServerLimit = pageSize
            };

            var page = await extractor.ExtractAsync().ToListAsync();

            if (page.Count == 0)
            {
                break;
            }

            Assert.True(page.Count <= pageSize, $"page at offset {offset} returned {page.Count} rows");
            seen.AddRange(page.Select(item => item.Value));
        }

        // No gaps, no duplicates, and in order — the three ways paging goes wrong.
        Assert.Equal(Enumerable.Range(1, 7).Select(i => i * 10), seen);
    }



    [SkippableFact]
    public async Task ExtractAsync_when_ServerLimit_exceeds_the_rows_remaining_returns_only_what_is_left()
    {
        Skip.IfNot(Fixture.Available, Fixture.UnavailableReason);

        using var conn = await Fixture.OpenConnectionAsync();
        await Fixture.ResetSchemaAsync(conn);
        await Fixture.SeedAsync(conn, rowCount: 5);

        var extractor = new DbExtractor<ContractItem>(conn, OrderedSelect)
        {
            PagingClauseTemplate = Fixture.PagingClauseTemplate,
            ServerOffset = 3,
            ServerLimit = 100
        };

        var results = await extractor.ExtractAsync().ToListAsync();

        Assert.Equal(2, results.Count);
        Assert.Equal("Item4", results[0].Name);
        Assert.Equal("Item5", results[1].Name);
    }



    [SkippableFact]
    public async Task ExtractAsync_when_ServerOffset_is_past_the_last_row_yields_nothing()
    {
        Skip.IfNot(Fixture.Available, Fixture.UnavailableReason);

        using var conn = await Fixture.OpenConnectionAsync();
        await Fixture.ResetSchemaAsync(conn);
        await Fixture.SeedAsync(conn, rowCount: 5);

        var extractor = new DbExtractor<ContractItem>(conn, OrderedSelect)
        {
            PagingClauseTemplate = Fixture.PagingClauseTemplate,
            ServerOffset = 50,
            ServerLimit = 10
        };

        var results = await extractor.ExtractAsync().ToListAsync();

        Assert.Empty(results);
    }



    [SkippableFact]
    public async Task ExtractAsync_when_only_ServerLimit_is_set_does_not_page_at_all()
    {
        Skip.IfNot(Fixture.Available, Fixture.UnavailableReason);

        using var conn = await Fixture.OpenConnectionAsync();
        await Fixture.ResetSchemaAsync(conn);
        await Fixture.SeedAsync(conn, rowCount: 5);

        // Paging is all-or-nothing: ApplyServerPaging is a no-op unless BOTH ServerOffset
        // and ServerLimit are set. Pinning it because the failure mode is silent — a caller
        // who sets only a limit gets every row rather than an error.
        var extractor = new DbExtractor<ContractItem>(conn, OrderedSelect)
        {
            PagingClauseTemplate = Fixture.PagingClauseTemplate,
            ServerLimit = 2
        };

        var results = await extractor.ExtractAsync().ToListAsync();

        Assert.Equal(5, results.Count);
    }
}
