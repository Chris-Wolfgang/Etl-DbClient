# Getting Started

This guide will help you quickly get up and running with Wolfgang.Etl.DbClient.

## Prerequisites

- A project targeting .NET Framework 4.6.2+, .NET Standard 2.0, or .NET 5.0+
- An ADO.NET provider for your database (for example `Microsoft.Data.SqlClient`, `Npgsql`,
  `MySqlConnector` or `Microsoft.Data.Sqlite`)

## Installation

### Via NuGet Package Manager

```bash
dotnet add package Wolfgang.Etl.DbClient
```

### Via Package Manager Console

```powershell
Install-Package Wolfgang.Etl.DbClient
```

## Quick Start

```csharp
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.Data.Sqlite;
using Wolfgang.Etl.DbClient;

// Open a connection (caller owns the lifetime)
using var connection = new SqliteConnection("Data Source=mydb.db");
await connection.OpenAsync();

// EXTRACT: stream rows from a query
var extractor = new DbExtractor<EmployeeRecord>
(
    connection,
    "SELECT id, first_name, last_name, salary FROM Employees WHERE salary > @Min",
    new Dictionary<string, object> { { "Min", 50000 } },
    new DbExtractorOptions { CommandTimeout = TimeSpan.FromSeconds(30) }
);

await foreach (var employee in extractor.ExtractAsync())
{
    Console.WriteLine($"{employee.FirstName} {employee.LastName}: ${employee.Salary:N0}");
}

// LOAD: insert records from an async stream
var loader = new DbLoader<EmployeeRecord>
(
    connection,
    WriteMode.Insert,
    new DbLoaderOptions { InsertBatchSize = 100 }
);

await loader.LoadAsync(GetNewHiresAsync());

// Example async stream of records to load
async IAsyncEnumerable<EmployeeRecord> GetNewHiresAsync()
{
    yield return new EmployeeRecord { FirstName = "Jane", LastName = "Doe", Salary = 75000 };
    await Task.CompletedTask;
}

// Record type with attribute-based mapping
[Table("Employees")]
public class EmployeeRecord
{
    [Column("id")]   public int Id { get; set; }
    [Column("first_name")] public string FirstName { get; set; } = "";
    [Column("last_name")]  public string LastName { get; set; } = "";
    [Column("salary")]     public double Salary { get; set; }
}
```

## Next Steps

- Explore the [API Reference](../api/index.md) for detailed documentation
- Read the [Introduction](introduction.md) to learn more about Wolfgang.Etl.DbClient
- Check out example projects in the [GitHub repository](https://github.com/Chris-Wolfgang/Etl-DbClient)

## Common Issues

- **The connection is closed.** By default the caller owns the connection: open it before
  calling `ExtractAsync` / `LoadAsync`, or set `ManageConnection = true` in the options to have
  the extractor or loader open and close it.
- **`Incorrect syntax near 'LIMIT'` (or a similar paging error).** Paging syntax is
  dialect-specific. Set `PagingClauseTemplate` to the preset for your database, for example
  `PagingClauseTemplates.SqlServer`.
- **`RowErrorHandling.Skip` still aborts the load.** Skip only applies on the per-record path:
  leave both `BatchSize` and `InsertBatchSize` at `1`.

## Additional Resources

- [GitHub Repository](https://github.com/Chris-Wolfgang/Etl-DbClient)
- [Contributing Guidelines](https://github.com/Chris-Wolfgang/Etl-DbClient/blob/main/CONTRIBUTING.md)
- [Report an Issue](https://github.com/Chris-Wolfgang/Etl-DbClient/issues)
