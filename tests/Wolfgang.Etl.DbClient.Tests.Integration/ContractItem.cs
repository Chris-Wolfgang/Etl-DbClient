using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Wolfgang.Etl.DbClient.Tests.Integration;

/// <summary>
/// Test record used by every provider's integration suite. Lower-case identifiers
/// avoid PostgreSQL's unquoted-folding behaviour while remaining valid in
/// SQL Server, MySQL, and SQLite.
/// </summary>
[ExcludeFromCodeCoverage]
[Table("contract_items")]
public sealed class ContractItem : IEquatable<ContractItem>
{
    [Column("name")]
    public string Name { get; set; } = string.Empty;



    [Column("value")]
    public int Value { get; set; }



    public bool Equals(ContractItem? other)
    {
        if (other is null) return false;
        return string.Equals(Name, other.Name, StringComparison.Ordinal) && Value == other.Value;
    }



    public override bool Equals(object? obj) => Equals(obj as ContractItem);



    public override int GetHashCode() => HashCode.Combine(Name, Value);
}
