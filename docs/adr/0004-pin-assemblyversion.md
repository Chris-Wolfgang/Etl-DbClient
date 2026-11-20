# ADR-0004: Pin `AssemblyVersion` at `1.0.0.0`

- **Status**: Accepted
- **Date**: 2026-11-17
- **Deciders**: @Chris-Wolfgang
- **Tags**: `versioning`, `packaging`

## Context

`Wolfgang.Etl.DbClient` targets `net462` through `net481`, so it runs on the .NET Framework CLR, which binds a reference by its full four-part `AssemblyVersion`. The package `<Version>` moves on every release. If `AssemblyVersion` moved with it, every .NET Framework consumer would need a binding redirect on every upgrade. .NET 5 and later ignore the value for binding.

The project file already carries the pin and a comment explaining the three version fields; there was no ADR. Separately, the ETL family has settled a policy (recorded in ETL-Abstractions as "`<AssemblyVersion>` policy for the ETL family"): a new library that targets .NET Framework derives `0.{Minor}.0.0` during `0.x`, and a library that already ships pinned at `1.0.0.0` stays pinned, because moving it to a `0.x` value would be a numeric downgrade that breaks the redirects consumers already have.

## Decision

`<AssemblyVersion>` is pinned at `1.0.0.0` in `src/Wolfgang.Etl.DbClient/Wolfgang.Etl.DbClient.csproj` and changes only on a deliberate breaking API change. `<FileVersion>` is derived from `<Version>` (pre-release tag stripped, `.0` appended) and `<InformationalVersion>` is `<Version>`, so those two carry the real release number. The library is not moved to `0.{Minor}.0.0`.

## Consequences

- **Positive**: a .NET Framework consumer upgrades across patch and minor releases with no binding redirect.
- **Negative**: while the library is `0.x`, a breaking minor release gives a .NET Framework consumer no bind-time signal. A consumer who swaps in the new package without recompiling finds out at run time. Only the package version and Package Validation convey the break.
- **Negative**: the `1.0.0.0` identity is already in use, so the real `1.0.0` release will not change it unless something else is decided. The family policy leaves that question open; it needs an ADR here before 1.0.
- **Neutral**: the three version fields look inconsistent to a reader who does not know the convention. The comment in the project file and this ADR are the explanation.

## Alternatives considered

- Let the SDK derive `AssemblyVersion` from `<Version>`: a different binding identity on every release, and a redirect on every upgrade.
- Derive `0.{Minor}.0.0`, as the family policy prescribes for new libraries: not available to a library already published at `1.0.0.0`, because the change is a downgrade.

## Notes

- Sibling records of the same pin: ETL-Abstractions ADR-0007, ETL-Xml ADR-001.
- Recorded after the fact: the pin was already in force when this ADR was written.
