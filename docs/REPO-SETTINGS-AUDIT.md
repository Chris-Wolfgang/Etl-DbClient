# Repo Settings & Branch-Protection Audit

Snapshot of the canonical settings and the audit result for this repo.
This document is **descriptive** — the source of truth lives in:

- the **GitHub Ruleset** (`Protect main branch`) — ruleset id `14462276`
- the **repo settings** (Settings → General → Pull Requests + Settings → Branches)

To re-apply the canonical settings programmatically, see
`scripts/Setup-BranchRuleset.ps1` and `scripts/Setup-GitHubPages.ps1` in the
canonical `repo-template`.

## Branch-protection ruleset — `Protect main branch`

| Rule | Configured |
|---|---|
| `pull_request` (require PR) | ✅ |
| `required_status_checks` | ✅ (8 checks, see below) |
| `code_scanning` (CodeQL must succeed) | ✅ |
| `copilot_code_review` (Copilot review required) | ✅ |
| `non_fast_forward` (no force-push) | ✅ |
| `deletion` (no branch delete) | ✅ |
| `code_quality` | ✅ |

### Required status checks

| Check name | Workflow |
|---|---|
| `Stage 1: Linux Tests (.NET 5.0-10.0) + Coverage Gate` | `pr.yaml` |
| `Stage 2: Windows Tests (.NET 5.0-10.0, Framework 4.6.2-4.8.1)` | `pr.yaml` |
| `Stage 3: macOS Tests (.NET 6.0-10.0)` | `pr.yaml` |
| `Security Scan (DevSkim)` | `pr.yaml` |
| `Security Scan (CodeQL) (csharp)` | `codeql.yaml` |
| `Secrets Scan (gitleaks)` | `pr.yaml` |
| `Detect .NET Projects` | `pr.yaml` |
| `Integration (all)` | `pr.yaml` (aggregator over the per-RDBMS matrix) |

## Repo-level settings

| Setting | Value |
|---|---|
| `deleteBranchOnMerge` | ✅ true |
| `squashMergeAllowed` | ✅ true |
| `mergeCommitAllowed` | ✅ true |
| `rebaseMergeAllowed` | ✅ true |
| `viewerDefaultMergeMethod` | `MERGE` (used for the stacked-PR workflow) |
| `hasIssuesEnabled` | ✅ true |
| `hasProjectsEnabled` | ✅ true |
| `hasWikiEnabled` | ✅ true |

## Audit result

**No drift.** Ruleset rule set, required status checks, and repo merge settings all match the canonical pattern. The `MERGE` default merge method is intentional — the maintainer's stacked-PR workflow relies on merge commits so the stack's history stays visible after each merge (squash would flatten it).

## Re-audit cadence

This document captures the state at the time of [issue #114](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/114). Re-run the audit:

- when a new required check is added to `pr.yaml` (update the table above + the ruleset).
- when the canonical ruleset changes in `repo-template`.
- before each release that adds or removes a CI stage.
