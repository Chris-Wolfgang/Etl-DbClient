using System.Diagnostics.CodeAnalysis;
using Wolfgang.Etl.DbClient.Tests.Integration.Fixtures;
using Xunit;

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
}
