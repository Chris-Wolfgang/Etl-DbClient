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

        // One extractor walks every page: 7 rows in pages of 3 deliberately leaves a
        // partial final page, which is what ends the walk.
        var extractor = new DbExtractor<ContractItem>
        (
            conn,
            OrderedSelect,
            new DbExtractorOptions
            {
                PagingClauseTemplate = Fixture.PagingClauseTemplate,
                PageSize = 3
            }
        );

        var seen = await extractor
            .ExtractAsync()
            .Select(item => item.Value)
            .ToListAsync();

        // No gaps, no duplicates, and in order — the three ways paging goes wrong.
        Assert.Equal(Enumerable.Range(1, 7).Select(i => i * 10), seen);
    }



    [SkippableFact]
    public async Task ExtractAsync_when_PageSize_is_set_counts_SkipItemCount_and_MaximumItemCount_across_pages()
    {
        Skip.IfNot(Fixture.Available, Fixture.UnavailableReason);

        using var conn = await Fixture.OpenConnectionAsync();
        await Fixture.ResetSchemaAsync(conn);
        await Fixture.SeedAsync(conn, rowCount: 7);

        // Skip 1 and take 5 in pages of 2: the window spans three pages, and the last
        // page is clamped to the one row still needed.
        var extractor = new DbExtractor<ContractItem>
        (
            conn,
            OrderedSelect,
            new DbExtractorOptions
            {
                PagingClauseTemplate = Fixture.PagingClauseTemplate,
                PageSize = 2,
                SkipItemCount = 1,
                MaximumItemCount = 5
            }
        );

        var seen = await extractor
            .ExtractAsync()
            .Select(item => item.Value)
            .ToListAsync();

        Assert.Equal
        (
            new[] { 20, 30, 40, 50, 60 },
            seen
        );
        Assert.Equal(1, extractor.CurrentSkippedItemCount);
    }



    [SkippableFact]
    public async Task ExtractAsync_when_template_is_set_without_PageSize_skips_on_the_server()
    {
        Skip.IfNot(Fixture.Available, Fixture.UnavailableReason);

        using var conn = await Fixture.OpenConnectionAsync();
        await Fixture.ResetSchemaAsync(conn);
        await Fixture.SeedAsync(conn, rowCount: 5);

        // Template set, no PageSize: one query with the skip pushed into the offset.
        // The skipped rows are never fetched but still count as skipped.
        var extractor = new DbExtractor<ContractItem>
        (
            conn,
            OrderedSelect,
            new DbExtractorOptions
            {
                PagingClauseTemplate = Fixture.PagingClauseTemplate,
                SkipItemCount = 2
            }
        );

        var results = await extractor.ExtractAsync().ToListAsync();

        Assert.Equal
        (
            new[] { "Item3", "Item4", "Item5" },
            results.Select(item => item.Name)
        );
        Assert.Equal(2, extractor.CurrentSkippedItemCount);
    }



    [SkippableFact]
    public async Task ExtractAsync_when_template_is_set_without_PageSize_pushes_skip_and_max_into_one_query()
    {
        Skip.IfNot(Fixture.Available, Fixture.UnavailableReason);

        using var conn = await Fixture.OpenConnectionAsync();
        await Fixture.ResetSchemaAsync(conn);
        await Fixture.SeedAsync(conn, rowCount: 5);

        var extractor = new DbExtractor<ContractItem>
        (
            conn,
            OrderedSelect,
            new DbExtractorOptions
            {
                PagingClauseTemplate = Fixture.PagingClauseTemplate,
                SkipItemCount = 1,
                MaximumItemCount = 2
            }
        );

        var results = await extractor.ExtractAsync().ToListAsync();

        Assert.Equal
        (
            new[] { "Item2", "Item3" },
            results.Select(item => item.Name)
        );
        Assert.Equal(1, extractor.CurrentSkippedItemCount);
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
    public async Task ExtractAsync_when_only_ServerLimit_is_set_pages_from_the_top()
    {
        Skip.IfNot(Fixture.Available, Fixture.UnavailableReason);

        using var conn = await Fixture.OpenConnectionAsync();
        await Fixture.ResetSchemaAsync(conn);
        await Fixture.SeedAsync(conn, rowCount: 5);

        // Reversed deliberately. This previously asserted that a limit with no offset was a
        // silent no-op returning all 5 rows — the caller asking for 2 and getting everything.
        // ServerLimit is an alias of MaximumItemCount, the template switches paging on,
        // and an unset offset means 0.
        var extractor = new DbExtractor<ContractItem>(conn, OrderedSelect)
        {
            PagingClauseTemplate = Fixture.PagingClauseTemplate,
            ServerLimit = 2
        };

        var results = await extractor.ExtractAsync().ToListAsync();

        Assert.Equal(2, results.Count);
        Assert.Equal("Item1", results[0].Name);
        Assert.Equal("Item2", results[1].Name);
    }
}
