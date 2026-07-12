# Disaster recovery — NuGet / GitHub account compromise

**Time-critical runbook.** If credentials for the NuGet account or the GitHub account are compromised (phishing, stolen machine, SSO breach, leaked token in a public log), work through this document top-to-bottom in order. Do the containment steps before communicating externally.

Review this document quarterly. Contact details and integration points drift.

---

## 1. Contain

**Do these first, even if you haven't confirmed exploitation yet.** Cost of a false alarm is minutes; cost of a real breach that spreads is days.

### GitHub account

1. **Sign out all other sessions.** GitHub → Settings → Sessions → **Sign out of all other sessions**.
2. **Revoke suspicious OAuth apps / SSH keys / PATs.** GitHub → Settings → Applications, SSH and GPG keys, Personal access tokens. Revoke anything you don't recognise.
3. **Rotate the password + reseed 2FA.** Assume 2FA seeds are compromised if the machine that held them is compromised.
4. **Disable Actions on all repos** until the audit is complete: `gh api -X PUT repos/Chris-Wolfgang/<repo>/actions/permissions -f enabled=false`. Reverse with `-f enabled=true`.

### NuGet account

1. **Sign in at nuget.org**, change password immediately, sign out other sessions.
2. **Delete every API key.** `Wolfgang.Etl.DbClient` uses **[Trusted Publishing (OIDC)](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)** for releases as of 2026-07-06 ([#228](https://github.com/Chris-Wolfgang/Etl-DbClient/pull/228)) — no long-lived `NUGET_API_KEY` should exist. If any legacy key is still on the account, delete it.
3. **Review the Trusted Publishing policy list.** Ensure only the expected `Chris-Wolfgang/Etl-DbClient` repository policy is present. Delete unfamiliar entries.

---

## 2. Assess the damage

Work through this checklist to size the incident before acting further.

- [ ] **Which accounts?** GitHub only, NuGet only, or both?
- [ ] **Which repos are affected?** Any private-repo secrets that could pivot to other systems (deploy tokens, cloud credentials)?
- [ ] **Any new NuGet packages published under my identity during the compromise window?** Check https://www.nuget.org/profiles/Chris-Wolfgang and compare against the release history in git.
- [ ] **Any releases created via GitHub Actions during the compromise window?** `gh api "repos/Chris-Wolfgang/Etl-DbClient/actions/runs?event=release" | jq '.workflow_runs[] | select(.status == "completed") | {id, name, actor: .triggering_actor.login, created_at}'`
- [ ] **Any force-pushes to `main`?** `gh api repos/Chris-Wolfgang/Etl-DbClient/events | jq '.[] | select(.type=="PushEvent") | .payload.forced' | grep -c true`
- [ ] **Any new collaborators / outside-collaborators added?**
- [ ] **Any changes to branch rulesets / repository ruleset actors?**

Preserve evidence: `gh api "repos/Chris-Wolfgang/Etl-DbClient/audit-log?phrase=action:repo.*" > /tmp/audit-<timestamp>.json` before making further changes.

---

## 3. Unlist compromised packages (do NOT delete)

If a malicious package version was published under the `Wolfgang.Etl.DbClient` identity:

1. Sign in at nuget.org → **Manage packages** → select the compromised version.
2. Set **List in search results** to **Off** ("unlist"). Do **NOT** delete.
   - **Why unlist, not delete?** Deleted NuGet versions can't be re-published under the same version number. Unlisted versions stop appearing in searches and Package Manager but remain restorable for consumers who already have the version pinned in their lock file — deleting them would silently break existing consumer builds without letting the consumer see there's a security concern.
3. Publish a superseding version with a patched or reverted binary if the compromise altered functionality. Bump the patch version.
4. Contact NuGet support (below) if the compromised version poses a supply-chain risk requiring true deletion. Only NuGet operators can delete.

### NuGet support contact

- **Email**: `support@nuget.org`
- **Alternate**: File a [nuget.org support ticket](https://www.nuget.org/policies/Contact).
- **What to include**: package ID, version(s), timestamps, brief incident summary. Ask for expedited handling if the compromise is confirmed.

---

## 4. GitHub security advisory

If the compromise affects released library code (not just infrastructure):

1. **Repo → Security → Advisories → New draft advisory.**
2. Fill in: affected versions, CVSS, description, patched version.
3. Publish once the patched version is on NuGet.
4. Reference the advisory GHSA-ID in the release notes and CHANGELOG.

Draft advisories are private until published — safe to prepare before the patched release is on NuGet.

---

## 5. Communicate to consumers

Once the compromised version is unlisted **and** a patched version is published:

- **CHANGELOG**: prepend a `## Security` section describing the affected versions, the fix, and the recommended upgrade action.
- **Release notes** on the GitHub release: link to the advisory + the CHANGELOG entry.
- **Downstream repos** in the ETL family that depend on `Wolfgang.Etl.DbClient`: open issues linking to the advisory so the maintainer for each repo pins the safe version.

### Consumer notification template

Copy into an issue on downstream repos:

```
Subject: Security advisory affecting Wolfgang.Etl.DbClient <versions>

A security advisory has been published for Wolfgang.Etl.DbClient
covering versions <affected range>.

- Advisory: <GHSA-ID URL>
- Patched version: <version>
- Recommended action: upgrade to <version> or later.

If you are pinned to an affected version, please plan the upgrade
and audit any transitive consumers of your package for the same
version range.
```

---

## 6. Post-incident

- [ ] Root-cause writeup (private repo `security-notes/YYYY-MM-DD-<incident>.md`).
- [ ] Rotate any secondary secrets that could have been observed during the compromise (SSH keys, deploy tokens, other-service API keys stored in `~/.config` or the OS credential store).
- [ ] Re-enable Actions on all repos (`gh api -X PUT repos/Chris-Wolfgang/<repo>/actions/permissions -f enabled=true`).
- [ ] Update this runbook with anything the incident revealed as missing or wrong.

---

## Reference — Etl-DbClient specifics

- **Release path**: OIDC / NuGet Trusted Publishing via `NuGet/login@v1` in `.github/workflows/release.yaml` (as of [#228](https://github.com/Chris-Wolfgang/Etl-DbClient/pull/228)). No long-lived API key.
- **Fallback**: none. If Trusted Publishing is compromised, the incident is at the GitHub-account level (OIDC identity = the repo).
- **Owners**: @Chris-Wolfgang.
- **Downstream consumers**: none inside the Wolfgang.* org today; the package is public on nuget.org.

## Fleet note

This runbook lives in Etl-DbClient. Per [#151](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/151) acceptance criteria the canonical version should live in `repo-template` and sync to every repo. That fleet-canonicalization is a separate follow-up.
