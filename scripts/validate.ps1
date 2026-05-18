param(
    [switch]$Pack
)

$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$solution = Get-ChildItem -Path $root -Filter *.sln | Select-Object -First 1

if (-not $solution)
{
    Write-Error 'No solution file found in repository root.'
    exit 1
}

Write-Host "Restoring $($solution.Name)..."
dotnet restore $solution.FullName
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Building $($solution.Name)..."
dotnet build $solution.FullName --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$testProjects = Get-ChildItem -Path $root -Recurse -File -Include *.Tests.csproj,*Test*.csproj |
    Where-Object { $_.FullName -notlike '*\bin\*' -and $_.FullName -notlike '*\obj\*' }

if ($testProjects)
{
    Write-Host "Testing $($solution.Name)..."
    dotnet test $solution.FullName --configuration Release --no-build --verbosity normal
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
else
{
    Write-Warning 'No test projects were found. Skipping dotnet test for this repository.'
}

if ($Pack)
{
    $artifacts = Join-Path $root 'artifacts'
    New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
    Write-Host "Packing solution outputs to $artifacts..."
    dotnet pack $solution.FullName --configuration Release --no-build --output $artifacts
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host 'Class library validation completed successfully.'
