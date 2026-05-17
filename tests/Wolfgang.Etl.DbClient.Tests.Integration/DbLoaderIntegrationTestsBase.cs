using System.Diagnostics.CodeAnalysis;
using Wolfgang.Etl.DbClient.Tests.Integration.Fixtures;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Integration;

/// <summary>
/// Reusable loader contract: every concrete RDBMS test class derives from this
/// and supplies its own <see cref="IDbProviderFixture"/>. Tests are skipped (not
/// failed) when the fixture's container could not start.
/// </summary>
[ExcludeFromCodeCoverage]
public abstract class DbLoaderIntegrationTestsBase
{
    protected abstract IDbProviderFixture Fixture { get; }



    private static IAsyncEnumerable<ContractItem> Source(int count) =>
        Enumerable.Range(1, count)
            .Select(i => new ContractItem { Name = $"Item{i}", Value = i * 10 })
            .ToAsyncEnumerable();



    private static async Task<int> CountRowsAsync(System.Data.Common.DbConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM contract_items";
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
    }



    [SkippableFact]
    public async Task LoadAsync_inserts_all_rows()
    {
        Skip.IfNot(Fixture.Available, Fixture.UnavailableReason);

        using var conn = await Fixture.OpenConnectionAsync();
        await Fixture.ResetSchemaAsync(conn);

        var loader = new DbLoader<ContractItem>(conn, "INSERT INTO contract_items (name, value) VALUES (@Name, @Value)");

        await loader.LoadAsync(Source(5));

        Assert.Equal(5, await CountRowsAsync(conn));
    }



    [SkippableFact]
    public async Task LoadAsync_when_source_is_empty_inserts_nothing()
    {
        Skip.IfNot(Fixture.Available, Fixture.UnavailableReason);

        using var conn = await Fixture.OpenConnectionAsync();
        await Fixture.ResetSchemaAsync(conn);

        var loader = new DbLoader<ContractItem>(conn, "INSERT INTO contract_items (name, value) VALUES (@Name, @Value)");

        await loader.LoadAsync(Source(0));

        Assert.Equal(0, await CountRowsAsync(conn));
    }



    [SkippableFact]
    public async Task LoadAsync_when_MaximumItemCount_is_set_stops_at_limit()
    {
        Skip.IfNot(Fixture.Available, Fixture.UnavailableReason);

        using var conn = await Fixture.OpenConnectionAsync();
        await Fixture.ResetSchemaAsync(conn);

        var loader = new DbLoader<ContractItem>(conn, "INSERT INTO contract_items (name, value) VALUES (@Name, @Value)")
        {
            MaximumItemCount = 3
        };

        await loader.LoadAsync(Source(10));

        Assert.Equal(3, await CountRowsAsync(conn));
    }
}
