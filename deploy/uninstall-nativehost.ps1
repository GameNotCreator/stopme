[CmdletBinding()]
param(
    [Parameter(HelpMessage="Directory where the manifest JSON files live")]
    [string]$ManifestDir = "$env:LOCALAPPDATA\StopMe\nativehost"
)

$ErrorActionPreference = 'Continue'
$HostName = 'com.stopme.nativehost'

foreach ($root in @(
    'HKCU:\Software\Google\Chrome\NativeMessagingHosts',
    'HKCU:\Software\Microsoft\Edge\NativeMessagingHosts',
    'HKCU:\Software\BraveSoftware\Brave-Browser\NativeMessagingHosts'
)) {
    $key = "$root\$HostName"
    if (Test-Path -LiteralPath $key) {
        Remove-Item -LiteralPath $key -Force
        Write-Host "Removed $key"
    }
}

if (Test-Path -LiteralPath $ManifestDir) {
    Get-ChildItem -LiteralPath $ManifestDir -Filter "$HostName.*.json" -ErrorAction SilentlyContinue |
        Remove-Item -Force
    Write-Host "Cleared manifest files in $ManifestDir"
}
