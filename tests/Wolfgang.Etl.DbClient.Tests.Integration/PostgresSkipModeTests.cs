using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wolfgang.Etl.DbClient.Tests.Integration.Fixtures;
using Xunit;

#pragma warning disable CS0618

namespace Wolfgang.Etl.DbClient.Tests.Integration;

/// <summary>
/// <c>RowErrorHandling.Skip</c> keeps loading inside the same transaction. PostgreSQL aborts the whole
/// transaction on the first failed statement (SQLSTATE 25P02 for every later one), so Skip needs a
/// per-row transaction there. These tests pin that documented limitation.
/// </summary>
[Collection("Postgres")]
[Trait("Category", "postgres")]
public sealed class PostgresSkipModeTests
{
    private readonly PostgresFixture _fixture;

    public PostgresSkipModeTests(PostgresFixture fixture) => _fixture = fixture;



    private static async Task<int> CountRowsAsync(System.Data.Common.DbConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM contract_items";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }



    private static IAsyncEnumerable<ContractItem> GoodBadGood() =>
        new[]
        {
            new ContractItem { Name = "good1", Value = 1 },
            new ContractItem { Name = null!, Value = 2 },
            new ContractItem { Name = "good3", Value = 3 },
        }.ToAsyncEnumerable();



    [SkippableFact]
    public async Task Skip_mode_in_one_transaction_fails_every_row_after_the_first_failure_and_commits_nothing()
    {
        Skip.IfNot(_fixture.Available, _fixture.UnavailableReason);

        using var conn = await _fixture.OpenConnectionAsync();
        await _fixture.ResetSchemaAsync(conn);
        var loader = new DbLoader<ContractItem>
        (
            conn,
            "INSERT INTO contract_items (name, value) VALUES (@Name, @Value)",
            new DbLoaderOptions { ErrorHandling = RowErrorHandling.Skip }
        );
        var failures = new List<Exception>();
        loader.RowFailed += (_, e) => failures.Add(e.Exception);

        await loader.LoadAsync(GoodBadGood());

        Assert.Equal(2, failures.Count);
        Assert.Contains("25P02", failures[1].Message, StringComparison.Ordinal);
        Assert.Equal(0, await CountRowsAsync(conn));
    }



    [SkippableFact]
    public async Task Skip_mode_with_a_transaction_per_row_skips_only_the_bad_row()
    {
        Skip.IfNot(_fixture.Available, _fixture.UnavailableReason);

        using var conn = await _fixture.OpenConnectionAsync();
        await _fixture.ResetSchemaAsync(conn);
        var loader = new DbLoader<ContractItem>
        (
            conn,
            "INSERT INTO contract_items (name, value) VALUES (@Name, @Value)",
            new DbLoaderOptions { ErrorHandling = RowErrorHandling.Skip, BatchCommitSize = 1 }
        );
        var failures = new List<Exception>();
        loader.RowFailed += (_, e) => failures.Add(e.Exception);

        await loader.LoadAsync(GoodBadGood());

        Assert.Single(failures);
        Assert.Equal(2, await CountRowsAsync(conn));
    }
}
