using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Wolfgang.Etl.TestKit.Xunit;

// Constructs via the deprecated constructors. Migrating to the options overloads is
// follow-up work; the deprecation exists to warn consumers, and the options constructors
// are covered by DbOptionsDefaultsTests.
#pragma warning disable CS0618

namespace Wolfgang.Etl.DbClient.Tests.Unit;

/// <summary>
/// Adopts <see cref="SupportsDryRunContractTests{TSut}"/> for
/// <see cref="DbLoader{TRecord}"/>'s <c>ISupportDryRun</c> implementation.
/// Adds the property-contract tests + the two behavioural tests
/// (side effect suppressed on dry-run, side effect performed when off)
/// without duplicating what <see cref="DbLoaderTests"/> already covers.
/// </summary>
/// <remarks>
/// The pre-existing <c>DbLoaderTests.LoadAsync_when_IsDryRun_is_true_does_not_write_to_database</c>
/// is retained deliberately — it exercises the batched
/// <see cref="DbLoader{TRecord}.InsertBatchSize"/> code path, which the
/// contract tests don't reach (the contract's default per-row path goes
/// through the loader base's <c>LoadWorkerAsync</c>). Property tests
/// <c>IsDryRun_defaults_to_false</c> + <c>IsDryRun_set_and_get_roundtrips</c>
/// in DbLoaderTests ARE subsumed and have been pruned from that file.
/// </remarks>
public sealed class DbLoaderSupportsDryRunContractTests
    : SupportsDryRunContractTests<DbLoader<DryRunContractRecord>>
{
    protected override DbLoader<DryRunContractRecord> CreateSut()
    {
        var conn = OpenSeededConnection();
        return new DbLoader<DryRunContractRecord>
        (
            conn,
            "INSERT INTO contract_dryrun (id, name) VALUES (@Id, @Name)"
        );
    }



    protected override async Task<bool> RunAndReportSideEffectAsync(bool isDryRun)
    {
        // Each call gets its own SUT + connection so the "IsDryRun=false → rows
        // land" and "IsDryRun=true → rows don't land" scenarios can't leak
        // state through a shared in-memory database.
        using var conn = OpenSeededConnection();

        var sut = new DbLoader<DryRunContractRecord>
        (
            conn,
            "INSERT INTO contract_dryrun (id, name) VALUES (@Id, @Name)"
        )
        {
            IsDryRun = isDryRun,
        };

        await sut.LoadAsync(GenerateAsync(rowCount: 3));

        // Side effect = at least one row landed in the destination table.
        using var count = conn.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM contract_dryrun;";
        var landed = Convert.ToInt64(await count.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
        return landed > 0;
    }



    private static SqliteConnection OpenSeededConnection()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var create = conn.CreateCommand();
        create.CommandText = "CREATE TABLE contract_dryrun (id INTEGER PRIMARY KEY, name TEXT NOT NULL);";
        create.ExecuteNonQuery();
        return conn;
    }



    private static async IAsyncEnumerable<DryRunContractRecord> GenerateAsync(int rowCount)
    {
        for (var i = 1; i <= rowCount; i++)
        {
            yield return new DryRunContractRecord { Id = i, Name = "contract-" + i };
            await Task.Yield();
        }
    }
}


// Test fixture for the SupportsDryRun contract. Kept alongside the test
// class rather than in a shared file since nothing else in the project
// needs a dry-run POCO.
public sealed class DryRunContractRecord
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}
