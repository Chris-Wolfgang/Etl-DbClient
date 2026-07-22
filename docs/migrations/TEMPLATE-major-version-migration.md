# Migrating from `Wolfgang.Etl.DbClient` X.y → `Z.0`

- **Previous major**: `X.y` (last minor: `X.y.z`, released YYYY-MM-DD)
- **This major**: `Z.0.0`, released YYYY-MM-DD
- **Effort estimate**: <one-line — "mechanical", "search + replace + review", "requires re-benchmarking", etc.>

## TL;DR

One paragraph. If a consumer reads only this section, what do they need to do?

## Why the major bump

Motivating change — the one or two things that couldn't be delivered without a breaking change. Link to the driving issue(s).

## Deprecation timeline

- **Deprecated in**: `X.y.z` (YYYY-MM-DD) — marked `[Obsolete]`, warning-only.
- **Removed in**: `Z.0.0` (YYYY-MM-DD).
- **Support window on `X.y`**: <"security patches only through YYYY-MM-DD" | "no further updates">.

## Breaking-change inventory

Group by category. Each entry has: **before**, **after**, **why**, **fix pattern**.

### API removals

#### `TypeName.MemberName`

- **Before**
  ```csharp
  <old usage>
  ```
- **After**
  ```csharp
  <replacement usage>
  ```
- **Why**: <one line — what forced this>.
- **Fix pattern**: search-and-replace | manual review required | scripted (link a script if applicable).

### API renames

#### `OldName` → `NewName`

- **Before / After / Why / Fix pattern** — same shape as above.

### Behavioral changes (no signature change)

#### `TypeName.MemberName` — `<one-line summary of behavior change>`

- **Before**: <old semantics>.
- **After**: <new semantics>.
- **Why**: <what forced this>.
- **How to detect** — what a consumer test would show if the change affects them.
- **Fix pattern**: how to keep the old behavior (if opt-in) or adapt.

### Configuration / defaults

Options whose default changed. Any consumer relying on the old default needs to set the option explicitly.

### Dependencies

- **Runtime dependencies**: what bumped, what got dropped, what got added.
- **Minimum TFM**: e.g. dropped `net462` / `netstandard2.0` — link the deprecation ADR.

## Fix-your-code cheat sheet

Copy-pasteable snippets for the ~5 most common consumer patterns. Keep this section under one screen.

```csharp
// OLD (X.y)


// NEW (Z.0)

```

## After the upgrade — verify

- [ ] Build succeeds with `TreatWarningsAsErrors`.
- [ ] Test suite passes.
- [ ] `<any release-specific verification — allocation regression, behavior sample, integration test row>`.
- [ ] Update your own downstream migration guide if you're a library.

## Rollback

If the upgrade turned up a blocker, pin back to the last `X.y.z` and open an issue on this repo with a repro. Rollback is safe — `X.y.z` remains listed on NuGet indefinitely; a `Z.0` upgrade is not a one-way door.

## References

- Release notes: <link to the GitHub Release>.
- CHANGELOG entry: <link>.
- Driving issues: `#NNN`, `#NNN`.
- Related ADRs: [ADR-NNNN](../adr/NNNN-title.md).
