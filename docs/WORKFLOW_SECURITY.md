# Workflow Security

## Overview

This document describes the security measures implemented in the GitHub Actions workflows for this repository, particularly focusing on the PR validation workflow (`.github/workflows/pr.yaml`).

## Security Architecture

### 1. Workflow YAML Protection

**Mechanism**: `pull_request_target` trigger

The PR workflow uses `pull_request_target` instead of `pull_request`. This means:
- The workflow YAML file is always executed from the base branch (main)
- Pull requests cannot modify the workflow logic that validates them
- Prevents malicious PRs from weakening or bypassing validation checks

**Code Reference**:
```yaml
on:
  pull_request_target:  # Runs from the main branch, not from PR branch
    branches:
      - main
```

### 2. Configuration File Protection

**Problem**: While `pull_request_target` protects the workflow YAML, the checked-out code includes configuration files (`.editorconfig`, `BannedSymbols.txt`, etc.) that control:
- Code analyzer behavior
- Code quality standards
- Security scanning rules

A malicious PR could modify these files to disable security checks.

**Solution**: After checking out the PR code, we fetch and overwrite configuration files from the trusted main branch.

**Protected Configuration Files**:
- `.editorconfig` - Code style and analyzer rules
- `Directory.Build.props` - MSBuild properties
- `Directory.Build.targets` - MSBuild targets
- `BannedSymbols.txt` - Banned API usage rules
- `*.globalconfig` - Global analyzer configuration
- `*.ruleset` - Code analysis rulesets
- `.github/workflows/*.yml` and `.github/workflows/*.yaml` - Workflow definitions

In addition to the overwrite step, a separate "Detect protected configuration file changes" step in `pr.yaml` causes the PR to fail if any of these files differ from `main`, signalling that a maintainer must manually review the change. Dependabot is exempted (its bumps to `Directory.Build.props` are legitimate).

**Implementation** (in jobs that consume project source — e.g. `detect-projects`, the test stages, and the security scans; *not* the `secrets-scan` job, which only fetches `.gitleaks.toml`):
```yaml
- name: Fetch trusted configuration files from main branch
  run: |
    echo "Fetching configuration files from main branch to prevent malicious overrides..."
    
    # Fetch the main branch
    git fetch origin main:main-branch
    
    # List of configuration files that should come from trusted main branch
    config_files=(
      ".editorconfig"
      "Directory.Build.props"
      "Directory.Build.targets"
      "BannedSymbols.txt"
      "*.globalconfig"
      "*.ruleset"
      ".github/workflows/*.yml"
      ".github/workflows/*.yaml"
    )
    
    # Copy each configuration file from main branch if it exists
    for config_file in "${config_files[@]}"; do
      # [Copy logic - see workflow file for full implementation]
    done
```

### 3. Credential Protection

**Mechanism**: `persist-credentials: false`

All checkout steps include `persist-credentials: false` to prevent the checkout token from being written to git config:

```yaml
- name: Checkout code
  uses: actions/checkout@v6
  with:
    ref: refs/pull/${{ github.event.pull_request.number }}/head
    persist-credentials: false
```

**Note**: This prevents the token from being stored in git config, but does NOT prevent steps from accessing `GITHUB_TOKEN` if explicitly exposed.

### 4. Minimal Permissions

The workflow runs with minimal required permissions:

```yaml
permissions:
  contents: read
```

This limits the impact if the `GITHUB_TOKEN` is somehow exposed or misused.

## Attack Scenarios Prevented

### Scenario 1: Malicious Workflow Modification
**Attack**: PR modifies `.github/workflows/pr.yaml` to disable security checks
**Prevention**: `pull_request_target` ensures workflow runs from main branch
**Status**: ✅ Protected

### Scenario 2: Configuration File Tampering
**Attack**: PR modifies `.editorconfig` to disable security analyzers
**Prevention**: Configuration files are fetched from main branch after checkout
**Status**: ✅ Protected

### Scenario 3: Credential Theft
**Attack**: PR contains malicious code that tries to access GitHub credentials
**Prevention**: `persist-credentials: false` + minimal permissions
**Status**: ✅ Protected

### Scenario 4: Code Analysis Bypass
**Attack**: PR modifies `BannedSymbols.txt` or `.ruleset` to allow dangerous APIs
**Prevention**: These files are fetched from main branch after checkout
**Status**: ✅ Protected

## Validation

The following manual validation scenarios can be used when reviewing changes to the workflow security model:

1. **Configuration Fetch Validation**: Confirm that configuration files are fetched from the `main` branch during workflow execution
2. **Malicious Modification Validation**: Simulate a PR that modifies `.editorconfig` to disable analyzers and confirm the workflow replaces it with the trusted version from `main`

## Maintenance

### Making Changes to Protected Configuration Files

To update protected configuration files (`.editorconfig`, `BannedSymbols.txt`, etc.), follow this workflow:

1. **Create a PR with your configuration changes**
   - Make changes to the configuration file(s) in your PR branch
   - The PR workflow will still fetch and use the current main branch version for testing
   - This means your PR will be tested against the **existing** configuration standards

2. **Get your PR reviewed and merged to main**
   - Once merged, your configuration changes become the new "trusted" version on main
   - Future PRs will automatically use your updated configuration

3. **Why this works:**
   - Configuration changes are intentionally one commit behind during PR validation
   - This ensures you can't weaken security standards in the same PR that adds problematic code
   - After merge, the new standards apply to all subsequent PRs

**Example Workflow:**
```
PR #1: Update .editorconfig to add new rule
  ↓ (tested with old .editorconfig from main)
  ↓ (approved and merged)
  ↓
Main: Now has updated .editorconfig

PR #2: New feature
  ↓ (tested with updated .editorconfig from main)
  ↓ (builds/tests using new rules)
```

**Important Notes:**
- If you need to relax a security rule AND add code that violates the old rule in the same change, you'll need two PRs:
  1. First PR: Update the configuration file only
  2. Second PR: Add the code that requires the relaxed rules
- This is intentional security design to prevent simultaneous weakening of standards and addition of problematic code

### Adding New Protected Configuration Files

When adding new configuration files that control code quality or security:

1. Add the file name to the `config_files` array in every job that runs `Fetch trusted configuration files from main branch` (the project-detection job, each test-stage job, and the security-scan jobs — search `pr.yaml` for that step name to find them all). The `secrets-scan` job does not consume project config files and does not need to be updated.
2. Add the file path to the "Detect protected configuration file changes" guard in `pr.yaml` so PRs that touch the file fail with a maintainer-review banner.
3. Test that the file is correctly fetched from main branch.
4. Update this documentation.

## OSSF Scorecard

`.github/workflows/scorecard.yaml` runs the OpenSSF Scorecard on a weekly schedule + on push to `main`. It scores repository *configuration* (branch protection, pinned dependencies, dangerous-workflow patterns, permission scoping, ...) against ~20 best practices and publishes SARIF to the Security tab.

- **Public dashboard**: https://scorecard.dev/viewer/?uri=github.com/Chris-Wolfgang/Etl-DbClient (badge in README.md).
- **Baseline**: to be captured once the first run publishes (this PR's first `push:main` after merge). Update this section with the initial score + a list of intentionally accepted findings.
- **Score floor**: **7.5**. If a scheduled or push run drops the score below 7.5:
  1. The maintainer investigates the specific check(s) that regressed.
  2. If the regression is intentional (e.g. a new third-party action we accept without SHA-pin), add it to the accepted-findings list here with a one-line rationale, and note the score-floor drop in the next CHANGELOG entry.
  3. If unintentional, fix and re-run — do not lower the floor.
- **Related**: Complementary to CodeQL (source code SAST) and `zizmor` / `actionlint` (workflow YAML security). Refs [#152](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/152).

## Workflow YAML audit — zizmor + actionlint

`.github/workflows/workflow-security.yaml` runs on every PR that touches workflow YAML (`.github/workflows/**`, `.github/actions/**`) or the tool config files (`.zizmor.yml`, `.github/actionlint.yaml`), and on every push to `main` / `vNext` that touches the same paths. Two tools, complementary rule sets:

- **[zizmor](https://github.com/zizmorcore/zizmor)** — security-focused static analysis. Catches untrusted-input injection into `run:` blocks, missing / overly-permissive `permissions:` blocks, secret leaks via expression-context expansion, third-party actions unpinned by SHA. Publishes SARIF to Code Scanning (category: `zizmor`). Gate: `min-severity: high` — highs fail the PR, medium/low surface in Code Scanning without blocking.
- **[actionlint](https://github.com/rhysd/actionlint)** — syntax + typing + common-mistake linting. Broader coverage; catches issues zizmor doesn't model (shell-checker integration, matrix-expression typos, deprecated Actions syntax). Runs with `fail-on-error: true`.

**Configuration**:
- `.zizmor.yml` at the repo root — suppression list. Currently empty (no accepted findings).
- `.github/actionlint.yaml` — self-hosted-runner labels + rule config. Currently empty.

**Adding a suppression** (either tool): document *why* the finding is intentional in the config file. Prefer fixing the finding over suppressing it. Refs [#153](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/153).

## References

- [GitHub Actions Security Hardening](https://docs.github.com/en/actions/security-guides/security-hardening-for-github-actions)
- [Keeping your GitHub Actions secure](https://docs.github.com/en/actions/security-guides/security-hardening-for-github-actions#using-third-party-actions)
- [Understanding pull_request_target](https://securitylab.github.com/research/github-actions-preventing-pwn-requests/)
- [OSSF Scorecard checks](https://github.com/ossf/scorecard/blob/main/docs/checks.md)
- [zizmor rule catalog](https://docs.zizmor.sh/audits/)
- [actionlint rule reference](https://github.com/rhysd/actionlint/blob/main/docs/checks.md)
