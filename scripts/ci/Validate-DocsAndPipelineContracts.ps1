param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Contains {
    param(
        [Parameter(Mandatory = $true)] [string] $Content,
        [Parameter(Mandatory = $true)] [string] $Needle,
        [Parameter(Mandatory = $true)] [string] $Description,
        [System.Collections.Generic.List[string]] $Failures
    )

    if ($Content.IndexOf($Needle, [StringComparison]::Ordinal) -lt 0) {
        $Failures.Add("Missing required marker: $Description")
    }
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$failures = [System.Collections.Generic.List[string]]::new()

$readmePath = Join-Path $repoRoot 'README.md'
$pipelinePath = Join-Path $repoRoot '.azure-pipelines\workflows\dataimportexportmanager-ci.yml'
$statusMatrixPath = Join-Path $repoRoot 'docs\standards\repository-modernization-status-matrix.md'

foreach ($requiredPath in @($readmePath, $pipelinePath, $statusMatrixPath)) {
    if (-not (Test-Path $requiredPath)) {
        $failures.Add("Required file not found: $requiredPath")
    }
}

if ($failures.Count -eq 0) {
    $readmeContent = Get-Content -Path $readmePath -Raw
    $pipelineContent = Get-Content -Path $pipelinePath -Raw
    $statusMatrixContent = Get-Content -Path $statusMatrixPath -Raw

    Assert-Contains -Content $readmeContent -Needle 'local branch -> remote branch -> PR to dev -> PR to master -> delivery on master merge' -Description 'README governance flow wording' -Failures $failures
    Assert-Contains -Content $readmeContent -Needle 'docs/standards/azure-devops-branch-policy-checklist.md' -Description 'README policy checklist reference' -Failures $failures
    Assert-Contains -Content $readmeContent -Needle '### Delivery Artifacts (Master Merge)' -Description 'README delivery artifacts section' -Failures $failures

    Assert-Contains -Content $pipelineContent -Needle '- stage: Validate' -Description 'pipeline Validate stage' -Failures $failures
    Assert-Contains -Content $pipelineContent -Needle 'dotnet restore DataImportExportManager.sln' -Description 'pipeline restore step' -Failures $failures
    Assert-Contains -Content $pipelineContent -Needle 'dotnet build DataImportExportManager.sln --configuration $(BuildConfiguration) --no-restore' -Description 'pipeline build step' -Failures $failures
    Assert-Contains -Content $pipelineContent -Needle 'dotnet test DataImportExportManager.sln --configuration $(BuildConfiguration) --no-build --verbosity normal' -Description 'pipeline test step' -Failures $failures
    Assert-Contains -Content $pipelineContent -Needle '- stage: Deliver' -Description 'pipeline Deliver stage' -Failures $failures
    Assert-Contains -Content $pipelineContent -Needle "eq(variables['Build.SourceBranch'], 'refs/heads/master')" -Description 'pipeline master delivery condition' -Failures $failures
    Assert-Contains -Content $pipelineContent -Needle 'dotnet pack DataImportExportManager.csproj' -Description 'pipeline pack step' -Failures $failures
    Assert-Contains -Content $pipelineContent -Needle 'task: PublishPipelineArtifact@1' -Description 'pipeline artifact publish step' -Failures $failures

    if ($statusMatrixContent -notmatch '- Tests: `\d+/\d+` passing') {
        $failures.Add('Missing or invalid tests baseline format in repository modernization status matrix.')
    }
}

if ($failures.Count -gt 0) {
    Write-Host 'Documentation/pipeline contract validation failed:' -ForegroundColor Red
    foreach ($failure in $failures) {
        Write-Host " - $failure" -ForegroundColor Red
    }

    exit 1
}

Write-Host 'Documentation/pipeline contract validation passed.' -ForegroundColor Green
