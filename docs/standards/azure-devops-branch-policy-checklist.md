# Azure DevOps Branch Policy Checklist

## Purpose
Use this checklist to enforce the repository flow:
`local branch -> remote branch -> PR to dev -> PR to master -> delivery on master merge`.

## Branch Targets
- `dev`: integration and validation gate
- `master`: promotion and delivery gate

## Operator Runbook (Azure DevOps UI)
1. Open `Repos` -> `Branches`.
2. On `dev`, open branch context menu -> `Branch policies` and configure required PR/build settings.
3. On `master`, open branch context menu -> `Branch policies` and configure owner-approval and promotion settings.
4. Open branch context menu -> `Security` for both `dev` and `master`.
5. Apply contributor restrictions (`Contribute`, `Bypass policies`, `Force push`) and owner permissions.
6. Save changes and verify policy scopes are set to the intended branch refs.

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
- Keep merge completion configured to trigger master delivery pipeline execution.

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
6. Merge to `master` and verify delivery pipeline run starts.
