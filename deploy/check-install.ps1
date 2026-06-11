#requires -Version 5.1
<#
.SYNOPSIS
    Validate that StopMe is installed and the local pieces are wired correctly.

.DESCRIPTION
    Doesn't change anything. Prints PASS/FAIL/WARN per item:
        - Service registered + running
        - Service exe present
        - NativeHost exe present
        - NativeHost manifest + registry under HKLM (Chrome / Edge / Brave)
        - %ProgramData%\StopMe\stopme.db present
        - %ProgramData%\StopMe\auth.token present
        - Recent log file written in last 5 minutes
        - LocalApi pipe answers `status`

    Returns exit code 0 when every required check passes, 1 otherwise.
#>
[CmdletBinding()]
param(
    [string]$InstallDir = "$env:ProgramFiles\StopMe",
    [string]$DataDir    = "$env:ProgramData\StopMe"
)

$ServiceName = 'StopMeService'
$NativeHostName = 'com.stopme.nativehost'
$failed = 0
$warned = 0

function Result {
    param([string]$Level, [string]$Message)
    $tag = switch ($Level) {
        'PASS' { '[PASS]' }
        'FAIL' { '[FAIL]' ; $script:failed++ }
        'WARN' { '[WARN]' ; $script:warned++ }
        default { "[$Level]" }
    }
    Write-Host "$tag $Message"
}

# 1. Service.
$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($null -eq $svc) {
    Result 'FAIL' "Windows service '$ServiceName' is not installed."
} else {
    if ($svc.Status -eq 'Running') {
        Result 'PASS' "Windows service '$ServiceName' is running (StartType: $($svc.StartType))."
    } else {
        Result 'FAIL' "Windows service '$ServiceName' is $($svc.Status) (StartType: $($svc.StartType))."
    }
}

# 2. Binaries.
$serviceExe    = Join-Path $InstallDir 'Service\StopMe.Service.exe'
$nativeHostExe = Join-Path $InstallDir 'NativeHost\StopMe.NativeHost.exe'

if (Test-Path -LiteralPath $serviceExe) { Result 'PASS' "Service exe at $serviceExe" }
else { Result 'FAIL' "Service exe missing: $serviceExe" }

if (Test-Path -LiteralPath $nativeHostExe) { Result 'PASS' "NativeHost exe at $nativeHostExe" }
else { Result 'FAIL' "NativeHost exe missing: $nativeHostExe" }

# 3. Data dir.
if (Test-Path -LiteralPath $DataDir) {
    Result 'PASS' "Data dir present: $DataDir"

    $db = Join-Path $DataDir 'stopme.db'
    if (Test-Path -LiteralPath $db) { Result 'PASS' "SQLite DB present: $db" }
    else { Result 'WARN' "SQLite DB missing (will be created on first service run): $db" }

    $token = Join-Path $DataDir 'auth.token'
    if (Test-Path -LiteralPath $token) { Result 'PASS' "Auth token present: $token" }
    else { Result 'WARN' "Auth token missing (will be created on first service run): $token" }

    $logs = Join-Path $DataDir 'Logs'
    if (Test-Path -LiteralPath $logs) {
        $latest = Get-ChildItem -LiteralPath $logs -Filter 'stopme-service-*.log' -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTimeUtc -Descending |
            Select-Object -First 1
        if ($null -eq $latest) {
            Result 'WARN' "No service log file in $logs yet."
        } elseif (($null -ne $svc) -and $svc.Status -eq 'Running' -and (((Get-Date) - $latest.LastWriteTime).TotalMinutes -gt 5)) {
            Result 'WARN' "Latest log $($latest.Name) hasn't been written in the last 5 minutes."
        } else {
            Result 'PASS' "Latest log: $($latest.FullName)"
        }
    } else {
        Result 'WARN' "Log dir missing: $logs"
    }
} else {
    Result 'FAIL' "Data dir missing: $DataDir"
}

# 4. Native Messaging registration (per-machine).
foreach ($entry in @(
    @{ Name='chrome'; Root='HKLM:\Software\Google\Chrome\NativeMessagingHosts' },
    @{ Name='edge';   Root='HKLM:\Software\Microsoft\Edge\NativeMessagingHosts' },
    @{ Name='brave';  Root='HKLM:\Software\BraveSoftware\Brave-Browser\NativeMessagingHosts' }
)) {
    $key = Join-Path $entry.Root $NativeHostName
    if (Test-Path -LiteralPath $key) {
        $path = (Get-ItemProperty -LiteralPath $key).'(default)'
        if (Test-Path -LiteralPath $path) {
            Result 'PASS' "Native host registered for $($entry.Name): $path"
        } else {
            Result 'WARN' "Native host registered for $($entry.Name) but manifest missing: $path"
        }
    } else {
        Result 'WARN' "Native host NOT registered for $($entry.Name) ($key)."
    }
}

# 5. Pipe smoke.
$tokenPath = Join-Path $DataDir 'auth.token'
if (Test-Path -LiteralPath $tokenPath) {
    try {
        $token = (Get-Content -LiteralPath $tokenPath -Raw).Trim()
        $client = New-Object System.IO.Pipes.NamedPipeClientStream('.', 'StopMe.LocalApi.v1',
            [System.IO.Pipes.PipeDirection]::InOut, [System.IO.Pipes.PipeOptions]::Asynchronous)
        $client.Connect(2000)
        $req = @{ version = 1; op = 'status'; token = $token } | ConvertTo-Json -Compress
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($req)
        $client.Write([BitConverter]::GetBytes([int]$bytes.Length), 0, 4)
        $client.Write($bytes, 0, $bytes.Length)
        $client.Flush()

        $lenBuf = New-Object byte[] 4
        if ($client.Read($lenBuf, 0, 4) -lt 4) {
            Result 'FAIL' 'LocalApi pipe: short read (header).'
        } else {
            $n = [BitConverter]::ToInt32($lenBuf, 0)
            if ($n -le 0 -or $n -gt 8388608) {
                Result 'FAIL' "LocalApi pipe: bad envelope size $n"
            } else {
                $buf = New-Object byte[] $n
                $off = 0
                while ($off -lt $n) {
                    $k = $client.Read($buf, $off, $n - $off)
                    if ($k -le 0) { break }
                    $off += $k
                }
                $json = [System.Text.Encoding]::UTF8.GetString($buf, 0, $off) | ConvertFrom-Json
                if ($json.success) {
                    Result 'PASS' "LocalApi pipe answered status (service version: $($json.payload.serviceVersion))."
                } else {
                    Result 'FAIL' "LocalApi pipe error: $($json.errorCode) — $($json.errorMessage)"
                }
            }
        }
        $client.Dispose()
    } catch {
        Result 'FAIL' "LocalApi pipe unreachable: $($_.Exception.Message)"
    }
} else {
    Result 'WARN' "Skipping pipe smoke (no auth.token at $tokenPath)."
}

Write-Host ""
if ($failed -gt 0) {
    Write-Host "FAILED: $failed required check(s) failed, $warned warning(s)."
    exit 1
} else {
    Write-Host "OK: 0 required checks failed, $warned warning(s)."
    exit 0
}
