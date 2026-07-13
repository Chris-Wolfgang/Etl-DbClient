# ADR-0001: Source generator ships in the runtime NuGet, not as a separate package

- **Status**: Accepted
- **Date**: 2026-07-01
- **Deciders**: @Chris-Wolfgang
- **Tags**: `packaging`, `source-generators`

## Context

v0.5.0 introduced `Wolfgang.Etl.DbClient.SourceGenerator` — an `IIncrementalGenerator` targeting `netstandard2.0` that emits per-`[DbTable]` `Insert` constants and `Bind(DynamicParameters, T)` helpers. Two packaging options were on the table:

1. **Separate NuGet** (`Wolfgang.Etl.DbClient.Generators`) — the original design in [#23](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/23).
2. **Pack into the runtime NuGet** under `analyzers/dotnet/cs/` so it flows transparently to every consumer of `Wolfgang.Etl.DbClient`.

Separate packaging keeps the runtime lightweight and lets consumers opt in. Bundled packaging means every consumer already has the generator — no discovery step, no "install this second package for AOT" documentation, no version-skew between the two packages.

## Decision

Ship the generator inside the main `Wolfgang.Etl.DbClient` NuGet under `analyzers/dotnet/cs/`. Wired via a `PackSourceGenerator` MSBuild target in `src/Wolfgang.Etl.DbClient/Wolfgang.Etl.DbClient.csproj` that copies the generator's `bin/Release/netstandard2.0` output into the runtime package's analyzers folder at pack time.

The source-gen project is referenced by the runtime csproj with `OutputItemType="Analyzer" ReferenceOutputAssembly="false"` — the analyzer runs in-process on the consumer's compiler; the assembly is not part of the runtime's public API.

## Consequences

- **Positive**:
  - Zero-friction adoption. `dotnet add package Wolfgang.Etl.DbClient` gives the consumer both the runtime and the generator; `[DbTable]` "just works" without further steps.
  - No version-skew — the generator ships lock-step with the attributes it consumes and the runtime code that will one day integrate its output.
  - AOT / trim readiness is a single-package promise.
- **Negative**:
  - Every consumer of the runtime pays for the generator DLL in their `.nupkg` closure — small (~50 KB) but not zero.
  - Consumers who want *only* the runtime (unlikely) cannot skip the generator.
  - `ProjectReference` with `ReferenceOutputAssembly="false"` does not force a solution-level build of the source-gen csproj. This bit us at v0.5.0 release: the generator wasn't in `.slnx`, so `dotnet build --no-restore -c Release` at the solution level skipped it, and `PackSourceGenerator` failed to find the output. Recovered via [#222](https://github.com/Chris-Wolfgang/Etl-DbClient/pull/222) (add to slnx) and hardened by [#225](https://github.com/Chris-Wolfgang/Etl-DbClient/pull/225) (pr.yaml solution-consistency guardrail).
- **Neutral**:
  - Consumers debugging generator behavior will see the analyzer under `~/.nuget/packages/wolfgang.etl.dbclient/<ver>/analyzers/dotnet/cs/`.

## Alternatives considered

- **Separate NuGet `Wolfgang.Etl.DbClient.Generators`** — cleaner separation, higher friction. Rejected: users don't want a second package for something that's baked into how they annotate their record types.
- **Optional MSBuild property to skip the analyzer** — deferred. If a consumer ever complains about the analyzer footprint, revisit; not worth adding the knob preemptively.

## Notes

- Introduced by [#200](https://github.com/Chris-Wolfgang/Etl-DbClient/pull/200) (in v0.5.0).
- Release-time hazard: [#222](https://github.com/Chris-Wolfgang/Etl-DbClient/pull/222) (slnx fix), [#225](https://github.com/Chris-Wolfgang/Etl-DbClient/pull/225) (guardrail).
- Related to [#23](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/23) — the source-generator umbrella issue.
