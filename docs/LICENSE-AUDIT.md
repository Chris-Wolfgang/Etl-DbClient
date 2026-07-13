# License audit policy

## Why

Dependabot tracks dependency *versions* (and CVE alerts). It does not check the *licenses* of transitive dependencies. A new transitive dependency with an incompatible license (GPL, AGPL, OSL, BSL) would silently leak into a derivative work distributed under MIT — a real procurement / legal exposure for downstream consumers of `Wolfgang.Etl.DbClient`.

## What runs

`.github/workflows/license-audit.yaml` runs on every PR that touches a csproj / `Directory.Build.props` / `Directory.Packages.props` / the allowlist itself, on push to `main`, and nightly at 04:17 UTC (catches transitive updates we don't control).

Tool: `nuget-license` (dotnet global tool). Enumerates every transitive package under `src/Wolfgang.Etl.DbClient/Wolfgang.Etl.DbClient.csproj`, reads its declared license, fails the run if any package's license is not in the allowlist below. The full report is uploaded as an artifact (`license-report`) for 30 days.

## Allowlist

Source of truth: [`.github/license-allowlist.json`](../.github/license-allowlist.json).

Currently accepted:

| SPDX identifier | Notes |
|---|---|
| `MIT` | Standard permissive. |
| `Apache-2.0` | Standard permissive; includes patent grant. |
| `BSD-2-Clause` | Standard permissive. |
| `BSD-3-Clause` | Standard permissive; no-endorsement clause. |
| `MS-PL` | Microsoft Public License — permissive; used by some MS OSS. |
| `0BSD` | Public-domain equivalent; used by some SPDX-normalised metadata. |
| `Microsoft.NETCore.Platforms` | Placeholder ID nuget-license emits for a Microsoft package whose license URL points at `dotnet/runtime`. Effectively MIT. |

## Adding an entry

Requires reviewer sign-off in the same PR. The PR description must include:

1. **Which dependency** requires the new license (direct or transitive; if transitive, which direct dep pulls it in).
2. **Why the license is acceptable** for a library distributed under MIT — link the license text and a one-line risk assessment.
3. **Whether attribution requirements** apply (e.g. copyleft-adjacent licenses that require notice preservation). If so, add the notice to `THIRD-PARTY-NOTICES.md`.

Copyleft licenses will not be accepted. This covers the strong-copyleft family (GPL / LGPL / AGPL) and also the weak-copyleft licenses that still impose source-availability obligations on distribution (EPL, OSL, MPL). Non-OSI source-available licenses (BSL, PolyForm, SSPL) are likewise rejected.

## `THIRD-PARTY-NOTICES.md`

Follow-up work: auto-generate `THIRD-PARTY-NOTICES.md` from `license-report.json` at release time and pack it into the NuGet. Tracked separately in [#239](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/239) so it doesn't get orphaned when this PR closes [#148](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/148).

## Baseline

The first run's `license-report.json` is the baseline. If any current transitive package fails the initial audit, the fix is to either:

1. Confirm the license is genuinely acceptable and add it to the allowlist per the rules above; or
2. Replace the dependency with a permissively-licensed alternative.

Refs [#148](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/148).
