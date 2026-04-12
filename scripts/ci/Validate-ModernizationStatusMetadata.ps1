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
        $failures.Add("Missing or invalid 'Last updated' metadata format. Expected YYYY-MM-DD.")
    }

    if ($content.IndexOf('## Staleness Audit Checkpoint', [StringComparison]::Ordinal) -lt 0) {
        $failures.Add("Missing required section: '## Staleness Audit Checkpoint'.")
    }

    $commitMatch = [regex]::Match($content, '- Latest commit: `(?<commit>[0-9a-fA-F]+)`')
    if (-not $commitMatch.Success) {
        $failures.Add("Missing or invalid 'Latest commit' metadata format.")
    }
    else {
        $documentCommit = $commitMatch.Groups['commit'].Value.ToLowerInvariant()
        $headCommit = (& git rev-parse --short HEAD).Trim().ToLowerInvariant()

        $allowed = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
        $allowed.Add($headCommit) | Out-Null

        try {
            $parentCommit = (& git rev-parse --short HEAD~1 2>$null).Trim().ToLowerInvariant()
            if (-not [string]::IsNullOrWhiteSpace($parentCommit)) {
                $allowed.Add($parentCommit) | Out-Null
            }
        }
        catch {
            # Repository may not have a parent commit yet; ignore.
        }

        if (-not $allowed.Contains($documentCommit)) {
            $failures.Add("Stale 'Latest commit' metadata: found '$documentCommit', expected one of: $($allowed -join ', ').")
        }
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
