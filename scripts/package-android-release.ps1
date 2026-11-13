[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Apk,
    [Parameter(Mandatory)][int]$VersionCode,
    [Parameter(Mandatory)][string]$NetworkPublicKey,
    [ValidateSet('android-arm64','android-x64')][string]$Runtime = 'android-arm64',
    [string]$Version = '0.8.0'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$repo = Split-Path $PSScriptRoot -Parent
$apkPath = (Get-Item -LiteralPath $Apk).FullName
$zip = [IO.Compression.ZipFile]::OpenRead($apkPath)
try {
    $names = @($zip.Entries | ForEach-Object FullName)
    $assets = @($names | Where-Object { $_.StartsWith('assets/resources/') -and !$_.EndsWith('/') })
    if ($assets.Count -lt 8000) { throw 'Full player resources are missing.' }
    if (@($names | Group-Object | Where-Object Count -gt 1).Count) { throw 'Duplicate APK entries.' }
    if ($names | Where-Object { $_ -match '(?i)(\.keystore|\.jks|\.pfx|\.pem|\.bkey|\.db(?:-wal|-shm)?|translation-service\.private\.json)$' }) {
        throw 'Private files or database caches found in APK.'
    }
    if (!($names | Where-Object { $_ -match '^res/.*/ic_launcher' })) { throw 'Launcher icon missing.' }
    $entry = $zip.GetEntry('assets/resources/config.json')
    if (!$entry) { throw 'Player config missing.' }
    $reader = [IO.StreamReader]::new($entry.Open())
    try { $config = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    if (!$config.Host -or $config.Host -in @('localhost','127.0.0.1','::1')) { throw 'Player config is not configured for a remote server.' }
} finally { $zip.Dispose() }
$architecture = if ($Runtime -eq 'android-arm64') { 'arm64-v8a' } else { 'x86_64' }
$directory = Join-Path $repo "artifacts/android/distribution/CR2026-Android-$Version-$VersionCode-$Runtime"
if (Test-Path -LiteralPath $directory) { throw 'Release folder already exists; increase versionCode.' }
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$name = "CR2026-Android-$Version-$VersionCode-$Runtime.apk"
$target = Join-Path $directory $name
Copy-Item -LiteralPath $apkPath -Destination $target
$sha = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
"$sha  $name" | Set-Content -LiteralPath (Join-Path $directory 'SHA256SUMS.txt') -Encoding ascii
$manifest = [ordered]@{
    ApplicationId = 'com.logiklik.corpsroyaux'; Version = $Version; VersionCode = $VersionCode
    Architecture = $architecture; MinimumAndroidApi = 23; BuiltUtc = [DateTime]::UtcNow.ToString('o')
    Apk = $name; Bytes = (Get-Item -LiteralPath $target).Length; Sha256 = $sha
    ResourceFiles = $assets.Count; ServerHost = $config.Host
    NetworkPublicKeySha256 = (Get-FileHash -LiteralPath $NetworkPublicKey -Algorithm SHA256).Hash
    Signing = 'Permanent Corps Royaux release key'
}
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $directory 'release.json') -Encoding utf8
@'
CORPS ROYAUX — ANDROID

FR : Android 64 bits (ARM64), Android 6 ou plus récent. Téléchargez l’APK sur le
téléphone, ouvrez-le et autorisez temporairement cette source d’installation si
Android le demande. Prévoyez environ 2 Go libres pour le paquet et les ressources.
Connectez-vous avec votre compte de jeu habituel. Pour mettre à jour une version
de distribution, installez le nouvel APK par-dessus, sans désinstaller le jeu.
Les anciennes versions de diagnostic utilisent une autre signature : elles
doivent être désinstallées avant la première version de distribution. Cela efface
les réglages locaux; personnages et progression restent sur le serveur.
Cette version se distribue directement par APK; elle n’est pas publiée sur Google Play.

EN: ARM64 Android, Android 6 or newer. Download and open the APK on your phone.
Temporarily allow installation from that source if Android asks. Allow about
2 GB free for the package and extracted resources. Use your regular game account.
Install future distribution APKs over this one without uninstalling. Older
diagnostic builds have a different signature and must first be uninstalled;
local settings are removed, while characters and progress remain on the server.
Direct APK distribution; this app is not published on Google Play.

ES: Android ARM64, Android 6 o posterior. Descarga y abre el APK en el teléfono.
Permite temporalmente la instalación desde esa fuente si Android lo solicita.
Reserva unos 2 GB libres para el paquete y sus recursos. Usa tu cuenta habitual.
Instala las futuras versiones de distribución sin desinstalar. Las versiones de
diagnóstico tienen otra firma y deben desinstalarse primero; se borran los ajustes
locales, pero los personajes y el progreso permanecen en el servidor.
Distribución directa por APK; la aplicación no está publicada en Google Play.
'@ | Set-Content -LiteralPath (Join-Path $directory 'INSTALLATION.txt') -Encoding utf8
if ($Runtime -eq 'android-x64') {
    $textPath = Join-Path $directory 'INSTALLATION.txt'
    $text = [IO.File]::ReadAllText($textPath)
    $text = "FR : Version x86_64 pour émulateurs/appareils Intel, incompatible avec les téléphones ARM64 comme le Pixel. / EN: x86_64 emulator/Intel build; not for ARM64 phones. / ES: Versión x86_64 para emuladores/Intel; no es para teléfonos ARM64.`n`n" + $text.Replace('ARM64','x86_64')
    [IO.File]::WriteAllText($textPath, $text)
}
$notice = Join-Path $repo 'Intersect.Client/LICENSE.md'
if (Test-Path -LiteralPath $notice) { Copy-Item -LiteralPath $notice -Destination (Join-Path $directory 'LICENSE.md') }
$archive = "$directory.zip"
[IO.Compression.ZipFile]::CreateFromDirectory($directory, $archive, [IO.Compression.CompressionLevel]::NoCompression, $false)
Write-Host "Player APK: $target"
Write-Host "Distribution ZIP: $archive"
