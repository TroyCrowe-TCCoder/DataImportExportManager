# Template-Ready Baseline Guide

## Purpose
Use this guide when turning a repository into a reusable template baseline for future services and applications.

## What Should Stay in the Template
Keep these assets in the template repository:
- `docs/standards/global-engineering-standards.md`
- `docs/standards/copilot-global-user-instructions-template.md`
- `docs/standards/copilot-global-user-instructions-ready-to-paste.md`
- `docs/standards/repository-template-checklist.md`
- `docs/standards/repository-copilot-addendum-template.md`
- `docs/standards/standards-rollout-guide.md`
- `scripts/standards/Apply-SharedStandards.ps1`
- `scripts/standards/Apply-SharedStandardsToRepositoriesUnderRoot.ps1`
- `scripts/standards/Initialize-NewRepositoryStandards.ps1`

## What Must Be Replaced Per Repository
Before a new repository is considered ready, replace the placeholders in `.github/copilot-instructions.md` for:
- repository remote or hosting context
- application purpose or bounded context
- tenant, platform, or cloud specifics
- auth claim names or identity-provider specifics
- approved provider scope
- deployment target names or environment constraints
- meaningful integration relationships or explicit exceptions

## What Should Not Be Globalized
Do not place these into the global user instructions baseline:
- specific cloud resource names
- tenant ids or claim naming tied to one solution
- app registration ids or environment secrets
- current project relationships or integration notes
- repo-specific deployment or pipeline exceptions

## Post-Create Tasks for a New Repository
1. Apply the shared standards assets.
2. Update `.github/copilot-instructions.md` with the new repository's meaningful deviations.
3. Confirm the production and test project names follow the standard naming pattern.
4. Add CI build and test validation.
5. Add deployment-specific documentation only after the target environment is known.
6. Validate that no placeholder values remain in the repo-local addendum.

## Recommended Creation Paths
### Option 1: Repository Template
Use a repository template that already includes the standards docs, scripts, and a placeholder `.github/copilot-instructions.md`.

### Option 2: Bootstrap Script
Create a new repository folder, then run:
```powershell
.\scripts\standards\Initialize-NewRepositoryStandards.ps1 -RepositoryRoot 'C:\Repos\NewService'
```

### Option 3: Existing Repository Adoption
For an already-created repository, run:
```powershell
.\scripts\standards\Apply-SharedStandards.ps1 -TargetPaths 'C:\Repos\ExistingService'
```

## Template Validation Checklist
A repository is template-ready when:
- global standards files are present
- rollout scripts are present
- `.github/copilot-instructions.md` is slim and repo-specific
- no current-solution-only values are baked into the reusable templates
- the rollout guide explains both existing-repo and new-repo onboarding
