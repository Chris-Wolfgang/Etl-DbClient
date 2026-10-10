using System.ComponentModel.DataAnnotations.Schema;

namespace Wolfgang.Etl.DbClient.Tests.Integration;

/// <summary>
/// Test record used by every provider's integration suite. Lower-case identifiers
/// avoid PostgreSQL's unquoted-folding behaviour while remaining valid in
/// SQL Server, MySQL, and SQLite.
/// </summary>
[Table("contract_items")]
public sealed record ContractItem
{
    [Column("name")]
    public string Name { get; init; } = string.Empty;



    [Column("value")]
    public int Value { get; init; }
}
