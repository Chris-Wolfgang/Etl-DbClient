# Fluent EtlPipeline chain

The `DbExtractor<T>` and `DbLoader<T>` types plug into the generic
[`EtlPipeline`](xref:Wolfgang.Etl.Abstractions.EtlPipeline) framework via a set
of class-named factory / terminator extensions. That lets a single fluent chain
read from a database, thread rows through operators from
`Wolfgang.Etl.Transformers`, and write to any sink shipped by the ETL family
— another database, JSON, CSV, `Console`, an arbitrary `LoaderBase<T, TProgress>`,
or an `IAsyncEnumerable<T>` escape hatch.

Added in **v0.7.0**. Requires `Wolfgang.Etl.Abstractions` ≥ 0.16.0.

## The pattern

Every source and sink hangs off the same `EtlPipeline.Create()` seed, so every
pipeline reads the same way regardless of the format at either end:

```csharp
using Wolfgang.Etl.Abstractions;
using Wolfgang.Etl.DbClient;

await EtlPipeline
    .Create()
    .DbExtractor<Person>(connection, "SELECT * FROM People WHERE Active = 1")
    .DbLoader<Person>(destConnection, "INSERT INTO ActivePeople (Id, Name) VALUES (@Id, @Name)")
    .RunAsync();
```

`DbExtractor<T>` returns an
[`IDbExtractorBuilder<T>`](xref:Wolfgang.Etl.DbClient.IDbExtractorBuilder`1);
`DbLoader<T>` returns an
[`IDbLoaderBuilder<T>`](xref:Wolfgang.Etl.DbClient.IDbLoaderBuilder`1). Both are
just `IEtlPipeline<T>` / `IEtlPipelineSink` with extra fluent setters — on the
first operator or terminator call they transition seamlessly into the plain
pipeline interfaces.

## Extractor knobs

Every configurable property on `DbExtractor<T>` has a matching builder setter:

| Setter | Underlying property | Purpose |
|---|---|---|
| `CommandType(CommandType)` | `DbExtractor<T>.CommandType` | `Text` (default) or `StoredProcedure`. |
| `ManageConnection(bool)` | `DbExtractor<T>.ManageConnection` | When `true`, extractor opens/closes the connection. |
| `Parameters(DynamicParameters)` | `DbExtractor<T>.Parameters` | Dapper parameter bag; overrides constructor `Parameters`. |
| `ServerOffset(long?)` | `DbExtractor<T>.ServerOffset` | Row offset for server-side paging. |
| `ServerLimit(long?)` | `DbExtractor<T>.ServerLimit` | Page size in rows. Setting this switches paging on. |
| `PagingClauseTemplate(string?)` | `DbExtractor<T>.PagingClauseTemplate` | Dialect-specific SQL appended when paging is active. Defaults to `PagingClauseTemplates.None` — it must be set, or paging throws. |
| `TotalCountQuery(Func<CancellationToken, Task<int>>)` | `DbExtractor<T>.TotalCountQuery` | Snapshot the total row count for progress reporting. |

Server-side paging is switched on by `ServerLimit`; `ServerOffset` defaults to `0`
when not set. Setting `ServerOffset` without `ServerLimit` throws, because no page
size can be inferred.

## Loader knobs

| Setter | Underlying property | Purpose |
|---|---|---|
| `CommandType(CommandType)` | `DbLoader<T>.CommandType` | `Text` (default) or `StoredProcedure`. |
| `ManageConnection(bool)` | `DbLoader<T>.ManageConnection` | When `true`, loader opens/closes the connection. |
| `ErrorHandling(RowErrorHandling)` | `DbLoader<T>.ErrorHandling` | `Abort` (default) or `Skip` (per-record path only). |

Dry-run behaviour on `DbLoader<T>` flows through the shared `ISupportDryRun`
pipeline hook — no builder-specific setter, consistent with the rest of the ETL
family.

## Connection ownership

The pipeline never overrides ownership. When `ManageConnection` is `false` (the
default) the caller owns the connection lifetime, so the caller is responsible
for opening the connection before `RunAsync` and disposing it after. When
`ManageConnection` is `true` the extractor / loader opens and closes it. There
is no factory-created file handle here (unlike `JsonLineLoader<T>(this
IEtlPipeline<T>, string path)`), so no `DisposingOwned`-style resource handoff
applies.

## Cross-package chains

Any format package that ships EtlPipeline extensions composes with DbClient:

```csharp
// SQL query → JSONL file (needs Wolfgang.Etl.Json)
await EtlPipeline
    .Create()
    .DbExtractor<Person>(conn, "SELECT * FROM People WHERE Active = 1")
    .JsonLineLoader<Person>("people.jsonl")
    .RunAsync();

// JSONL file → database table
await EtlPipeline
    .Create()
    .JsonLineExtractor<Order>("orders.jsonl")
    .DbLoader<Order>(conn, "INSERT INTO Orders (Id, Total) VALUES (@Id, @Total)")
    .RunAsync();
```

## Reusing a pre-configured extractor or loader

Both `DbExtractor` and `DbLoader` extensions have overloads that accept an
existing instance instead of connection + SQL. Setter calls on the returned
builder mutate the caller's instance — useful when the extractor / loader is
constructed elsewhere (e.g. dependency injection) and needs a chain-time
override:

```csharp
var extractor = serviceProvider.GetRequiredService<DbExtractor<Person>>();

await EtlPipeline
    .Create()
    .DbExtractor(extractor)           // reuse the DI-registered instance
    .PagingClauseTemplate(PagingClauseTemplates.PostgreSql)
    .ServerOffset(0)                  // mutates `extractor.ServerOffset`
    .ServerLimit(500)
    .DbLoader<Person>(destConn, insertSql)
    .RunAsync();
```

## Server-side paging

```csharp
await EtlPipeline
    .Create()
    .DbExtractor<Invoice>(conn, "SELECT * FROM Invoices WHERE Status = @Status")
    .Parameters(new DynamicParameters(new { Status = "paid" }))
    .PagingClauseTemplate(PagingClauseTemplates.PostgreSql)
    .ServerOffset(1000)
    .ServerLimit(500)
    .DbLoader<Invoice>(destConn, "INSERT INTO PaidInvoicesPage2 ...")
    .RunAsync();
```

When paging is active, the extractor appends `PagingClauseTemplate` to the command
text and adds `@PageOffset` / `@PageLimit` to the parameter set.

`PagingClauseTemplate` **must be set** — it defaults to `PagingClauseTemplates.None`.
Paging syntax is dialect-specific and no portable form exists, so the library does
not guess one; activating paging without a template throws
`InvalidOperationException` rather than emitting SQL only some engines accept.
Use a preset, or supply your own clause referencing `@PageOffset` and `@PageLimit`:

```csharp
await EtlPipeline
    .Create()
    .DbExtractor<Invoice>(conn, "SELECT * FROM Invoices ORDER BY Id")
    .ServerOffset(0)
    .ServerLimit(500)
    .PagingClauseTemplate(PagingClauseTemplates.SqlServer)
    .DbLoader<Invoice>(destConn, "...")
    .RunAsync();
```

## Error handling

`RowErrorHandling.Skip` on the loader silently drops the failing row and
continues (per-record path only, requires `InsertBatchSize = 1`). Useful for
imports where a per-row PK conflict or check-constraint violation shouldn't
tear down the whole run:

```csharp
var loader = new DbLoader<Widget>(destConn, insertSql) { InsertBatchSize = 1 };

await EtlPipeline
    .Create()
    .DbExtractor<Widget>(srcConn, "SELECT * FROM StagingWidgets")
    .DbLoader(loader)
    .ErrorHandling(RowErrorHandling.Skip)
    .RunAsync();
```

## Escape hatch: enumerate without a sink

`AsAsyncEnumerable()` drops down to the raw stream, useful when the caller
wants to apply `System.Linq.Async` operators the pipeline doesn't ship, or when
the row processing is not a "load somewhere" pattern at all:

```csharp
using System.Linq;

var totalPaid = await EtlPipeline
    .Create()
    .DbExtractor<Invoice>(conn, "SELECT * FROM Invoices WHERE Status = 'paid'")
    .AsAsyncEnumerable()
    .SumAsync(i => i.Total);
```

## Progress reporting

`RunAsync` accepts an optional `IProgress<EtlPipelineProgress>`:

```csharp
var progress = new Progress<EtlPipelineProgress>(p =>
    Console.WriteLine($"loaded {p.LoadedItemCount}"));

await EtlPipeline
    .Create()
    .DbExtractor<Person>(conn, sql)
    .DbLoader<Person>(destConn, insertSql)
    .RunAsync(progress);
```

For extractor-side totals, wire
[`TotalCountQuery`](xref:Wolfgang.Etl.DbClient.IDbExtractorBuilder`1.TotalCountQuery*)
so the extractor can populate `EtlPipelineProgress.TotalRecords`.

## See also

- [`EtlPipeline`](xref:Wolfgang.Etl.Abstractions.EtlPipeline) — the framework
  entry point in `Wolfgang.Etl.Abstractions`.
- [`IEtlPipeline<T>`](xref:Wolfgang.Etl.Abstractions.IEtlPipeline`1) — the core
  chain interface.
- The [Example.EtlPipeline](https://github.com/Chris-Wolfgang/Etl-DbClient/tree/main/examples/Wolfgang.Etl.DbClient.Example.EtlPipeline)
  runnable project.
