param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$statusPath = Join-Path $repoRoot 'docs\standards\repository-modernization-status-matrix.md'
$failures = [System.Collections.Generic.List[string]]::new()

if (-not (Test-Path $statusPath)) {
    $failures.Add("Required file not found: $statusPath")
}
else {
    $content = Get-Content -Path $statusPath -Raw

    $lastUpdatedMatch = [regex]::Match($content, '- Last updated: `(?<date>\d{4}-\d{2}-\d{2})`')
    if (-not $lastUpdatedMatch.Success) {
        $failures.Add("Missing or invalid 'Last updated' metadata format. Expected: - Last updated: ``YYYY-MM-DD``")
    }
    else {
        $recordedDate = [datetime]::ParseExact($lastUpdatedMatch.Groups['date'].Value, 'yyyy-MM-dd', $null)
        $daysSinceUpdate = ([datetime]::UtcNow - $recordedDate).Days
        if ($daysSinceUpdate -gt 90) {
            $failures.Add("'Last updated' date is $daysSinceUpdate days old. Status matrix must be reviewed at least every 90 days.")
        }
    }

    if ($content.IndexOf('## Staleness Audit Checkpoint', [StringComparison]::Ordinal) -lt 0) {
        $failures.Add("Missing required section: '## Staleness Audit Checkpoint'.")
    }
}

if ($failures.Count -gt 0) {
    Write-Host 'Modernization status metadata validation failed:' -ForegroundColor Red
    foreach ($failure in $failures) {
        Write-Host " - $failure" -ForegroundColor Red
    }
    exit 1
}

Write-Host 'Modernization status metadata validation passed.' -ForegroundColor Green