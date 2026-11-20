# ADR-0005: Enforce async-only I/O with `BannedSymbols.txt`

- **Status**: Accepted
- **Date**: 2026-11-17
- **Deciders**: @Chris-Wolfgang
- **Tags**: `async`, `analyzers`, `ci`

## Context

`DbExtractor` and `DbLoader` are stages in an `IAsyncEnumerable<T>` pipeline, and every operation they perform is a database round trip. A blocking call on that path (`Task.Result`, `Thread.Sleep`, a synchronous stream read) holds a thread-pool thread for the length of the I/O and can deadlock a consumer that runs under a single-threaded synchronization context. Such a call compiles without complaint and is easy to miss in review.

The repository already carries the mechanism: a `BannedSymbols.txt` at the root, read by `Microsoft.CodeAnalysis.BannedApiAnalyzers`. On the date of this ADR the list had the same 65 entries in all eight ETL repositories. What was missing was a record of the decision and of where it stops, which for a database library is not where a reader would guess.

## Decision

We enforce the async-only rule with the banned-API analyzer instead of relying on review. `BannedSymbols.txt` bans `Task.Wait`, `Task.WaitAll`, `Task.WaitAny`, `Task<T>.Result`, `GetAwaiter().GetResult()`, `Thread.Sleep`, the synchronous `File` helpers, `Stream.Read` / `Write` / `Flush` / `CopyTo`, `Parallel.For` / `ForEach` and the blocking `Console` reads, each with its replacement in the message. `.editorconfig` sets `RS0030` to `error` for the shipped code and to `none` for tests, benchmarks and examples. The shipped code has no `RS0030` suppression.

The list does not contain the synchronous ADO.NET members (`DbConnection.Open`, `DbCommand.ExecuteReader`, `DbTransaction.Commit` and so on). The library uses the asynchronous form of those wherever the target framework has one, and the analyzer does not check that.

## Consequences

- **Positive**: a direct call to a banned member cannot merge into shipped code, and the build error names the replacement.
- **Negative**: the rule does not cover the database calls themselves. Two places use synchronous ADO.NET on purpose:
  - On the targets below `net5.0`, `DbConnection` and `DbTransaction` have no `BeginTransactionAsync`, `CommitAsync`, `RollbackAsync`, `CloseAsync` or `DisposeAsync`. The code calls the synchronous member there, behind `#if NET5_0_OR_GREATER`.
  - `DbSchemaValidator.Validate` is a public synchronous method that opens the connection and runs a reader synchronously. `ValidateAsync` is its asynchronous companion.
- **Negative**: nothing but review stops a new synchronous ADO.NET call on a target that has the asynchronous form.
- **Neutral**: `BannedSymbols.txt` is a protected file, so a change to the list goes in its own pull request. Tests, benchmarks and examples are exempt.

## Alternatives considered

- Rely on code review and AsyncFixer: AsyncFixer catches some patterns, not all of the banned APIs, and review misses them under time pressure.
- Add the synchronous ADO.NET members to the list: not done. The list is shared across the ETL repositories, and this library needs those members on the older targets and in `DbSchemaValidator.Validate`, so every use would need a suppression.

## Notes

- Sibling records of the same rule: ETL-SqlBulkCopy ADR 0003, ETL-Transformers ADR-0001.
- Recorded after the fact: the rule was already in force when this ADR was written.
