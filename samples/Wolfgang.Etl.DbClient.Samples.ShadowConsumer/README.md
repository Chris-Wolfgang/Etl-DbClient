# Shadow-testing consumer

Realistic workload for [#130](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/130).
Represents a consumer that uses the library the way production code does —
paged extract + transform + batched load — so a nightly shadow run can
detect regressions the synthetic BDN suite misses.

## Scenario

Seeds a 100,000-row SQLite source table, then extracts it in 100 pages of
1,000 rows via `DbExtractor<T>`'s `ServerOffset` / `ServerLimit`, projects
each row through a small transform, and reloads the projection into a
destination table via `DbLoader<T>` with `InsertBatchSize = 100`.

Every knob a production consumer would touch is exercised: server-side
paging, per-page extractor instantiation, batched inserts, transaction
management, per-row Dapper materialization, `IAsyncEnumerable<T>` streaming.

## Metrics

One JSON line is written to **stdout** at the end of the run; human-readable
progress goes to **stderr**. The shadow-testing workflow (follow-up PR)
parses the stdout line and compares against a baseline recorded on
gh-pages.

```json
{
  "schemaVersion": 1,
  "runtime": "10.0.10",
  "serverGc": true,
  "scenario": "paged-etl",
  "rows": 100000,
  "loaded": 100000,
  "elapsedMs": 3297,
  "rowsPerSecond": 30330,
  "allocatedBytes": 307479216,
  "bytesPerRow": 3074,
  "gen0Collections": 89,
  "gen1Collections": 1,
  "gen2Collections": 0,
  "peakWorkingSetBytes": 58466304
}
```

Fields tracked by the regression gate (thresholds set by the follow-up PR
that adds the gate):
- `elapsedMs` — total wall-clock time.
- `rowsPerSecond` — throughput.
- `allocatedBytes` — cumulative GC allocations for the whole ETL loop.
- `bytesPerRow` — normalised allocation cost.
- `gen0Collections` / `gen1Collections` / `gen2Collections` — GC pressure
  by generation. gen2 is the most sensitive to allocation regressions.

Not tracked: `peakWorkingSetBytes`. Useful eyeballing but too noisy
across CI runners for a threshold gate — reported for context only.

## Why SQLite in-memory

Deterministic across runners. The gate compares **metric deltas**, not
absolute values, so the SQLite path is a valid stand-in for real ETL
shapes even though production consumers hit SQL Server / Postgres / etc.
A real-DB replay against Testcontainers would add ~30s of container
warmup per matrix leg for no additional signal — the code paths inside
DbClient are the same regardless of driver.

## Running locally

```bash
dotnet run --project samples/Wolfgang.Etl.DbClient.Samples.ShadowConsumer -c Release
```

Typical local run: 3-5 seconds wall-clock on a warm .NET 10 runtime,
~30k rows/second, ~3 KB allocated per row, ~90 gen0 collections.

## Follow-up work (separate PRs)

- **`shadow.yaml` workflow** — nightly + `workflow_dispatch`, matrix
  across the shipping TFMs, runs this project + parses JSON.
- **Baseline capture on gh-pages** — one JSON per runner+TFM combo,
  updated on `push` to main.
- **Regression gate + auto-issue** — fails the shadow run when a metric
  regresses beyond a per-metric threshold. Same auto-issue pattern as
  the fuzz-failure workflow (#275).
