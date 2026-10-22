# Reproducible-build guarantee

`Wolfgang.Etl.DbClient` targets **reproducible builds**: building the same tagged commit twice — same source, same SDK version, different runner OS — should produce byte-identical `.dll` / `.pdb` outputs, and CI fails when the Linux and Windows builds differ (see [Gate mode](#gate-mode-blocking) below and [#255](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/255)).

The guarantee covers the **assemblies and symbols**, not the package files: NuGet stamps the pack time into every zip entry, so two packs of the same commit, even on the same machine, give different `.nupkg` / `.snupkg` bytes while the `.dll` / `.pdb` inside them are identical.

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

Two more inputs differed per OS and are removed explicitly:

| Where | What | Why |
|---|---|---|
| `Directory.Build.targets` | `NormalizeGeneratedLineEndings` rewrites the SDK-generated `AssemblyInfo` / `AssemblyAttributes` / `GlobalUsings` sources to LF before `CoreCompile` (packable projects) | The SDK writes them with the OS newline; the PDB hashes and embeds them, so the PDB checksum and the DLL's MVID differed per OS. |
| `Directory.Build.props` | `Microsoft.NETFramework.ReferenceAssemblies` (build-time only) for `src/` net4x builds | Windows compiled against the installed targeting pack, Linux against this package; the PDB records each reference's MVID. |

`Microsoft.SourceLink.GitHub` (added via `Directory.Build.props`) rewrites source paths in the PDB to `https://raw.githubusercontent.com/Chris-Wolfgang/Etl-DbClient/<sha>/*`. Combined with `ContinuousIntegrationBuild`, this eliminates the runner-machine path variance that would otherwise break reproducibility.

## Automated verification

`.github/workflows/reproducible-build.yaml` runs on every PR that touches `src/**`, `Directory.Build.*`, or the workflow itself. It:

1. Builds `src/Wolfgang.Etl.DbClient/Wolfgang.Etl.DbClient.csproj` on **`ubuntu-latest`** and **`windows-latest`** in parallel — same source, same SDK version, different OS.
2. Computes `sha256sum` over the **produced own-binary artifacts** (`Wolfgang.Etl.DbClient.dll` + `.pdb` per TFM; the packed `.nupkg` / `.snupkg` digests are listed too, for reference only). Transitive-dep DLLs from the runtime pack are deliberately excluded — those aren't part of this repo's reproducibility claim.
3. Uploads each per-OS manifest as an artifact for 30 days.
4. `diff -u`s the `.dll` / `.pdb` lines of the two manifests. The result is posted to the Step Summary tab.

### Gate mode: blocking

Any difference in a `.dll` / `.pdb` digest between the two OSes fails the Compare step, as does a manifest with no assembly digests at all. `.nupkg` / `.snupkg` lines are left out of the comparison (see the top of this page). If the gate fails, diff the per-TFM PDB documents and referenced-assembly records first: a generated source with a different newline, or a different reference assembly, were the two causes [#255](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/255) removed.

## Per-release manifest

Every published release ships a `reproducible-build-manifest.json` as a
GitHub Release asset alongside the `.nupkg` / `.snupkg`. The manifest
records the sha256 of every artifact plus enough metadata for a
third-party reproducer to look up "what should I get if I rebuild this
tag from source?" without needing to download the actual package first.

Example (excerpt):

```json
{
  "schemaVersion": 1,
  "package": "Wolfgang.Etl.DbClient",
  "tag": "v0.7.0",
  "commitSha": "…",
  "releasedAt": "2026-07-29T…Z",
  "repository": "Chris-Wolfgang/Etl-DbClient",
  "instructions": "https://github.com/Chris-Wolfgang/Etl-DbClient/blob/main/docs/REPRODUCIBLE-BUILD.md",
  "artifacts": [
    { "name": "Wolfgang.Etl.DbClient.0.7.0.nupkg",  "sha256": "…", "size": 123456 },
    { "name": "Wolfgang.Etl.DbClient.0.7.0.snupkg", "sha256": "…", "size":  45678 }
  ]
}
```

Grab it from the release page:

```bash
gh release download v<version> --repo Chris-Wolfgang/Etl-DbClient --pattern reproducible-build-manifest.json
```

Or fetch directly:

```bash
curl -sSL -o reproducible-build-manifest.json \
  "https://github.com/Chris-Wolfgang/Etl-DbClient/releases/download/v<version>/reproducible-build-manifest.json"
```

## Toolchain versions for verification

The release build resolves the .NET SDK via `actions/setup-dotnet`'s
floating `x.x.x` pins in `release.yaml` (the latest patch of each major
line at build time — there's no `global.json` pinning an exact SDK
build). For a rebuild to match, use the same major .NET SDK line the
target TFM needs (e.g. the `10.0.x` SDK for `net10.0`) — check
`release.yaml`'s `Setup .NET` step for the current list. Other tools
used in the verification recipe below:

- **GitHub CLI** ≥ 2.60 (for `gh attestation verify` support).
- **`jq`** (for the manifest cross-check `sha256sum -c` pipeline).
- **`sha256sum`** (GNU coreutils, or an equivalent — `shasum -a 256` on macOS).

## Verify a released build yourself

To confirm a NuGet package on nuget.org actually came from the tagged
commit and reproduces from source:

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

# 3. Download the published package + snupkg + the manifest, keeping
#    NuGet's own filenames so they line up with what the manifest lists.
curl -sSL -o "Wolfgang.Etl.DbClient.<version>.nupkg" \
  "https://api.nuget.org/v3-flatcontainer/wolfgang.etl.dbclient/<version>/wolfgang.etl.dbclient.<version>.nupkg"
curl -sSL -o "Wolfgang.Etl.DbClient.<version>.snupkg" \
  "https://api.nuget.org/v3-flatcontainer/wolfgang.etl.dbclient/<version>/wolfgang.etl.dbclient.<version>.snupkg"
gh release download v<version> --repo Chris-Wolfgang/Etl-DbClient \
  --pattern reproducible-build-manifest.json

# 4. Cross-check:
#    - your local rebuild vs the NuGet-published binaries. Compare the
#      assemblies INSIDE the packages: the .nupkg files themselves differ,
#      because NuGet stamps the pack time into every zip entry.
mkdir -p mine published
unzip -q -o my-artifacts/Wolfgang.Etl.DbClient.<version>.nupkg -d mine
unzip -q -o Wolfgang.Etl.DbClient.<version>.nupkg -d published
diff <(cd mine && find lib -type f | sort | xargs sha256sum) \
     <(cd published && find lib -type f | sort | xargs sha256sum)
#    - the NuGet-published package files vs the manifest's declared hashes
#      (filenames must match what step 3 downloaded them as)
jq -r '.artifacts[] | "\(.sha256)  \(.name)"' reproducible-build-manifest.json | \
  sha256sum -c --ignore-missing
```

Both checks should match (the `diff` prints nothing). If they don't:

- **Local rebuild ≠ NuGet-published**: either the tag doesn't correspond to the published binary (audit the release-workflow log for that tag) or a reproducibility knob regressed between publish time and now.
- **NuGet-published ≠ manifest**: the release was tampered with after publish. This is a serious supply-chain event — file a [discrepancy issue](#file-a-discrepancy) immediately.

## File a discrepancy

If your local rebuild does not match the manifest's declared sha256:

1. Open an issue at `https://github.com/Chris-Wolfgang/Etl-DbClient/issues/new` with title `reproducibility: <version> divergence on <OS>`.
2. Include:
   - The version tag you built.
   - Your `dotnet --info` output (SDK version, OS, RID).
   - Both sha256 values (your rebuild's, and the manifest's declared).
   - Any local modifications to `Directory.Build.props`, `global.json`, or environment (`DOTNET_ROOT`, `MSBUILDDISABLENODEREUSE`, etc).

The maintainer investigates by reproducing your environment on a clean runner and either issues an errata for the release or documents the specific knob that leaked.

## Third-party verification attestations

Wolfgang.Etl.DbClient participates in the [Reproducible Builds project](https://reproducible-builds.org/) conventions for cross-verifier attestations.

A third party who has independently rebuilt a tagged release and gotten matching bytes can publish that fact:

1. Follow the Reproducible Builds project's [rebuilderd](https://reproducible-builds.org/tools/#rebuilderd) or `in-toto` attestation format.
2. Sign the attestation with your GPG key or a Sigstore identity.
3. Publish somewhere durable (a personal git repo, a rebuilders' index, etc.).
4. Optionally: open an issue on this repo referencing your attestation so it can be linked from the release page.

The GitHub Release itself already carries a Sigstore-keyless [SLSA build-provenance attestation](https://slsa.dev/spec/v1.0/provenance) generated by `attest-build-provenance` in `release.yaml`. Consumers can verify it with:

```bash
gh attestation verify <path-to-downloaded.nupkg> --owner Chris-Wolfgang --repo Etl-DbClient
```

The SLSA attestation proves "this .nupkg was built by this release-workflow run at this commit." The reproducibility manifest + third-party attestations layer on top to prove "…and building from source deterministically produces the same bytes."

## What can break reproducibility

- A new csproj that skips `Directory.Build.props` inheritance — the deterministic knobs won't apply.
- A package that ships per-OS binaries and lands in the runtime graph (currently: none in this repo's runtime deps).
- A build step that stamps a wall-clock timestamp into an assembly (e.g. `AssemblyInformationalVersionAttribute` with `$([System.DateTime]::Now)`) — never do this; use `<InformationalVersion>` from a static `<Version>` instead.
- A source-generator whose output depends on file-enumeration order rather than a stable sort — a subtle bug that only shows up when the two OSes surface files in different orders.

Refs [#146](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/146).
