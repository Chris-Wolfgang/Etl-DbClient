# Wolfgang.Etl.DbClient

Extractors and Loaders for working with SQL databases using ADO.NET

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-Multi--Targeted-purple.svg)](https://dotnet.microsoft.com/)
[![GitHub](https://img.shields.io/badge/GitHub-Repository-181717?logo=github)](https://github.com/Chris-Wolfgang/Etl-DbClient)

---

## 📦 Installation

```bash
dotnet add package Wolfgang.Etl.DbClient
```

**NuGet Package:** Coming soon to NuGet.org

---

## 📄 License

This project is licensed under the **MIT License**. See the [LICENSE](LICENSE) file for details.

---

## 📚 Documentation

- **GitHub Repository:** [https://github.com/Chris-Wolfgang/Etl-DbClient](https://github.com/Chris-Wolfgang/Etl-DbClient)
- **API Documentation:** https://Chris-Wolfgang.github.io/Etl-DbClient/
- **Formatting Guide:** [README-FORMATTING.md](README-FORMATTING.md)
- **Contributing Guide:** [CONTRIBUTING.md](CONTRIBUTING.md)

---

## 🚀 Quick Start

```csharp
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.Data.Sqlite;
using Wolfgang.Etl.DbClient;

// Open a connection (caller owns the lifetime)
using var connection = new SqliteConnection("Data Source=mydb.db");
await connection.OpenAsync();

// EXTRACT: stream rows from a query
var extractor = new DbExtractor<EmployeeRecord, DbReport>(
    connection,
    "SELECT id, first_name, last_name, salary FROM Employees WHERE salary > @Min",
    new Dictionary<string, object> { { "Min", 50000 } }
);

await foreach (var employee in extractor.ExtractAsync())
{
    Console.WriteLine($"{employee.FirstName} {employee.LastName}: ${employee.Salary:N0}");
}

// LOAD: insert records from an async stream
var loader = new DbLoader<EmployeeRecord, DbReport>(
    connection,
    WriteMode.Insert
);

await loader.LoadAsync(GetRecordsAsync());

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

---

## ✨ Features

| Feature | Description |
|---------|-------------|
| Async Streaming | Built on `IAsyncEnumerable<T>` via Dapper's unbuffered reader for constant memory usage |
| Auto-generated SQL | Builds SELECT, INSERT, and UPDATE from `[Table]`, `[Column]`, and `[Key]` attributes |
| Parameterized Queries | Pass named parameters via `Dictionary<string, object>` |
| Column Mapping | Dapper integration with `[Column]` attribute support via `ColumnAttributeTypeMapper` |
| Transaction Support | Caller-managed or auto-managed (commit on success, rollback on failure) |
| Progress Reporting | `DbReport` includes item count, skipped count, command text, and elapsed time |
| Skip & Limit | `SkipItemCount` and `MaximumItemCount` for pagination |
| Structured Logging | Optional `ILogger` integration |
| Multi-TFM | Targets .NET Framework 4.6.2+, .NET Standard 2.0, and .NET 5.0–10.0 |

---

## 🎯 Target Frameworks

| Framework | Versions |
|-----------|----------|
| .NET Framework | .NET 4.6.2, .NET 4.8.1 |
| .NET Standard | .NET Standard 2.0 |
| .NET | .NET 5.0, .NET 6.0, .NET 7.0, .NET 8.0, .NET 9.0, .NET 10.0 |

---

## 🔍 Code Quality & Static Analysis

This project enforces **strict code quality standards** through **7 specialized analyzers** and custom async-first rules:

### Analyzers in Use

1. **Microsoft.CodeAnalysis.NetAnalyzers** - Built-in .NET analyzers for correctness and performance
2. **Roslynator.Analyzers** - Advanced refactoring and code quality rules
3. **AsyncFixer** - Async/await best practices and anti-pattern detection
4. **Microsoft.VisualStudio.Threading.Analyzers** - Thread safety and async patterns
5. **Microsoft.CodeAnalysis.BannedApiAnalyzers** - Prevents usage of banned synchronous APIs
6. **Meziantou.Analyzer** - Comprehensive code quality rules
7. **SonarAnalyzer.CSharp** - Industry-standard code analysis

### Async-First Enforcement

This library uses **`BannedSymbols.txt`** to prohibit synchronous APIs and enforce async-first patterns:

**Blocked APIs Include:**
- ❌ `Task.Wait()`, `Task.Result` - Use `await` instead
- ❌ `Thread.Sleep()` - Use `await Task.Delay()` instead
- ❌ Synchronous file I/O (`File.ReadAllText`) - Use async versions
- ❌ Synchronous stream operations - Use `ReadAsync()`, `WriteAsync()`
- ❌ `Parallel.For/ForEach` - Use `Task.WhenAll()` or `Parallel.ForEachAsync()`
- ❌ Obsolete APIs (`WebClient`, `BinaryFormatter`)

**Why?** To ensure all code is **truly async** and **non-blocking** for optimal performance in async contexts.

---

## 🛠️ Building from Source

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download) or later
- Optional: [PowerShell Core](https://github.com/PowerShell/PowerShell) for formatting scripts

### Build Steps

```bash
# Clone the repository
git clone https://github.com/Chris-Wolfgang/Etl-DbClient.git
cd Etl-DbClient

# Restore dependencies
dotnet restore

# Build the solution
dotnet build --configuration Release

# Run tests
dotnet test --configuration Release

# Run code formatting (PowerShell Core)
pwsh ./scripts/format.ps1
```

### Code Formatting

This project uses `.editorconfig` and `dotnet format`:

```bash
# Format code
dotnet format

# Verify formatting (as CI does)
dotnet format --verify-no-changes
```

See [README-FORMATTING.md](README-FORMATTING.md) for detailed formatting guidelines.

### Building Documentation

This project uses [DocFX](https://dotnet.github.io/docfx/) to generate API documentation:

```bash
# Install DocFX (one-time setup)
dotnet tool install -g docfx

# Generate API metadata and build documentation
cd docfx_project
docfx metadata  # Extract API metadata from source code
docfx build     # Build HTML documentation

# Documentation is generated in the docs/ folder at the repository root
```

The documentation is automatically built and deployed to GitHub Pages when changes are pushed to the `main` branch.

**Local Preview:**
```bash
# Serve documentation locally (with live reload)
cd docfx_project
docfx build --serve

# Open http://localhost:8080 in your browser
```

**Documentation Structure:**
- `docfx_project/` - DocFX configuration and source files
- `docs/` - Generated HTML documentation (published to GitHub Pages)
- `docfx_project/index.md` - Main landing page content
- `docfx_project/docs/` - Additional documentation articles
- `docfx_project/api/` - Auto-generated API reference YAML files

---

## 🤝 Contributing

Contributions are welcome! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for:
- Code quality standards
- Build and test instructions
- Pull request guidelines
- Analyzer configuration details

---

## 🙏 Acknowledgments

- [Dapper](https://github.com/DapperLib/Dapper) - High-performance micro-ORM for ADO.NET
- [Wolfgang.Etl.Abstractions](https://github.com/Chris-Wolfgang/ETL-Abstractions) - Base classes for the ETL framework
- Static analysis powered by Roslyn, Roslynator, Meziantou, and SonarAnalyzer
- Documentation generated with [DocFX](https://dotnet.github.io/docfx/)
