#requires -Version 5.1
<#
.SYNOPSIS
    Remove the StopMe Windows service, Native Messaging registration, and
    optionally the program files and local data.

.DESCRIPTION
    Refuses to run while a strict-mode session is currently active (queried via
    the local IPC pipe). Pass -Force to bypass the strict-mode safety net (you
    should not need this outside of recovery scenarios).

.PARAMETER InstallDir
.PARAMETER DataDir
.PARAMETER RemoveData
    When set, also deletes %ProgramData%\StopMe (DB, logs, auth.token).
    OFF by default — your stats and blocklists stay around for the next install.

.PARAMETER Force
    Skip the active-strict-session check. Use only in recovery.
#>
[CmdletBinding()]
param(
    [string]$InstallDir = "$env:ProgramFiles\StopMe",
    [string]$DataDir    = "$env:ProgramData\StopMe",
    [switch]$RemoveData,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$ServiceName = 'StopMeService'
$NativeHostName = 'com.stopme.nativehost'

function Assert-Admin {
    $identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object System.Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "uninstall.ps1 must be run from an elevated PowerShell prompt."
    }
}

function Test-StrictSessionActive {
    # Best-effort: read auth.token from the data dir and ask the service whether
    # any active session is strict. If the service is already down we treat it
    # as "no strict session" — there is nothing to enforce anyway.
    $tokenPath = Join-Path $DataDir 'auth.token'
    if (-not (Test-Path -LiteralPath $tokenPath)) { return $false }

    $pipeName = 'StopMe.LocalApi.v1'
    try {
        $token = (Get-Content -LiteralPath $tokenPath -Raw).Trim()
    } catch {
        return $false
    }

    try {
        $client = New-Object System.IO.Pipes.NamedPipeClientStream('.', $pipeName,
            [System.IO.Pipes.PipeDirection]::InOut,
            [System.IO.Pipes.PipeOptions]::Asynchronous)
        $client.Connect(2000)
    } catch {
        return $false
    }

    try {
        $request = @{
            version = 1
            op = 'sessions.active'
            token = $token
        } | ConvertTo-Json -Depth 5 -Compress

        $bytes = [System.Text.Encoding]::UTF8.GetBytes($request)
        $lenBytes = [BitConverter]::GetBytes([int]$bytes.Length)
        $client.Write($lenBytes, 0, 4)
        $client.Write($bytes, 0, $bytes.Length)
        $client.Flush()

        $lenBuf = New-Object byte[] 4
        $read = $client.Read($lenBuf, 0, 4)
        if ($read -lt 4) { return $false }
        $respLen = [BitConverter]::ToInt32($lenBuf, 0)
        if ($respLen -le 0 -or $respLen -gt 8388608) { return $false }
        $respBuf = New-Object byte[] $respLen
        $off = 0
        while ($off -lt $respLen) {
            $n = $client.Read($respBuf, $off, $respLen - $off)
            if ($n -le 0) { break }
            $off += $n
        }
        $json = [System.Text.Encoding]::UTF8.GetString($respBuf, 0, $off) | ConvertFrom-Json
        if (-not $json.success) { return $false }
        foreach ($s in @($json.payload)) {
            if ($null -ne $s -and $s.mode -eq 'Strict') { return $true }
        }
        return $false
    } catch {
        return $false
    } finally {
        $client.Dispose()
    }
}

function Stop-And-Delete-Service {
    $existing = & sc.exe query $ServiceName 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Service $ServiceName not registered."
        return
    }

    Write-Host "Stopping $ServiceName ..."
    & sc.exe stop $ServiceName | Out-Null
    Start-Sleep -Seconds 2

    Write-Host "Removing $ServiceName ..."
    & sc.exe delete $ServiceName | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "sc.exe delete failed (exit $LASTEXITCODE)."
    }
}

function Unregister-NativeHost {
    # HKLM (added by install.ps1)
    foreach ($root in @(
        'HKLM:\Software\Google\Chrome\NativeMessagingHosts',
        'HKLM:\Software\Microsoft\Edge\NativeMessagingHosts',
        'HKLM:\Software\BraveSoftware\Brave-Browser\NativeMessagingHosts'
    )) {
        $key = "$root\$NativeHostName"
        if (Test-Path -LiteralPath $key) {
            Remove-Item -LiteralPath $key -Force -Recurse
            Write-Host "Removed $key"
        }
    }
}

# ------------------------------------------------------------------ run

Assert-Admin

if (-not $Force) {
    if (Test-StrictSessionActive) {
        Write-Host ""
        Write-Host "ABORT: a strict StopMe session is currently active."
        Write-Host "Wait for it to end or re-run with -Force (not recommended)."
        Write-Host ""
        exit 1
    }
}

Stop-And-Delete-Service
Unregister-NativeHost

if (Test-Path -LiteralPath $InstallDir) {
    Write-Host "Removing $InstallDir ..."
    Remove-Item -LiteralPath $InstallDir -Recurse -Force
}

if ($RemoveData) {
    if (Test-Path -LiteralPath $DataDir) {
        Write-Host "Removing data directory $DataDir ..."
        Remove-Item -LiteralPath $DataDir -Recurse -Force
    }
} else {
    Write-Host "Data directory preserved at $DataDir (pass -RemoveData to wipe)."
}

Write-Host ""
Write-Host "DONE"
