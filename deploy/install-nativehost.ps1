[CmdletBinding()]
param(
    [Parameter(Mandatory=$true, HelpMessage="Absolute path to StopMe.NativeHost.exe")]
    [string]$ExePath,

    [Parameter(HelpMessage="Chrome extension id (32 lowercase letters)")]
    [string]$ChromeExtensionId,

    [Parameter(HelpMessage="Edge extension id (32 lowercase letters)")]
    [string]$EdgeExtensionId,

    [Parameter(HelpMessage="Directory where the manifest JSON files will be written")]
    [string]$ManifestDir = "$env:LOCALAPPDATA\StopMe\nativehost"
)

# Registers the com.stopme.nativehost manifest for Chrome and/or Edge under the current
# user. Run from a normal (non-elevated) shell — HKCU writes don't require admin.

$ErrorActionPreference = 'Stop'
$HostName = 'com.stopme.nativehost'

if (-not (Test-Path -LiteralPath $ExePath)) {
    throw "ExePath not found: $ExePath"
}

if (-not (Test-Path -LiteralPath $ManifestDir)) {
    New-Item -ItemType Directory -Force -Path $ManifestDir | Out-Null
}

function Install-Browser {
    param(
        [string]$BrowserName,
        [string]$RegistryRoot,
        [string]$ExtensionId,
        [string]$ManifestTemplate
    )

    if ([string]::IsNullOrWhiteSpace($ExtensionId)) {
        Write-Host "Skipping $BrowserName (no extension id provided)."
        return
    }

    $manifestPath = Join-Path $ManifestDir "$HostName.$BrowserName.json"

    $manifest = @{
        name = $HostName
        description = 'StopMe Native Messaging Host'
        path = $ExePath
        type = 'stdio'
        allowed_origins = @("chrome-extension://$ExtensionId/")
    } | ConvertTo-Json -Depth 4

    $manifest | Out-File -FilePath $manifestPath -Encoding ascii -Force
    Write-Host "[$BrowserName] wrote manifest to $manifestPath"

    $key = "$RegistryRoot\$HostName"
    if (-not (Test-Path -LiteralPath $key)) {
        New-Item -Path $key -Force | Out-Null
    }
    Set-ItemProperty -LiteralPath $key -Name '(default)' -Value $manifestPath
    Write-Host "[$BrowserName] registered $key"
}

Install-Browser -BrowserName 'chrome' -RegistryRoot 'HKCU:\Software\Google\Chrome\NativeMessagingHosts' -ExtensionId $ChromeExtensionId -ManifestTemplate $null
Install-Browser -BrowserName 'edge'   -RegistryRoot 'HKCU:\Software\Microsoft\Edge\NativeMessagingHosts' -ExtensionId $EdgeExtensionId  -ManifestTemplate $null
Install-Browser -BrowserName 'brave'  -RegistryRoot 'HKCU:\Software\BraveSoftware\Brave-Browser\NativeMessagingHosts' -ExtensionId $ChromeExtensionId -ManifestTemplate $null

Write-Host ''
Write-Host 'Done. Reload the extension in your browser; the popup should now report Connected.'
