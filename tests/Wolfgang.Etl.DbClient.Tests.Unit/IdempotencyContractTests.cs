using System.Collections.Generic;
using System.Linq;
using Wolfgang.Etl.TestKit.Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

/// <summary>
/// Adopts <see cref="IdempotentExtractorContractTests{TSut, TItem, TProgress}"/> for
/// <see cref="DbExtractor{TRecord}"/>: running the same instance twice yields the same rows,
/// reports progress on both runs and resets <c>CurrentItemCount</c> between them.
/// </summary>
public sealed class DbExtractorIdempotencyContractTests
    : IdempotentExtractorContractTests<DbExtractor<ContractRecord>, ContractRecord, DbReport>
{
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
        var connection = TestDb.CreateContractConnection(itemCount);
        return new DbExtractor<ContractRecord>
        (
            connection,
            "SELECT Name, Value FROM ContractItems ORDER BY Value",
            new DbExtractorOptions()
        );
    }



    /// <inheritdoc/>
    protected override IReadOnlyList<ContractRecord> CreateExpectedItems() => ExpectedItems;
}



/// <summary>
/// Adopts <see cref="IdempotentLoaderContractTests{TSut, TItem, TProgress}"/> for
/// <see cref="DbLoader{TRecord}"/>: loading twice through the same instance processes every
/// item both times and resets <c>CurrentItemCount</c> between runs.
/// </summary>
public sealed class DbLoaderIdempotencyContractTests
    : IdempotentLoaderContractTests<DbLoader<ContractRecord>, ContractRecord, DbReport>
{
    /// <inheritdoc/>
    protected override DbLoader<ContractRecord> CreateSut(int itemCount)
    {
        var connection = TestDb.CreateContractLoaderConnection();
        return new DbLoader<ContractRecord>
        (
            connection,
            "INSERT INTO ContractItems (Name, Value) VALUES (@Name, @Value)",
            new DbLoaderOptions()
        );
    }



    /// <inheritdoc/>
    protected override IReadOnlyList<ContractRecord> CreateSourceItems()
        => Enumerable
            .Range(1, 5)
            .Select(i => new ContractRecord { Name = "Item" + i, Value = i * 10 })
            .ToList();



    // DbLoader<T> writes to a database and keeps no snapshot of what it loaded, so the
    // collected-items check does not apply.
    /// <inheritdoc/>
    protected override IReadOnlyList<ContractRecord>? TryGetLoadedItems(DbLoader<ContractRecord> sut) => null;
}
