# Standards Rollout Guide

## Goal
Use the shared standards files and rollout script to reduce manual setup for both future repositories and existing solutions.

## What Can Be Automated
The following can be applied automatically to repositories:
- `docs/standards/global-engineering-standards.md`
- `docs/standards/copilot-global-user-instructions-template.md`
- `docs/standards/copilot-global-user-instructions-ready-to-paste.md`
- `docs/standards/repository-modernization-checklist.md`
- `docs/standards/repository-template-checklist.md`
- `docs/standards/repository-copilot-addendum-template.md`
- `docs/standards/existing-repositories-rollout-execution-plan.md`
- `docs/standards/template-ready-baseline-guide.md`
- `.github/copilot-instructions.md` when a repository does not already have a meaningful local addendum or when overwrite is explicitly requested

## What Remains a One-Time Manual Step
Global Copilot user instructions are outside the repository, so they still require a one-time manual setup in the user or organization Copilot instructions experience.

Use `docs/standards/copilot-global-user-instructions-template.md` as the exact baseline for that setup.
If a paste-ready version is preferred, use `docs/standards/copilot-global-user-instructions-ready-to-paste.md`.

## Rollout Script
Script path:
- `scripts/standards/Apply-SharedStandards.ps1`
- `scripts/standards/Apply-SharedStandardsToRepositoriesUnderRoot.ps1`
- `scripts/standards/Initialize-NewRepositoryStandards.ps1`

### Example: apply to one existing repository
```powershell
.\scripts\standards\Apply-SharedStandards.ps1 -TargetPaths 'C:\Repos\MyApi'
```

### Example: apply to multiple existing repositories
```powershell
.\scripts\standards\Apply-SharedStandards.ps1 -TargetPaths 'C:\Repos\ApiOne','C:\Repos\ApiTwo'
```

### Example: discover existing repositories under a root folder
```powershell
.\scripts\standards\Apply-SharedStandardsToRepositoriesUnderRoot.ps1 -RootPath 'C:\Repos'
```

### Example: overwrite a placeholder repo addendum
```powershell
.\scripts\standards\Apply-SharedStandards.ps1 -TargetPaths 'C:\Repos\MyApi' -OverwriteRepoAddendum
```

### Example: standards docs only
```powershell
.\scripts\standards\Apply-SharedStandards.ps1 -TargetPaths 'C:\Repos\MyApi' -CreateStandardsFolderOnly
```

### Example: initialize a future repository or solution folder
```powershell
.\scripts\standards\Initialize-NewRepositoryStandards.ps1 -RepositoryRoot 'C:\Repos\NewApi'
```

## Recommended Future-State Process
1. Put the contents of `docs/standards/copilot-global-user-instructions-template.md` into Copilot global user instructions.
2. Use a repository template that already contains the `docs/standards` folder and `.github/copilot-instructions.md` addendum template.
3. Run `scripts/standards/Apply-SharedStandards.ps1` or `scripts/standards/Apply-SharedStandardsToRepositoriesUnderRoot.ps1` for existing repositories that need to pick up the shared standards.
4. Keep `.github/copilot-instructions.md` limited to meaningful repository-specific deviations.
5. Review each repository against `docs/standards/repository-modernization-checklist.md` and drive execution with `docs/standards/existing-repositories-rollout-execution-plan.md`.

## Existing Repository Adoption Guidance
- If a repository already has a meaningful `.github/copilot-instructions.md`, do not overwrite it blindly.
- The rollout script preserves an existing addendum unless `-OverwriteRepoAddendum` is supplied.
- Existing repositories should receive the shared standards files even when the local addendum is preserved.

## New Repository Guidance
For new solutions, the best non-manual path is:
- a repository template that includes the standards files
- global Copilot user instructions already configured
- a slim repo addendum copied from `docs/standards/repository-copilot-addendum-template.md`
- the replacement and validation steps from `docs/standards/template-ready-baseline-guide.md`

## Validation Checklist
After rollout, confirm:
- `docs/standards` exists in the target repository
- `.github/copilot-instructions.md` exists and contains only repo-specific deviations
- the global user instructions are configured once at the user or org level
- the repo-specific addendum references the shared standards files
