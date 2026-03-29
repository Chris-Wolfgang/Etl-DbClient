using System;
using System.Diagnostics.CodeAnalysis;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

[ExcludeFromCodeCoverage]
public class ContractRecord
{
    public string Name { get; set; } = string.Empty;



    public int Value { get; set; }



    public override bool Equals(object? obj)
    {
        if (obj is not ContractRecord other) return false;
        return string.Equals(Name, other.Name, StringComparison.Ordinal) && Value == other.Value;
    }



    public override int GetHashCode()
    {
#if NETCOREAPP2_1_OR_GREATER || NET5_0_OR_GREATER
        return HashCode.Combine(Name, Value);
#else
        unchecked
        {
            return (Name != null ? StringComparer.Ordinal.GetHashCode(Name) : 0) * 397 ^ Value.GetHashCode();
        }
#endif
    }
}
