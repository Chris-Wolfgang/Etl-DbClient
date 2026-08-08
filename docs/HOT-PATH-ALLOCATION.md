# Hot-path allocation policy

**Status:** snapshot. No zero-allocation hot paths are asserted for
`Wolfgang.Etl.DbClient`.

This document exists because [#147](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/147)
proposes zero-allocation guards on hot paths across the fleet. That
proposal has an explicit escape clause for libraries where no method
is intended to be zero-alloc: *"snapshot doc explaining why instead
of half-implemented enforcement."* This is that snapshot.

## Why DbClient is not a zero-alloc library

Every non-trivial code path in `Wolfgang.Etl.DbClient` goes through
one of the following, each of which allocates by design:

- **Dapper materialization.** `DbExtractor<TRecord>.ExtractAsync`
  reads rows via `conn.QueryUnbufferedAsync<TRecord>`. Every row
  materializes a new `TRecord` instance (unavoidable — the caller
  needs one to consume) plus Dapper's per-row plumbing
  (`DataReader.GetValue` boxes value types, `IDataRecord` field
  arrays, etc.).
- **ADO.NET driver internals.** `DbCommand`, `DbDataReader`,
  `DbParameterCollection`, and provider-specific parameter objects
  are allocated per call by the underlying driver
  (`Microsoft.Data.SqlClient`, `Npgsql`, `Microsoft.Data.Sqlite`,
  `Oracle.ManagedDataAccess`, etc.). We do not own that surface and
  cannot elide the allocations without abandoning provider
  neutrality.
- **`IAsyncEnumerable<TRecord>` state machines.** `ExtractAsync`
  and `LoadAsync` are compiler-generated async iterators. On
  netstandard2.0 and .NET Framework the state machine boxes on
  first `MoveNextAsync`; on .NET 5+ some paths pool but not all.
  Either way, per-call overhead is a documented cost of the
  contract, not a bug to eliminate.
- **Dapper `DynamicParameters`.** Parameter passing goes through
  Dapper's parameter builder, which allocates a wrapper per
  invocation.
- **Reflection-driven mapping.** `ColumnAttributeTypeMapper`
  populates a per-`TRecord` `TypeMap` at cold start. Warm-path
  lookups are dictionary reads (no allocation), but cold start is
  reflection-heavy and intentionally cached rather than
  hand-optimized.

## What we do care about

DbClient's performance story is about *throughput* per round-trip,
not per-call allocation:

- **Batching.** `DbLoader<TRecord>.BatchSize` collapses N per-row
  `INSERT`s into a single multi-row `INSERT … VALUES (…), (…), …`
  statement. The single biggest single-line win available without
  provider-specific bulk APIs.
- **Server-side paging.** `DbExtractor<TRecord>` streams via
  `QueryUnbufferedAsync` instead of `Query`, so a 10M-row result
  does not first materialize as a `List<T>` in memory.
- **Connection ownership modes.** `ManageConnection` and the
  factory ctor path let the caller choose between "hold the
  connection open for the whole extract/load" and "open/close
  around each call" without dictating a lifetime.

The [BenchmarkDotNet suite][bdn] measures these — insert throughput
under batch sizes 1/10/100/1000, extract throughput under different
row counts, etc. Regressions there DO block a release. That is the
right shape of enforcement for this library.

[bdn]: ../benchmarks/Wolfgang.Etl.DbClient.Benchmarks/

## When to revisit

If a future public API is added that *is* intended to be zero-alloc
— for example, a `bool DbSchemaValidator.HasTable` guard-clause
predicate that could reasonably be called per-message on a hot
pipeline — flip this doc from "snapshot" to "opt-in enforcement"
and add a `[MemoryDiagnoser]` BenchmarkDotNet job that asserts the
allocation delta on that specific call. Do not add half-implemented
enforcement across the existing surface just to have something
covered — that is the failure mode #147 explicitly warns against.

## References

- [#147](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/147)
  — fleet-wide thorough-review item that spawned this doc.
- [`benchmarks/`](../benchmarks/) — BenchmarkDotNet suite that
  guards the throughput-facing perf story.
- [`docs/GC-PROFILE.md`](GC-PROFILE.md) — sustained-load GC
  profiling under real ETL workloads.
