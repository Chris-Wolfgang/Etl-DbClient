# GC / allocation profile

BDN benchmarks (see [`benchmarks/`](../benchmarks/)) measure single-method micro-perf. This workflow measures the **sustained-load** metrics — the ones that only surface after minutes of continuous ETL traffic:

- gen0 → gen1 → gen2 promotion rates
- Large Object Heap (LOH) growth and pinning
- Finalizer queue depth
- Thread-pool starvation events
- Working-set growth vs allocated bytes

## What runs

`.github/workflows/gc-profile.yaml` runs on **workflow_dispatch** and on a weekly Sunday 07:00 UTC schedule (Stryker runs at 06:00, so the two don't fight for the same GitHub-hosted runner pool).

The workload — `tools/GcProfileWorkload/` — extracts + loads against in-memory SQLite in a loop for 10 minutes (configurable). ServerGC + concurrent GC are enabled to match a realistic long-running ETL runtime. `dotnet-counters` attaches by PID and samples the `System.Runtime` counter set every 5 seconds; results write to a CSV artifact.

## Gate mode: informational

Every scheduled run uploads:

- `reports/workload.log` — stdout from the workload (per-cycle progress + final GC counts).
- `reports/counters.csv` — full CSV of every sampled runtime counter (heap sizes per generation, GC-count-per-gen, working-set, thread-pool queue depth, exceptions/sec, etc.).

**No regression gate today.** A meaningful gate needs a stable baseline, which the first several runs need to establish (variance between runs is higher for GC metrics than for BDN benchmarks — differently-timed collections dominate short samples). Follow-up: once we have ~10 baseline runs, add a threshold gate (e.g., "gen2 collections/minute > 2× rolling median → fail scheduled run + open a maintenance issue").

## Reading a report

The counter CSV has columns like:

```
Timestamp,Metadata,Provider,Name,Value
2026-07-16T07:00:15Z,,System.Runtime,gc-heap-size,42.1
2026-07-16T07:00:15Z,,System.Runtime,gen-0-gc-count,3.0
...
```

Metrics worth watching:

- **`gc-heap-size`** — total heap MB. Should stabilise, not grow linearly. Linear growth = leak.
- **`gen-2-gc-count` / `loh-size`** — high gen2 or LOH growth = large-object pinning or long-lived allocations. For an ETL library, we expect near-zero gen2.
- **`threadpool-queue-length`** — should stay near zero. A rising queue = the workload is blocking a thread-pool thread somewhere.
- **`allocation-rate`** — MB/sec allocated. High is fine for an ETL workload; look at the workload's own summary for allocations-per-row instead.

Cross-reference with `workload.log`'s per-cycle line — it prints `gen0`/`gen1`/`gen2` counts each 10 cycles alongside `total-alloc` — same metrics, different sampling.

## Ratchet policy

Same shape as [MUTATION-TESTING.md](MUTATION-TESTING.md)'s ratchet:

- Baseline: whatever the first stable run gives.
- Improvement: tighter thresholds.
- Regression: never quietly relaxed — flag in review.

Refs [#142](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/142).
