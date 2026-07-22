# Mutation testing policy

Stryker.NET mutation-tests `src/Wolfgang.Etl.DbClient/` against the unit test suite. The runner mutates every line of production code (swap `<` → `<=`, drop `return`, replace `true` with `false`, etc.) and reports how many of those mutants the test suite catches. A mutant that runs and returns wrong output but no test fails is a **survived mutant** — a coverage gap the line-coverage number can't see.

## What runs

`.github/workflows/stryker.yaml` triggers on `workflow_dispatch` + weekly Sunday 06:00 UTC. Config lives at `stryker-config.json` at the repo root — target `net10.0`, unit-test project as the test source, `Standard` mutation level (adds arithmetic + boolean-logic mutants beyond the default `Basic` set). Excluded: source-generator polyfill (`IsExternalInit.cs`), assembly attributes, global usings.

## Thresholds

Set in `stryker-config.json`:

- `high: 80` — score at or above this is "good". Reporter shows green.
- `low: 60`  — score at or above this but below `high` is "warning". Reporter shows amber.
- `break: 0` — Stryker exits non-zero if score falls below this. Currently `0` = **informational only**.

**Why break = 0 today**: we haven't run Stryker against the current tree yet, so we don't know the baseline. Setting a break threshold without knowing the baseline either blocks every scheduled run (threshold too high) or is meaningless (threshold too low). First run establishes the floor; subsequent runs can only tighten.

## Ratcheting the floor

The first scheduled run (or manual `workflow_dispatch`) produces the initial mutation score. Once we have that number:

1. Set `break` to a value slightly below it (e.g. baseline is 72% → `break: 65`).
2. Each time we intentionally improve coverage / catch survived mutants, bump `break` upward.
3. `break` is a ratchet — it only goes up, not down. Dropping it silently is a regression indicator to flag in review.

## Investigating survived mutants

The workflow produces `mutation-report.html` under `StrykerOutput/`. It lists every survived mutant with:

- File + line
- Original code
- Mutated code
- The tests that ran through it (all still passed)

For each survivor, decide one of:

- **Add a test** that would catch this mutation — the ideal outcome.
- **Ignore-mutant** in `stryker-config.json`'s `ignore-methods` / `ignore-mutations` — record a one-line rationale (e.g. "defensive-only branch; can't be exercised without mocking the framework").
- **Refactor** to remove the mutable surface if the code is defensive-only and can be deleted.

Do **not** just accept the score dropping. That's what the ratchet is for.

Refs [#135](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/135).
