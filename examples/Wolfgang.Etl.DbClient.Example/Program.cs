using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Wolfgang.Etl.DbClient;
using Wolfgang.Etl.DbClient.Example;

// ---------------------------------------------------------------
// Example: Extract → Transform → Load using Wolfgang.Etl.DbClient
// ---------------------------------------------------------------

// Set up an in-memory SQLite database
using var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync().ConfigureAwait(false);

// Create source and destination tables
using (var cmd = connection.CreateCommand())
{
    cmd.CommandText = @"
        CREATE TABLE Employees (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            first_name TEXT NOT NULL,
            last_name TEXT NOT NULL,
            salary REAL NOT NULL
        );
        CREATE TABLE HighEarners (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            full_name TEXT NOT NULL,
            salary REAL NOT NULL
        );
        INSERT INTO Employees (first_name, last_name, salary) VALUES ('Alice', 'Smith', 95000);
        INSERT INTO Employees (first_name, last_name, salary) VALUES ('Bob', 'Jones', 45000);
        INSERT INTO Employees (first_name, last_name, salary) VALUES ('Carol', 'Brown', 120000);
        INSERT INTO Employees (first_name, last_name, salary) VALUES ('Dan', 'Wilson', 85000);
        INSERT INTO Employees (first_name, last_name, salary) VALUES ('Eve', 'Davis', 150000);
    ";
    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
}

Console.WriteLine("=== ETL Pipeline: Employees → HighEarners (salary > 80000) ===");
Console.WriteLine();

// EXTRACT: Read employees with salary > 80000
var extractor = new DbExtractor<EmployeeRecord, DbReport>
(
    connection,
    "SELECT id AS Id, first_name AS FirstName, last_name AS LastName, salary AS Salary FROM Employees WHERE salary > @MinSalary",
    new System.Collections.Generic.Dictionary<string, object> { { "MinSalary", 80000 } }
);

// LOAD: Insert into HighEarners table
var loader = new DbLoader<HighEarnerRecord, DbReport>
(
    connection,
    "INSERT INTO HighEarners (full_name, salary) VALUES (@FullName, @Salary)"
);

// Transform in-flight: combine first + last name
var count = 0;
await loader.LoadAsync(TransformAsync()).ConfigureAwait(false);

async System.Collections.Generic.IAsyncEnumerable<HighEarnerRecord> TransformAsync()
{
    await foreach (var employee in extractor.ExtractAsync().ConfigureAwait(false))
    {
        count++;
        Console.WriteLine($"  Extracted: {employee.FirstName} {employee.LastName} (${employee.Salary:N0})");
        yield return new HighEarnerRecord
        {
            FullName = $"{employee.FirstName} {employee.LastName}",
            Salary = employee.Salary
        };
    }
}

Console.WriteLine();
Console.WriteLine($"=== Results: {count} high earners loaded ===");
Console.WriteLine();

// Verify the results
using (var cmd = connection.CreateCommand())
{
    cmd.CommandText = "SELECT full_name, salary FROM HighEarners ORDER BY salary DESC";
    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
    while (await reader.ReadAsync().ConfigureAwait(false))
    {
        Console.WriteLine($"  {reader.GetString(0)}: ${reader.GetDouble(1):N0}");
    }
}
