# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

[Unreleased]: https://github.com/Chris-Wolfgang/Etl-DbClient/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/Chris-Wolfgang/Etl-DbClient/releases/tag/v0.3.0
