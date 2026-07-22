# Reproducible-build guarantee

`Wolfgang.Etl.DbClient` claims **reproducible builds**: building the same tagged commit twice — same source, same SDK version, different runner OS — produces byte-identical `.dll` / `.pdb` / `.nupkg` outputs. Downstream consumers, SBOM validators, and third-party reproducers can rely on that guarantee.

## What "reproducible" means here

- **Deterministic**: same source + same toolchain + same environment → same output. This is table stakes; C# has been deterministic-by-default for years.
- **Reproducible**: same source + same toolchain, *different environments* → same output. Achievable but requires ruling out embedded absolute paths, machine-specific metadata, timestamp injection, and non-deterministic ordering. This document describes the specific knobs used and how a consumer verifies them.

## The knobs

Set in `Directory.Build.props` and inherited by every csproj that ships:

| Property | Value | Why |
|---|---|---|
| `<Deterministic>` | `true` | Compiler emits deterministic type layouts, GUIDs, embedded timestamps. |
| `<ContinuousIntegrationBuild>` | `true` | `.pdb` records use CI-normalised paths (`/_/` prefix) instead of the runner's absolute paths. |
| `<EmbedUntrackedSources>` | `true` | Generated sources (source-generator output) embed into the PDB rather than referencing per-machine paths. |
| `<IncludeSymbols>` + `<SymbolPackageFormat>snupkg</SymbolPackageFormat>` | | Separate `.snupkg` per NuGet convention; portable PDBs. |

`Microsoft.SourceLink.GitHub` (added via `Directory.Build.props`) rewrites source paths in the PDB to `https://raw.githubusercontent.com/Chris-Wolfgang/Etl-DbClient/<sha>/*`. Combined with `ContinuousIntegrationBuild`, this eliminates the runner-machine path variance that would otherwise break reproducibility.

## Automated verification

`.github/workflows/reproducible-build.yaml` runs on every PR that touches `src/**`, `Directory.Build.*`, or the workflow itself. It:

1. Builds `src/Wolfgang.Etl.DbClient/Wolfgang.Etl.DbClient.csproj` on **`ubuntu-latest`** and **`windows-latest`** in parallel — same source, same SDK version, different OS.
2. Computes `sha256sum` over the **produced own-binary artifacts** (`Wolfgang.Etl.DbClient.dll` + `.pdb` per TFM, plus the packed `.nupkg` / `.snupkg`). Transitive-dep DLLs from the runtime pack are deliberately excluded — those aren't part of this repo's reproducibility claim.
3. Uploads each per-OS manifest as an artifact for 30 days.
4. `diff -u`s the two manifests. The result is posted to the Step Summary tab.

### Gate mode: informational (non-blocking) today

The current tree isn't yet reproducible cross-OS — the deterministic knobs above are necessary but not sufficient. The Compare step reports divergence as a workflow warning + Step Summary section, but exits 0 so it doesn't block merges.

The follow-up work to close the gap and flip the gate to blocking is tracked in [#255](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/255) (candidate causes: PathMap coverage, SDK-emitted AssemblyMetadata attributes, source-generator file ordering, `.nupkg` zip-format determinism).

## Verify a released build yourself

To confirm a NuGet package on nuget.org actually came from the tagged commit and reproduces from source:

```bash
# 1. Clone the tagged commit.
git clone https://github.com/Chris-Wolfgang/Etl-DbClient
cd Etl-DbClient
git checkout v<version>

# 2. Restore + build + pack the runtime csproj.
dotnet restore src/Wolfgang.Etl.DbClient/Wolfgang.Etl.DbClient.csproj
dotnet build src/Wolfgang.Etl.DbClient/Wolfgang.Etl.DbClient.csproj \
  --no-restore --configuration Release
dotnet pack src/Wolfgang.Etl.DbClient/Wolfgang.Etl.DbClient.csproj \
  --no-build --configuration Release --output my-artifacts

# 3. Download the published package.
curl -sSL -o published.nupkg \
  "https://api.nuget.org/v3-flatcontainer/wolfgang.etl.dbclient/<version>/wolfgang.etl.dbclient.<version>.nupkg"

# 4. sha256sum both. They should match.
sha256sum my-artifacts/Wolfgang.Etl.DbClient.<version>.nupkg published.nupkg
```

If the digests differ, either the tag doesn't correspond to the published binary (audit the release-workflow log for that tag) or a reproducibility knob regressed between publish time and now (compare against the artifact manifests uploaded by the reproducible-build workflow for that commit).

## What can break reproducibility

- A new csproj that skips `Directory.Build.props` inheritance — the deterministic knobs won't apply.
- A package that ships per-OS binaries and lands in the runtime graph (currently: none in this repo's runtime deps).
- A build step that stamps a wall-clock timestamp into an assembly (e.g. `AssemblyInformationalVersionAttribute` with `$([System.DateTime]::Now)`) — never do this; use `<InformationalVersion>` from a static `<Version>` instead.
- A source-generator whose output depends on file-enumeration order rather than a stable sort — a subtle bug that only shows up when the two OSes surface files in different orders.

Refs [#146](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/146).
