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

- **Release path**: OIDC / NuGet Trusted Publishing via `NuGet/login@v1` in `.github/workflows/release.yaml` (adopted in [#228](https://github.com/Chris-Wolfgang/Etl-DbClient/pull/228)). **No long-lived `NUGET_API_KEY`** exists on the repo or account. Any legacy key on the account should be treated as an anomaly and deleted immediately.
- **Fallback**: none. If Trusted Publishing is compromised, the incident is at the GitHub-account level (the OIDC identity is `Chris-Wolfgang/Etl-DbClient`).
- **Owner**: @Chris-Wolfgang.
- **Downstream consumers**: none inside the Wolfgang.* org today; the package is public on nuget.org.
- **Package coordinates for unlisting**: `Wolfgang.Etl.DbClient` on nuget.org — https://www.nuget.org/packages/Wolfgang.Etl.DbClient/.
