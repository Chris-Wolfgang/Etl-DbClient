# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

### Changed

### Deprecated

### Removed

### Fixed

### Security

## [0.7.0] - 2026-07-20

Additive release. New runtime schema-validation type, opt-in
extractor/loader property that wires it in, `[DbKey]` attribute + full
Update/Delete source-generator emit, and a fluent `EtlPipeline` chain
surface over `DbExtractor` / `DbLoader`.

### Added

- **`DbSchemaValidator.Validate<TRecord>` + `ValidateAsync<TRecord>`** —
  provider-agnostic pre-flight schema check via
  `SELECT * FROM <table> WHERE 1 = 0`. Error message names both the
  missing columns AND the columns the table actually has, so
  copy-paste typos surface at the top of a batch instead of mid-loop
  (#20).
- **`DbExtractor<T>.ValidateSchemaOnStart` / `DbLoader<T>.ValidateSchemaOnStart`**
  — opt-in bool that runs `DbSchemaValidator.ValidateAsync` before
  reading / writing the first row. Default `false`; adds a single
  zero-row round-trip when enabled (#20).
- **`[DbKey]` attribute + source-generator `Update` / `Delete` const
  emit** — completes source-generator CRUD (`Insert` / `Select` /
  `Update` / `Delete` + `Bind`). Composite keys preserve declaration
  order in the WHERE clause; `Update` emitted only when the type has
  ≥ 1 `[DbKey]` AND ≥ 1 non-key column; `Delete` emitted only when the
  type has ≥ 1 `[DbKey]` (#23).
- **Fluent `EtlPipeline` chain surface** — `DbExtractor<T>(…)` and
  `DbLoader<T>(…)` extension methods on `EtlPipeline` /
  `IEtlPipeline<T>`, returning `IDbExtractorBuilder<T>` /
  `IDbLoaderBuilder<T>` with fluent setters that mirror every
  configurable property on the underlying extractor / loader
  (`CommandType`, `ManageConnection`, `Parameters`, `ServerOffset`,
  `ServerLimit`, `PagingClauseTemplate`, `TotalCountQuery`,
  `ErrorHandling`). Composes cleanly with sibling ETL packages
  (JSON, CSV, FixedWidth, Transformers). Requires
  `Wolfgang.Etl.Abstractions` 0.16.0 (#280).
- **`docs/HOT-PATH-ALLOCATION.md`** — snapshot explaining why DbClient
  intentionally has no zero-alloc guards (Dapper + ADO.NET +
  `IAsyncEnumerable` state machines allocate by contract) (#147).
- **`docs/etl-pipeline.md`** — full reference for the EtlPipeline
  chain surface + `examples/Wolfgang.Etl.DbClient.Example.EtlPipeline/`
  runnable console app (#280).

### Changed

- Bumped `Wolfgang.Etl.Abstractions` to `0.16.0`.

## [0.6.0] - 2026-07-06

Feature-rich release: dry-run mode, source-generator scaffolding, batching + paging + connection lifecycle knobs, plus a broad InspectCode fix pass.

### Added

- **IsDryRun mode for `DbLoader`** — validate pipelines without writing to the target DB. Requires Abstractions 0.15.0 (#21).
- **`DbLoader.BatchCommitSize`** — chunked transactional commits during long loads (#22).
- **Row-level error handling on `DbLoader`** — continue-on-error / stop-on-error policies with per-row failure callbacks (#24).
- **`DbExtractor.CountAsync()`** convenience method for pre-flight sizing (#32).
- **`ManageConnection` on `DbExtractor` and `DbLoader`** — opt-in library-owned connection lifecycle (#31).
- **`DbExtractor.Parameters` property** — output-parameter support for stored procedures (#27).
- **Server-side paging on `DbExtractor`** — `ServerOffset` + `ServerLimit` + `PagingClauseTemplate` for streaming large tables without buffering (plus optional `TotalCountQuery` for pre-flight sizing) (#33).
- **Multi-row `INSERT` batching on `DbLoader`** — SQL Server / PostgreSQL / MySQL / MariaDB batch-insert paths (#30).
- **Source generator scaffolding** for compile-time SQL generation from `DbTableAttribute` / `DbColumnAttribute` — the generator DLL ships embedded in the main package under `analyzers/dotnet/cs` (#23).

### Changed

- Bumped `Wolfgang.Etl.Abstractions` to `0.15.0` and `Wolfgang.Etl.TestKit.Xunit` to `0.10.0`.
- Silenced non-applicable analyzer rules on the SourceGenerator project (RS1036 / RS2008 / NU1701) — quieter analyzer set for source-gen code (#213).
- Suppressed VSTHRD200 on the `AsAsyncEnumerable` adapter (#213).

### Fixed

- Real-bug findings from the InspectCode audit (#202 follow-up).
- Remaining InspectCode findings via actual source changes rather than suppressions (#202 follow-up).
- Replaced file-scope ReSharper disables with documented JetBrains annotations (#202 follow-up).
## [0.5.0] — robustness + extractor ergonomics + source generator

### Added — DbLoader robustness
- `IsDryRun` ([#21](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/21)) — when `true`, the loader runs the full pipeline (enumerate, evaluate skip/max, increment counters, fire progress) but skips both `ExecuteAsync` call sites (per-row + batched). The DB is not modified. Implements `ISupportDryRun` from Abstractions 0.15.0.
- `ErrorHandling` ([#24](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/24)) — new `RowErrorHandling` enum (`Abort` default, `Skip`). In `Skip` mode the per-row catch fires a new `RowFailed` event (`EventHandler<RowFailedEventArgs<TRecord>>`), advances `CurrentErrorCount`, and continues. `MaxErrorCount` caps the threshold; `OperationCanceledException` always propagates. Per-row path only — batched mode still aborts.
- `BatchCommitSize` ([#22](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/22)) — commit every N successfully-loaded rows in auto-managed-transaction mode. Failures roll back only the current chunk; previously-committed chunks survive. Trades all-or-nothing semantics for resumability + lower undo-log pressure.
- `InsertBatchSize` ([#30](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/30)) — replaces N per-row `INSERT`s with a single multi-row `INSERT … VALUES (…), (…), …` statement per chunk. Requires `CommandText` to end with `VALUES (template)`; properties bound by reflection via case-insensitive name match. Mutually exclusive with `BatchSize > 1`, `IsDryRun`, stored-procedure `CommandType`.

### Added — Extractor ergonomics
- `CountAsync(CancellationToken)` ([#32](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/32)) — runs the configured `TotalCountQuery` (or default) and returns the row count without streaming. Side-effect-free on extractor state.
- `Parameters` (`DynamicParameters?`) ([#27](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/27)) — opt-in override of the dictionary-built parameters. Useful for stored procedures with OUT / INOUT parameters that need explicit `ParameterDirection`. Both data and default-count queries honor the override.
- `ServerOffset` / `ServerLimit` (`long?`) + `PagingClauseTemplate` ([#33](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/33)) — server-side paging via configurable clause template. Default `LIMIT @PageLimit OFFSET @PageOffset` fits SQLite/Postgres/MySQL; SQL Server users set the OFFSET/FETCH form. Engages only when both `ServerOffset` and `ServerLimit` are non-null.

### Added — Connection lifecycle
- `ManageConnection` on both `DbExtractor` and `DbLoader` ([#31](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/31)) — when `true`, opens a closed connection before the first command and **closes (not disposes)** it after. The connection returns to the pool. Already-open connections are left open. Ignored on the owned-connection ctor path (which still disposes).

### Added — Source generator
- New project **`Wolfgang.Etl.DbClient.SourceGenerator`** ([refs #23](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/23)) — `netstandard2.0`, `IIncrementalGenerator`, packed into the runtime NuGet under `analyzers/dotnet/cs` so it ships transparently to consumers.
- New public attributes `[DbTable("name")]` and `[DbColumn("col", Skip = bool)]`.
- For every `partial class`/`partial record` decorated with `[DbTable]`, the generator emits a partial with `public const string Insert = "INSERT INTO … VALUES (…)";` and a reflection-free `public static void Bind(DynamicParameters, T record)` helper. `Update` / `Delete` / `Select` + `DbLoader` / `DbExtractor` wire-up tracked as follow-up.

### Dependency bumps
- `Wolfgang.Etl.Abstractions` 0.13.0 → **0.15.0** — introduces `ISupportDryRun` interface; `Report.TotalItemCount` moved to base (`DbReport` now inherits it instead of declaring locally).
- `Wolfgang.Etl.TestKit` 0.7.0 → **0.9.0**, `Wolfgang.Etl.TestKit.Xunit` 0.6.0 → **0.9.0**.
- `Microsoft.Bcl.AsyncInterfaces` 10.0.5 → **10.0.9** (TestKit 0.9.0 floor).

### CI / release hardening
- **`release.yaml` integration-test gate** ([#206](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/206)) — added a 5×2 RDBMS × TFM matrix (sqlserver / postgres / mysql / mariadb / sqlite × net8.0 / net10.0) + aggregator job between `pack-and-validate` and `publish-nuget`. Closes the gap where a release could ship while the integration suite was red.

### Code quality
- **InspectCode hygiene** ([refs #202](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/202)) — first canonical pass on this repo. Real bug fixes: `AccessToDisposedClosure` in a test (TotalCountQuery captured a `using var conn`), `S127` loop-counter mutation in `ExtractTemplateParamNames`. Code-quality cleanups: redundant `?.` on non-null benchmark `Dispose` receivers, redundant casts/qualifiers/usings, always-true nullable-API checks, `NonReadonlyMemberInGetHashCode` on fixtures (converted to `init`). Reflection-consumed surfaces now carry `[PublicAPI]` (src) or `[UsedImplicitly]` (benchmarks/examples/tests) from `JetBrains.Annotations` to document why ReSharper can't see consumers — replacing the broader `// ReSharper disable` comment approach. `jb inspectcode` clean (0 findings, 0 errors).

### Breaking
- **Removed**: `DbReport.TotalItemCount` (locally declared) — the property is now inherited from `Report` (Abstractions 0.14+ moved it to the base). Callers reading `report.TotalItemCount` continue to work via inheritance lookup; recompilation against Abstractions 0.15+ may be needed for tightly-coupled consumers.

## [0.4.0] — production-readiness knobs

### Added
- `DbExtractor<TRecord>.CommandTimeout` / `DbLoader<TRecord>.CommandTimeout` (`TimeSpan?`) — controls how long each underlying command can run before timing out. `null` (the default) falls back to the ADO.NET provider default (~30 s). Negative values throw `ArgumentOutOfRangeException`. Wired through every Dapper call site (extractor's main query + default count query; loader's per-record and batched `ExecuteAsync`).
- `DbExtractor<TRecord>.CommandType` / `DbLoader<TRecord>.CommandType` (`System.Data.CommandType`) — enables stored-procedure invocation. Default `CommandType.Text` preserves prior behavior. Set to `CommandType.StoredProcedure` and `CommandText` becomes the sproc name; Dapper binds parameters from the POCO properties as usual. Not wired to `DefaultTotalCountQuery` (that path wraps `CommandText` in `SELECT COUNT(*) FROM (...)` which is incompatible with sprocs by construction; supply a custom `TotalCountQuery` instead).
- `DbExtractor<TRecord>(DbProviderFactory, string connectionString, string commandText, ILogger?)` — owned-connection ctor overload. The extractor creates the connection via the supplied `DbProviderFactory`, opens it lazily before the first command, and disposes it when extraction completes (or throws). Saves callers the `using var conn = …; await conn.OpenAsync();` boilerplate for one-off scenarios.
- `DbLoader<TRecord>(DbProviderFactory, string connectionString, string commandText, ILogger?)` — owned-connection ctor overload with the same semantics (open lazily, dispose at end). Defaults to auto-managed transaction.

[Unreleased]: https://github.com/Chris-Wolfgang/Etl-DbClient/compare/v0.6.0...HEAD
[0.6.0]: https://github.com/Chris-Wolfgang/Etl-DbClient/compare/v0.5.0...v0.6.0
[0.5.0]: https://github.com/Chris-Wolfgang/Etl-DbClient/releases/tag/v0.5.0
[0.4.0]: https://github.com/Chris-Wolfgang/Etl-DbClient/releases/tag/v0.4.0

## [0.3.0] — code-review pass + integration-test surface

### Added
- `DbClientOptions.StrictColumnMapping` (default `false`) — opt-in flag that converts unmapped result-set columns from Dapper's silent-drop default into a descriptive `InvalidOperationException` naming the column and target type. Preserves out-of-the-box Dapper behavior by default. Useful for catching `[Column]` typos during development.
- `DbLoader<TRecord>.BatchSize` — new batched-execution knob. Defaults to `1` (one round-trip per record, unchanged behavior); raise it to amortize per-call overhead on networked databases. `SkipItemCount` / `MaximumItemCount` are still honored at per-record granularity.
- Integration-test surface (Testcontainers): SQL Server, PostgreSQL, MySQL, MariaDB, CockroachDB, SQLite across net8.0 and net10.0, gated by the `Integration (all)` aggregator on every PR.
- Per-RDBMS dynamic shields.io status badges + per-RDBMS BenchmarkDotNet charts published to `gh-pages`.

### Changed
- **Major perf win** in `DbCommandBuilder`: per-`Type` cache of reflection results + pre-built SQL strings. BuildSelect / BuildInsert / BuildUpdate went from ~7–8 µs to ~5 ns each (~1500× faster, zero allocations on cache hits).
- **Major perf win** in `ColumnAttributeTypeMapper`: replaced per-column reflection + LINQ scan with a single-pass `Dictionary<string, PropertyInfo>` built once at `Register<T>()`. 6-column lookup went from 15.69 µs to 202 ns (~77× faster).
- `DbExtractor` now takes a defensive copy of the caller's parameter dictionary at construction; cached `DynamicParameters` wrapper for reuse across the data query and the default total-count query.
- `DbLoader.LoadWorkerAsync` split into named caller-managed vs auto-managed transaction helpers for readability.
- Thread-safety hardening across extractor + loader (`Interlocked.CompareExchange` for the progress-timer wiring one-shot; documented single-use contract).
- CI Stage 1 / 2 / 3 path-gated via stub jobs (docs-only PRs skip the multi-TFM matrix while keeping ruleset-required check names green).

### Fixed
- `DbReport` restored the 4-arg constructor as a `[EditorBrowsable(Never)]` binary-compat shim so already-compiled consumers don't hit `MissingMethodException`.

## [0.2.x] and earlier

See git history.

[0.3.0]: https://github.com/Chris-Wolfgang/Etl-DbClient/releases/tag/v0.3.0
