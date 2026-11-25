[CmdletBinding()]
param([string]$KeyTool = 'keytool')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$directory = Join-Path $repo '.local/android-signing'
$keystore = Join-Path $directory 'corps-royaux-release.keystore'
$passwordFile = Join-Path $directory 'keystore-password.txt'
if ((Test-Path -LiteralPath $keystore) -or (Test-Path -LiteralPath $passwordFile)) {
    throw 'Signing material already exists. Preserve it; never regenerate an existing release identity.'
}
New-Item -ItemType Directory -Force $directory | Out-Null
if ($IsWindows -or $env:OS -eq 'Windows_NT') {
    $sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    & icacls $directory /inheritance:r /grant:r "*${sid}:(OI)(CI)F" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not restrict signing directory access.' }
}
$oldPassword = $env:CR2026_ANDROID_KEYTOOL_PASSWORD
try {
    $bytes = New-Object byte[] 48
    [Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    $env:CR2026_ANDROID_KEYTOOL_PASSWORD = [Convert]::ToBase64String($bytes)
    & $KeyTool -genkeypair -keystore $keystore -storetype PKCS12 -alias corpsroyaux -keyalg RSA -keysize 3072 -validity 10000 -dname 'CN=Corps Royaux, O=Studio Logiklik, C=CA' -storepass:env CR2026_ANDROID_KEYTOOL_PASSWORD -keypass:env CR2026_ANDROID_KEYTOOL_PASSWORD
    if ($LASTEXITCODE -ne 0) { throw 'Release key generation failed.' }
    [IO.File]::WriteAllText($passwordFile, $env:CR2026_ANDROID_KEYTOOL_PASSWORD)
} finally { $env:CR2026_ANDROID_KEYTOOL_PASSWORD = $oldPassword }
$settingsPath = Join-Path $repo '.local/android-client.json'
$settings = @{}
if (Test-Path -LiteralPath $settingsPath) {
    (Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json).PSObject.Properties | ForEach-Object { $settings[$_.Name] = $_.Value }
}
$settings.SigningKeyStore = $keystore
$settings.SigningKeyAlias = 'corpsroyaux'
$settings.SigningPasswordFile = $passwordFile
$settings | ConvertTo-Json | Set-Content -LiteralPath $settingsPath -Encoding utf8
@'
PRIVATE: back up this entire directory in secure offline storage.
Keep the keystore AND its password to sign future updates. Never send these files
to players, commit them, or place them inside client resources/distribution ZIPs.
The password is stored here for unattended local builds; directory access is
restricted to the Windows account that generated it.
'@ | Set-Content -LiteralPath (Join-Path $directory 'KEEP-PRIVATE.txt') -Encoding utf8
Write-Host "Permanent release signing configured. Secure backup required: $directory"
