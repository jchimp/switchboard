<#
.SYNOPSIS
    Simple release build: publish Switchboard.App and copy it into dist/.

.DESCRIPTION
    Publishes src/Switchboard.App in Release configuration into
    dist/Switchboard-v<version>/. The version comes from -Version, or falls back
    to the <Version> property in Switchboard.App.csproj (or 1.0.0 if unset,
    which is the .NET default).

    Refuses to overwrite an existing dist folder for the same version unless
    -Force is given, so a released build can't be silently replaced.

.EXAMPLE
    .\tools\Release.ps1 -Version 0.3.0

.EXAMPLE
    .\tools\Release.ps1 -Version 0.3.0 -Force
    # rebuild and replace dist\Switchboard-v0.3.0
#>
[CmdletBinding()]
param(
    # Semantic version for the output folder, e.g. 0.3.0. Defaults to the csproj <Version>.
    [ValidatePattern('^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$')]
    [string]$Version,

    # Replace an existing dist folder for this version.
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src\Switchboard.App\Switchboard.App.csproj'

if (-not $Version) {
    $csproj = [xml](Get-Content -LiteralPath $project)
    $node = $csproj.SelectSingleNode('//PropertyGroup/Version')
    $Version = if ($node) { $node.InnerText.Trim() } else { '1.0.0' }
    Write-Host "No -Version given; using $Version from the project." -ForegroundColor Yellow
}

$distDir = Join-Path $repoRoot "dist\Switchboard-v$Version"
if (Test-Path -LiteralPath $distDir) {
    if (-not $Force) {
        throw "dist folder already exists: $distDir (use -Force to replace it)"
    }
    Remove-Item -LiteralPath $distDir -Recurse -Force
}

Write-Host "Publishing Switchboard v$Version (Release)..." -ForegroundColor Cyan
dotnet publish $project -c Release -o $distDir "-p:Version=$Version"
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$size = '{0:N1} MB' -f ((Get-ChildItem -LiteralPath $distDir -Recurse -File |
    Measure-Object Length -Sum).Sum / 1MB)
Write-Host "Release ready: $distDir ($size)" -ForegroundColor Green
