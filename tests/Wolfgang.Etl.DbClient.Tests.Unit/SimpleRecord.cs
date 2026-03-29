using System.Diagnostics.CodeAnalysis;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

[ExcludeFromCodeCoverage]
public class SimpleRecord
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
