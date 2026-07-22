# Security Policy

## Reporting a Vulnerability

If you discover a security vulnerability, please follow these steps:

1. **Do not** create a public issue on this repository.
2. In the top navigation of this repository, click the **Security** tab.
3. In the top right, click the **Report a vulnerability** button.
4. Fill out the provided form with:
   - A description of the vulnerability
   - Steps to reproduce the issue
   - Potential impact
   - Suggested fix (if you have one)

## Response Timeline

We will acknowledge your report within 48 hours and provide an estimated timeline for a fix.

## Thank You

Your help is greatly appreciated!
Responsible disclosure of security vulnerabilities helps protect our entire community.

## Release path & compromise scope

Facts a maintainer would need at 2am if the release identity is compromised. Generic incident-response steps (rotating credentials, revoking OAuth apps, publishing advisories, unlisting NuGet packages) are not duplicated here — GitHub's and NuGet's own docs update faster than a checked-in runbook. The fleet-canonical full runbook is tracked at [Chris-Wolfgang/repo-template#430](https://github.com/Chris-Wolfgang/repo-template/issues/430).

- **Release path**: OIDC / NuGet Trusted Publishing via `NuGet/login@v1` in `.github/workflows/release.yaml` (adopted in [#228](https://github.com/Chris-Wolfgang/Etl-DbClient/pull/228)). The workflow mints an ephemeral push token per run via OIDC — the release path does not depend on a long-lived API key stored in GitHub secrets or on the NuGet account. During an incident, check the NuGet account for any long-lived API keys anyway (they can be created outside of CI) and delete anything you don't recognize.
- **Fallback**: none. If Trusted Publishing is compromised, the incident is at the GitHub-account level (the OIDC identity is `Chris-Wolfgang/Etl-DbClient`).
- **Owner**: @Chris-Wolfgang.
- **Downstream consumers**: none known inside the Wolfgang.* org at time of writing; the package is public on nuget.org, so unknown downstream consumers may exist. Re-check via `dotnet-outdated`, GitHub code-search, and the NuGet package's `Used By` dependents list before communicating during an incident.
- **Package coordinates for unlisting**: `Wolfgang.Etl.DbClient` on nuget.org — https://www.nuget.org/packages/Wolfgang.Etl.DbClient/.

## Supply-chain verification (consumer-side)

Every published `Wolfgang.Etl.DbClient` NuGet has:

1. **A CycloneDX SBOM** (`Wolfgang.Etl.DbClient.bom.json`) attached to the GitHub Release, listing every direct + transitive dependency at release time.
2. **A SLSA-3 build provenance attestation** signed by Sigstore's keyless CA using GitHub's OIDC identity. The attestation proves the `.nupkg` + `.snupkg` were built by `.github/workflows/release.yaml` at a specific commit SHA — no local build was substituted, no bit was flipped between build and publish.

To verify a package you downloaded from nuget.org actually came from this repo's release pipeline:

```bash
# 1. Download the package from nuget.org (or your local NuGet feed).
curl -sSL -o Wolfgang.Etl.DbClient.<version>.nupkg \
  "https://api.nuget.org/v3-flatcontainer/wolfgang.etl.dbclient/<version>/wolfgang.etl.dbclient.<version>.nupkg"

# 2. Verify the SLSA attestation.
gh attestation verify Wolfgang.Etl.DbClient.<version>.nupkg \
  --owner Chris-Wolfgang \
  --repo Etl-DbClient
```

The `gh attestation verify` command:
- Fetches the attestation from Sigstore's public transparency log.
- Confirms the attestation was signed by `Chris-Wolfgang/Etl-DbClient`'s OIDC identity (GitHub-verified, unforgeable).
- Confirms the workflow was `.github/workflows/release.yaml`.
- Confirms the artifact's SHA-256 matches the one recorded at build time.

Any mismatch = the file you downloaded didn't come from a legitimate release, or was tampered with in transit / on your local cache.

For the SBOM, download the `Wolfgang.Etl.DbClient.bom.json` asset from the GitHub Release and validate with any CycloneDX-aware SBOM tooling (`cyclonedx-cli`, Grype, Trivy, GitHub's Dependency Graph, etc.).

Refs [#138](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/138).
