# Azure DevOps Branch Policy Checklist

## Purpose
Use this checklist to enforce the repository flow:
`local branch -> remote branch -> PR to dev -> PR to master`.

## Quick Start (5-Minute Setup)
1. Set default branch to `dev`.
2. Configure `dev` branch policies (PR required + build validation).
3. Configure `master` branch policies (PR required + owner approval).
4. Set `dev` and `master` branch security to restrict direct push/bypass for non-owner users.
5. Validate flow with one test PR to `dev` and one promotion PR from `dev` to `master`.

## Branch Targets
- `dev`: integration and validation gate
- `master`: promotion gate

## Operator Runbook (Azure DevOps UI)
1. Open `Repos` -> `Branches`.
2. On `dev`, open branch context menu -> `Branch policies` and configure required PR/build settings.
3. On `master`, open branch context menu -> `Branch policies` and configure owner-approval and promotion settings.
4. Open branch context menu -> `Security` for both `dev` and `master`.
5. Apply contributor restrictions (`Contribute`, `Bypass policies`, `Force push`) and owner permissions.
6. Save changes and verify policy scopes are set to the intended branch refs.

## Optional CLI Mapping (Reference)
- If automating policy setup, use Azure DevOps CLI policy commands as a mapping for:
  - required reviewers
  - build validation
  - merge strategy constraints
- Keep the UI checklist above as the source of truth for expected policy outcomes.

## `dev` Branch Policy Checklist
- Require pull requests for all changes.
- Require minimum reviewers (recommended: `1` or more).
- Require successful build validation using `dataimportexportmanager-ci.yml`.
- Require comment resolution before completion.
- Restrict direct pushes for non-owner users.
- Restrict policy bypass for non-owner users.

## `master` Branch Policy Checklist
- Require pull requests for all changes.
- Require repository owner approval for `dev -> master` promotions.
- Restrict direct pushes for non-owner users.
- Restrict policy bypass for non-owner users.
- Keep merge completion governed by approval + policy checks only.

## Permissions Guidance
For contributor identities or groups:
- Deny `Contribute` on `dev` and `master` as required by governance.
- Deny `Bypass policies when completing pull requests`.
- Deny `Bypass policies when pushing`.
- Deny `Force push`.

For repository owner:
- Allow approval/completion permissions needed for `dev -> master` promotion.

## Validation Sequence
1. Push a local branch to its remote branch.
2. Open PR from the remote branch to `dev`.
3. Verify required validation passes before merge to `dev`.
4. Open PR from `dev` to `master`.
5. Verify owner approval is required and enforced.
6. Merge to `master` and verify branch protection and policy enforcement remain intact.
