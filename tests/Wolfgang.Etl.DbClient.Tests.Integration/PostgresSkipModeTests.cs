using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Wolfgang.Etl.DbClient.Tests.Integration.Fixtures;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Integration;

/// <summary>
/// <c>RowErrorHandling.Skip</c> on the PostgreSQL family. PostgreSQL and CockroachDB abort the whole
/// transaction on the first failed statement and reject every later one with SQLSTATE 25P02, so Skip
/// cannot continue in one transaction there: the loader fails fast and points at
/// <c>BatchCommitSize = 1</c>, which runs each row in its own transaction.
/// </summary>
public abstract class SkipModeAbortedTransactionTestsBase
{
    protected abstract IDbProviderFixture Fixture { get; }



    private static async Task<int> CountRowsAsync(DbConnection conn)
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



    private static DbLoader<ContractItem> CreateLoader(DbConnection conn, int batchCommitSize) => new
    (
        conn,
        "INSERT INTO contract_items (name, value) VALUES (@Name, @Value)",
        new DbLoaderOptions { ErrorHandling = RowErrorHandling.Skip, BatchCommitSize = batchCommitSize }
    );



    [SkippableFact]
    public async Task Skip_mode_in_one_transaction_when_the_transaction_is_aborted_throws_InvalidOperationException()
    {
        Skip.IfNot(Fixture.Available, Fixture.UnavailableReason);

        using var conn = await Fixture.OpenConnectionAsync();
        await Fixture.ResetSchemaAsync(conn);
        var loader = CreateLoader(conn, batchCommitSize: 0);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => loader.LoadAsync(GoodBadGood()));

        Assert.Equal
        (
            "25P02",
            Assert.IsAssignableFrom<DbException>(ex.InnerException).SqlState
        );
    }



    [SkippableFact]
    public async Task Skip_mode_in_one_transaction_when_the_transaction_is_aborted_reports_only_the_bad_row_and_commits_nothing()
    {
        Skip.IfNot(Fixture.Available, Fixture.UnavailableReason);

        using var conn = await Fixture.OpenConnectionAsync();
        await Fixture.ResetSchemaAsync(conn);
        var loader = CreateLoader(conn, batchCommitSize: 0);
        var failures = new List<Exception>();
        loader.RowFailed += (_, e) => failures.Add(e.Exception);

        await Assert.ThrowsAsync<InvalidOperationException>(() => loader.LoadAsync(GoodBadGood()));

        Assert.Single(failures);
        Assert.Equal(0, await CountRowsAsync(conn));
    }



    [SkippableFact]
    public async Task Skip_mode_with_a_transaction_per_row_skips_only_the_bad_row()
    {
        Skip.IfNot(Fixture.Available, Fixture.UnavailableReason);

        using var conn = await Fixture.OpenConnectionAsync();
        await Fixture.ResetSchemaAsync(conn);
        var loader = CreateLoader(conn, batchCommitSize: 1);
        var failures = new List<Exception>();
        loader.RowFailed += (_, e) => failures.Add(e.Exception);

        await loader.LoadAsync(GoodBadGood());

        Assert.Single(failures);
        Assert.Equal(2, await CountRowsAsync(conn));
    }
}



[Collection("Postgres")]
[Trait("Category", "postgres")]
public sealed class PostgresSkipModeTests : SkipModeAbortedTransactionTestsBase
{
    private readonly PostgresFixture _fixture;

    public PostgresSkipModeTests(PostgresFixture fixture) => _fixture = fixture;

    protected override IDbProviderFixture Fixture => _fixture;
}



[Collection("CockroachDb")]
[Trait("Category", "cockroachdb")]
public sealed class CockroachDbSkipModeTests : SkipModeAbortedTransactionTestsBase
{
    private readonly CockroachDbFixture _fixture;

    public CockroachDbSkipModeTests(CockroachDbFixture fixture) => _fixture = fixture;

    protected override IDbProviderFixture Fixture => _fixture;
}
