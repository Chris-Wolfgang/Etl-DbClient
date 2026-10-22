# Copilot Coding Agent Instructions

## Repository Summary

`Wolfgang.Etl.DbClient` is a NuGet library (package id `Wolfgang.Etl.DbClient`) that implements
the `Wolfgang.Etl.Abstractions` extractor and loader contracts over ADO.NET:
`DbExtractor<TRecord>` streams query results as `IAsyncEnumerable<TRecord>`, and
`DbLoader<TRecord>` writes records back. Dapper does row materialization and parameter binding.
A Roslyn source generator (`[DbTable]`) emits SQL constants and a reflection-free binder.

- **Primary language**: C#
- **Library TFMs**: net462, net472, net48, net481, netstandard2.0, net5.0-net10.0
  (`src/Wolfgang.Etl.DbClient/Wolfgang.Etl.DbClient.csproj`)
- **Code conventions**: see `CONTRIBUTING.md` (Allman braces, three blank lines between members, Allman-style multi-line argument lists,
  file-scoped namespaces, `MethodUnderTest_when_condition_expected_result` test names).

## Layout

```
src/Wolfgang.Etl.DbClient/                   library
src/Wolfgang.Etl.DbClient.SourceGenerator/   [DbTable] source generator
tests/Wolfgang.Etl.DbClient.Tests.Unit/      unit tests (all TFMs, SQLite in-memory)
tests/Wolfgang.Etl.DbClient.Tests.Integration/  Testcontainers suites per engine (needs Docker, Linux)
tests/...Tests.Concurrency, .DocExamples, .Fuzz, .Snapshots, .SourceLink
benchmarks/Wolfgang.Etl.DbClient.Benchmarks/ BenchmarkDotNet
examples/, samples/, tools/                  runnable examples, shadow consumer, GC workload
docfx_project/                               docs site source (output: _site)
docs/                                        hand-written policy docs and ADRs
changelog/unreleased/                        one changelog fragment per PR (scripts/changelog.ps1)
```

## Build and Validation

- Local gate: `pwsh ./scripts/build-pr.ps1` (build, every test project, coverage, DevSkim,
  gitleaks). `-SkipSecurity`, `-SkipCoverage` and `-SkipTests` narrow it.
- Plain build/test: `dotnet build -c Release`, then `dotnet test <project> -c Release`.
- Warnings are errors. Do not suppress an analyzer finding unless it is a known false positive.
- **Coverage gates** (`.github/workflows/pr.yaml`): production code `CODECOV_MINIMUM`, test
  assemblies `CODECOV_TEST_MINIMUM` (fleet policy: tests 100 %, production 95 %). Line coverage only.
- Integration tests need Docker with Linux containers; they run on the Linux CI stage only.
- Public API changes must update `PublicAPI.Unshipped.txt` (PublicApiAnalyzers).
- Every PR needs a changelog fragment in `changelog/unreleased/` (or the `no-changelog` label).

## Protected files

`.editorconfig`, `Directory.Build.*`, `BannedSymbols.txt`, `*.globalconfig`, `*.ruleset` and
`.github/workflows/*` are guarded by the `Detect .NET Projects` check: change them in their own
PR, separate from code changes.

## Agent Guidelines

- Prefer the options-record constructors (`DbExtractorOptions`, `DbLoaderOptions`) over the
  obsolete property setters in new code, examples and docs.
- All I/O is async; the banned-API analyzer rejects synchronous I/O.
- Keep XML doc comments accurate: public members carry `///` docs, and examples in them must
  compile without obsolete APIs.
