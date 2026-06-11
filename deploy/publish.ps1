#requires -Version 5.1
<#
.SYNOPSIS
    Build a publish layout for install.ps1 to consume.

.DESCRIPTION
    Publishes the Service, NativeHost and (optionally) WinUI App to win-x64.
    Output layout matches what install.ps1 expects:
        OutputDir\Service\
        OutputDir\NativeHost\
        OutputDir\App\

.PARAMETER OutputDir
    Where to drop the publish trees. Defaults to .\publish (created if missing).

.PARAMETER Configuration
    Release / Debug. Defaults to Release.

.PARAMETER SkipApp
    Skip the WinUI app publish (faster, useful for headless dev boxes).
#>
[CmdletBinding()]
param(
    [string]$OutputDir = (Join-Path (Get-Location) 'publish'),
    [string]$Configuration = 'Release',
    [switch]$SkipApp
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

if (-not (Test-Path -LiteralPath $OutputDir)) {
    New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
}

function Publish {
    param([string]$Project, [string]$Subdir, [string[]]$ExtraArgs)
    $dst = Join-Path $OutputDir $Subdir
    if (Test-Path -LiteralPath $dst) { Remove-Item -LiteralPath $dst -Recurse -Force }
    Write-Host ""
    Write-Host "==> Publishing $Project -> $dst"
    & dotnet publish $Project -c $Configuration -r win-x64 --self-contained false -o $dst @ExtraArgs
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE) for $Project" }
}

Publish -Project (Join-Path $repo 'src\StopMe.Service\StopMe.Service.csproj')       -Subdir 'Service'    -ExtraArgs @()
Publish -Project (Join-Path $repo 'src\StopMe.NativeHost\StopMe.NativeHost.csproj') -Subdir 'NativeHost' -ExtraArgs @()

if (-not $SkipApp) {
    Publish -Project (Join-Path $repo 'src\StopMe.App\StopMe.App.csproj') -Subdir 'App' -ExtraArgs @('-p:Platform=x64')
}

Write-Host ""
Write-Host "DONE -> $OutputDir"
Write-Host "Next: deploy\install.ps1 -SourceDir `"$OutputDir`" -ChromeExtensionId <id>"
