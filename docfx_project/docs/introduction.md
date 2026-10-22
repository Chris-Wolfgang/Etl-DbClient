# Introduction

Welcome to Wolfgang.Etl.DbClient!

## Overview

Extractors and Loaders for working with SQL databases using ADO.NET.

`Wolfgang.Etl.DbClient` implements the `Wolfgang.Etl.Abstractions` extractor and loader
contracts over any ADO.NET provider (`DbConnection` / `DbCommand`). `DbExtractor<TRecord>`
streams query results as an `IAsyncEnumerable<TRecord>`; `DbLoader<TRecord>` writes an
`IAsyncEnumerable<TRecord>` back, with SQL you supply (any statement or a stored procedure)
or an `INSERT` / `UPDATE` generated from the record type (`WriteMode`).
Mapping is by convention or by `[Table]` / `[Column]` / `[Key]` attributes, with Dapper doing
the row materialization and parameter binding.

The integration suite runs against SQL Server, PostgreSQL, MySQL, MariaDB, CockroachDB and
SQLite.

## Key Features

- **Streaming extraction** - rows are read one at a time from a `DbDataReader`, never buffered.
- **Parameters** - named input parameters from a dictionary, plus output and input/output
  parameters through `EtlParameter<T>`.
- **Server-side paging** - `ServerLimit` / `ServerOffset` with dialect presets in
  `PagingClauseTemplates` (SQL Server, PostgreSQL, MySQL, SQLite, Oracle, Db2).
- **Batched loading** - `BatchSize` (N statements per round trip), `InsertBatchSize`
  (one multi-row `INSERT ... VALUES`) and `BatchCommitSize` (commit every N rows).
- **Error handling** - `RowErrorHandling.Skip` with a `RowFailed` event and an error
  threshold, or abort on the first failure (the default).
- **Dry run** - `IsDryRun` exercises the pipeline without writing.
- **Schema validation** - `DbSchemaValidator` and `ValidateSchemaOnStart` check the mapped
  table and columns before any row moves.
- **Source generator** - `[DbTable]` generates `Insert`, `Select`, `Update`, `Delete` SQL and a
  reflection-free parameter binder at compile time.
- **Progress reporting** - `DbReport` with current, skipped and (optionally) total item counts.
- **Fluent pipeline** - `EtlPipeline.Create().DbExtractor<T>(...)...DbLoader<T>(...)` builders;
  see [Fluent EtlPipeline chain](etl-pipeline.md).

## Getting Help

If you need help with Wolfgang.Etl.DbClient, please:

- Check the [Getting Started](getting-started.md) guide
- Review the [API Reference](../api/index.md)
- Visit the [GitHub repository](https://github.com/Chris-Wolfgang/Etl-DbClient)
- Open an issue on [GitHub Issues](https://github.com/Chris-Wolfgang/Etl-DbClient/issues)
