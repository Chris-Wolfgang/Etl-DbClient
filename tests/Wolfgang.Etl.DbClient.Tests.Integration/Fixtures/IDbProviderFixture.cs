using System.Data.Common;

namespace Wolfgang.Etl.DbClient.Tests.Integration.Fixtures;

/// <summary>
/// Common contract every per-RDBMS fixture exposes to the contract-style test bases.
/// </summary>
public interface IDbProviderFixture
{
    /// <summary>
    /// Friendly provider name used in test output ("sqlserver", "postgres", ...).
    /// </summary>
    string ProviderName { get; }



    /// <summary>
    /// The paging clause this engine requires — normally a preset from
    /// <see cref="PagingClauseTemplates"/>. Declared per fixture so the paging contract
    /// tests exercise the real preset against the real engine: if a preset is wrong for
    /// this RDBMS, the paging tests fail here rather than in a consumer's production query.
    /// </summary>
    string PagingClauseTemplate { get; }



    /// <summary>
    /// True when the underlying environment (e.g. Docker daemon) was reachable
    /// and the schema was provisioned. Tests <c>Skip.IfNot(Available, ...)</c> on
    /// this so a developer without Docker can still run the rest of the suite.
    /// </summary>
    bool Available { get; }



    /// <summary>
    /// Optional reason explaining why <see cref="Available"/> is false.
    /// </summary>
    string? UnavailableReason { get; }



    /// <summary>
    /// Open a fresh connection to the per-fixture test database. Caller owns disposal.
    /// </summary>
    Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);



    /// <summary>
    /// Drop and recreate the <c>contract_items</c> table so each test starts from a
    /// known empty state. Implementations should be safe to call repeatedly.
    /// </summary>
    Task ResetSchemaAsync(DbConnection connection, CancellationToken cancellationToken = default);



    /// <summary>
    /// Seed the <c>contract_items</c> table with <paramref name="rowCount"/> rows
    /// where each row is <c>Name="Item{i}"</c>, <c>Value=i*10</c> (1-based).
    /// </summary>
    Task SeedAsync(DbConnection connection, int rowCount, CancellationToken cancellationToken = default);
}
