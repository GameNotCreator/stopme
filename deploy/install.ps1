#requires -Version 5.1
<#
.SYNOPSIS
    Install StopMe Windows service + UI + Native Messaging host on this machine.

.DESCRIPTION
    Copies a published build of StopMe.Service / StopMe.NativeHost / StopMe.App to
    C:\Program Files\StopMe, creates %ProgramData%\StopMe, hardens the data
    directory ACL, registers the Windows service with auto-start, and (when
    extension IDs are provided) writes the Native Messaging manifest under HKLM
    so all Windows users on this box can talk to the same service.

    Run from an elevated PowerShell prompt. Does NOT touch source files.

.PARAMETER SourceDir
    Folder containing a published "win-x64" build of StopMe. The script expects:
        SourceDir\Service\StopMe.Service.exe
        SourceDir\NativeHost\StopMe.NativeHost.exe
        SourceDir\App\StopMe.App.exe   (optional; only if you built the WinUI app)

.PARAMETER InstallDir
    Target install root. Defaults to "C:\Program Files\StopMe".

.PARAMETER DataDir
    Target data directory (DB, logs, auth.token). Defaults to "$env:ProgramData\StopMe".

.PARAMETER ChromeExtensionId
.PARAMETER EdgeExtensionId
.PARAMETER BraveExtensionId
    32-letter unpacked-extension IDs. When omitted the corresponding browser is
    skipped — you can re-run with -SkipService if you only need to register one
    of them later.

.PARAMETER SkipService
    Only run Native Messaging registration. Service install untouched.

.PARAMETER SkipNativeHost
    Only install the service. Native Messaging registration is skipped.

.EXAMPLE
    pwsh deploy\install.ps1 -SourceDir .\publish -ChromeExtensionId aaaabbbbccccdddd...
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SourceDir,

    [string]$InstallDir = "$env:ProgramFiles\StopMe",
    [string]$DataDir    = "$env:ProgramData\StopMe",

    [string]$ChromeExtensionId,
    [string]$EdgeExtensionId,
    [string]$BraveExtensionId,

    [switch]$SkipService,
    [switch]$SkipNativeHost
)

$ErrorActionPreference = 'Stop'
$ServiceName = 'StopMeService'
$NativeHostName = 'com.stopme.nativehost'

function Assert-Admin {
    $identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object System.Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "install.ps1 must be run from an elevated PowerShell prompt."
    }
}

function Copy-Tree {
    param([string]$From, [string]$To)
    if (-not (Test-Path -LiteralPath $From)) { return $false }
    if (-not (Test-Path -LiteralPath $To))   { New-Item -ItemType Directory -Force -Path $To | Out-Null }
    Copy-Item -Path (Join-Path $From '*') -Destination $To -Recurse -Force
    return $true
}

function Harden-DataDirAcl {
    param([string]$Path)
    # Admins + SYSTEM full control; Users read & execute (so the WinUI app can read auth.token).
    # NOTE: this is a sensible default; production deployments wanting per-user isolation
    # can rerun with a tighter ACL after install.
    $acl = Get-Acl -LiteralPath $Path
    $acl.SetAccessRuleProtection($true, $false)

    foreach ($rule in $acl.Access) { [void]$acl.RemoveAccessRule($rule) }

    $rules = @(
        New-Object System.Security.AccessControl.FileSystemAccessRule(
            'BUILTIN\Administrators', 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow'),
        New-Object System.Security.AccessControl.FileSystemAccessRule(
            'NT AUTHORITY\SYSTEM',    'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow'),
        New-Object System.Security.AccessControl.FileSystemAccessRule(
            'BUILTIN\Users',          'ReadAndExecute', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
    )
    foreach ($rule in $rules) { $acl.AddAccessRule($rule) }

    Set-Acl -LiteralPath $Path -AclObject $acl
}

function Install-Service {
    param([string]$ExePath)

    if (-not (Test-Path -LiteralPath $ExePath)) {
        throw "Service exe not found at '$ExePath'. Did you publish StopMe.Service?"
    }

    # Stop + delete an older registration if present.
    $existing = & sc.exe query $ServiceName 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Stopping existing $ServiceName ..."
        & sc.exe stop $ServiceName | Out-Null
        Start-Sleep -Seconds 2
        Write-Host "Removing existing $ServiceName ..."
        & sc.exe delete $ServiceName | Out-Null
        Start-Sleep -Seconds 1
    }

    Write-Host "Registering $ServiceName ..."
    # Embed --data-dir into the service binPath so a non-default $DataDir
    # actually reaches the service. Previously the installer logged a fake
    # $DataDir but the service still defaulted to %ProgramData%\StopMe.
    $binPath = "`"$ExePath`" --data-dir `"$DataDir`""
    & sc.exe create $ServiceName binPath= $binPath start= auto DisplayName= "StopMe Focus Service" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "sc.exe create failed (exit $LASTEXITCODE)." }

    & sc.exe description $ServiceName "Enforces StopMe focus sessions: blocks distracting websites and applications, exposes the local IPC API." | Out-Null
    & sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/120000/restart/300000 | Out-Null

    Write-Host "Starting $ServiceName ..."
    & sc.exe start $ServiceName | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "sc.exe start returned $LASTEXITCODE. Inspect Event Viewer and $DataDir\Logs."
    }
}

function Register-NativeHost {
    param([string]$ExePath, [string]$Browser, [string]$RegistryRoot, [string]$ExtensionId)

    if ([string]::IsNullOrWhiteSpace($ExtensionId)) {
        Write-Host "[$Browser] no extension id supplied, skipping."
        return
    }

    $manifestDir = Join-Path $InstallDir 'nativehost'
    if (-not (Test-Path -LiteralPath $manifestDir)) {
        New-Item -ItemType Directory -Force -Path $manifestDir | Out-Null
    }

    $manifestPath = Join-Path $manifestDir "$NativeHostName.$Browser.json"
    $manifest = @{
        name = $NativeHostName
        description = 'StopMe Native Messaging Host'
        path = $ExePath
        type = 'stdio'
        allowed_origins = @("chrome-extension://$ExtensionId/")
    } | ConvertTo-Json -Depth 4

    $manifest | Out-File -FilePath $manifestPath -Encoding ascii -Force
    Write-Host "[$Browser] wrote manifest to $manifestPath"

    $key = "$RegistryRoot\$NativeHostName"
    if (-not (Test-Path -LiteralPath $key)) {
        New-Item -Path $key -Force | Out-Null
    }
    Set-ItemProperty -LiteralPath $key -Name '(default)' -Value $manifestPath
    Write-Host "[$Browser] registered $key"
}

# ------------------------------------------------------------------ run

Assert-Admin

Write-Host "StopMe installer"
Write-Host "----------------"
Write-Host "Source : $SourceDir"
Write-Host "Install: $InstallDir"
Write-Host "Data   : $DataDir"
Write-Host ""

# Ensure source folders exist (App is optional).
$serviceSrc    = Join-Path $SourceDir 'Service'
$nativeHostSrc = Join-Path $SourceDir 'NativeHost'
$appSrc        = Join-Path $SourceDir 'App'

if (-not (Test-Path -LiteralPath $serviceSrc) -and -not $SkipService) {
    throw "Service source not found at '$serviceSrc'. Either publish it or pass -SkipService."
}
if (-not (Test-Path -LiteralPath $nativeHostSrc) -and -not $SkipNativeHost) {
    throw "NativeHost source not found at '$nativeHostSrc'. Either publish it or pass -SkipNativeHost."
}

# Layout target directories.
$serviceDst    = Join-Path $InstallDir 'Service'
$nativeHostDst = Join-Path $InstallDir 'NativeHost'
$appDst        = Join-Path $InstallDir 'App'

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null

if (-not $SkipService) {
    Write-Host "Copying service files ..."
    [void](Copy-Tree -From $serviceSrc -To $serviceDst)
}
if (-not $SkipNativeHost) {
    Write-Host "Copying native host files ..."
    [void](Copy-Tree -From $nativeHostSrc -To $nativeHostDst)
}
$appCopied = Copy-Tree -From $appSrc -To $appDst
if ($appCopied) { Write-Host "Copied WinUI app to $appDst" }

# Data directory (DB, logs, auth token).
New-Item -ItemType Directory -Force -Path $DataDir | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $DataDir 'Logs') | Out-Null
Write-Host "Tightening ACL on $DataDir ..."
Harden-DataDirAcl -Path $DataDir

# Service install.
if (-not $SkipService) {
    Install-Service -ExePath (Join-Path $serviceDst 'StopMe.Service.exe')
}

# Native messaging registration (HKLM so the entry is per-machine, not per-user).
if (-not $SkipNativeHost) {
    $nativeExe = Join-Path $nativeHostDst 'StopMe.NativeHost.exe'
    if (-not (Test-Path -LiteralPath $nativeExe)) {
        Write-Warning "Native host exe missing at $nativeExe — skipping Chrome/Edge/Brave registration."
    } else {
        Register-NativeHost -ExePath $nativeExe -Browser 'chrome' -RegistryRoot 'HKLM:\Software\Google\Chrome\NativeMessagingHosts'             -ExtensionId $ChromeExtensionId
        Register-NativeHost -ExePath $nativeExe -Browser 'edge'   -RegistryRoot 'HKLM:\Software\Microsoft\Edge\NativeMessagingHosts'            -ExtensionId $EdgeExtensionId
        Register-NativeHost -ExePath $nativeExe -Browser 'brave'  -RegistryRoot 'HKLM:\Software\BraveSoftware\Brave-Browser\NativeMessagingHosts' -ExtensionId $BraveExtensionId
    }
}

Write-Host ""
Write-Host "DONE"
Write-Host "  Service     : $ServiceName ($(if($SkipService){'skipped'}else{'installed'}))"
Write-Host "  Install dir : $InstallDir"
Write-Host "  Data dir    : $DataDir"
Write-Host "  Logs        : $DataDir\Logs"
Write-Host ""
Write-Host "Run 'deploy\check-install.ps1' to validate, or open StopMe.App.exe."
