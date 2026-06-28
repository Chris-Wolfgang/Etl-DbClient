// ReSharper disable UnusedAutoPropertyAccessor.Global -- consumed by Dapper / PublicAPI consumers via reflection (not visible to static analysis)

namespace Wolfgang.Etl.DbClient.Example;

public class EmployeeRecord
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public double Salary { get; set; }
}