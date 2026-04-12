param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$markdownFiles = Get-ChildItem -Path $repoRoot -Recurse -File -Filter *.md
$failures = [System.Collections.Generic.List[string]]::new()

foreach ($file in $markdownFiles) {
    $content = Get-Content -Path $file.FullName -Raw
    $matches = [regex]::Matches($content, '\[[^\]]*\]\((?<url>[^)]+)\)')

    foreach ($match in $matches) {
        $url = $match.Groups['url'].Value.Trim()

        if ([string]::IsNullOrWhiteSpace($url)) {
            continue
        }

        if ($url.StartsWith('http://', [StringComparison]::OrdinalIgnoreCase) -or
            $url.StartsWith('https://', [StringComparison]::OrdinalIgnoreCase) -or
            $url.StartsWith('mailto:', [StringComparison]::OrdinalIgnoreCase) -or
            $url.StartsWith('#', [StringComparison]::Ordinal)) {
            continue
        }

        if ($url.StartsWith('<', [StringComparison]::Ordinal) -or $url.StartsWith('`', [StringComparison]::Ordinal)) {
            continue
        }

        $pathPart = ($url -split '#')[0]
        if ([string]::IsNullOrWhiteSpace($pathPart)) {
            continue
        }

        $resolvedPath = if ($pathPart.StartsWith('/', [StringComparison]::Ordinal) -or $pathPart.StartsWith('\\', [StringComparison]::Ordinal)) {
            Join-Path $repoRoot $pathPart.TrimStart('/','\')
        }
        else {
            Join-Path $file.DirectoryName $pathPart
        }

        if (-not (Test-Path $resolvedPath)) {
            $relativeSource = [System.IO.Path]::GetRelativePath($repoRoot, $file.FullName)
            $failures.Add("$relativeSource -> $url")
        }
    }
}

if ($failures.Count -gt 0) {
    Write-Host 'Markdown link validation failed. Missing local targets:' -ForegroundColor Red
    foreach ($failure in ($failures | Sort-Object -Unique)) {
        Write-Host " - $failure" -ForegroundColor Red
    }

    exit 1
}

Write-Host 'Markdown link validation passed.' -ForegroundColor Green
