// ReSharper disable UnusedAutoPropertyAccessor.Global -- consumed by Dapper / PublicAPI consumers via reflection (not visible to static analysis)

namespace Wolfgang.Etl.DbClient.Example;

public class HighEarnerRecord
{
    public string FullName { get; set; } = string.Empty;
    public double Salary { get; set; }
}