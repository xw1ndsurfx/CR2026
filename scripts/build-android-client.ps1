[CmdletBinding()]
param(
    [string]$Resources,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [ValidateSet('android-arm64', 'android-x64')][string]$Runtime = 'android-arm64',
    [string]$NetworkPublicKey,
    [string]$OutputDirectory,
    [string]$PackageVersion = '0.8.0-beta',
    [string]$Version = '0.8.0',
    [ValidateRange(0, 2100000000)][int]$VersionCode = 0,
    [switch]$Distribution,
    [string]$SigningKeyStore,
    [string]$SigningKeyAlias,
    [string]$SigningPasswordFile,
    [string]$SigningPasswordEnvironment = 'CR2026_ANDROID_SIGNING_PASSWORD',
    [ValidateSet('quiet', 'minimal', 'normal', 'detailed', 'diagnostic')][string]$Verbosity = 'minimal',
    [switch]$CompileOnly
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repo 'Intersect.Client.Android/Intersect.Client.Android.csproj'
$output = Join-Path $repo "artifacts/android/$Configuration/$Runtime"
if ($OutputDirectory) { $output = [IO.Path]::GetFullPath($OutputDirectory) }
$localSettings = Join-Path $repo '.local/android-client.json'
if (Test-Path -LiteralPath $localSettings) {
    $settings = Get-Content -LiteralPath $localSettings -Raw | ConvertFrom-Json
    if (!$Resources) { $Resources = $settings.Resources }
    if (!$NetworkPublicKey) { $NetworkPublicKey = $settings.NetworkPublicKey }
    if (!$SigningKeyStore) { $SigningKeyStore = $settings.SigningKeyStore }
    if (!$SigningKeyAlias) { $SigningKeyAlias = $settings.SigningKeyAlias }
    if (!$SigningPasswordFile) { $SigningPasswordFile = $settings.SigningPasswordFile }
}

if ($Distribution -and ($CompileOnly -or $Configuration -ne 'Release')) {
    throw 'Distribution requires a full Release APK.'
}
if ($Distribution) {
    if (!$NetworkPublicKey -or !(Test-Path -LiteralPath $NetworkPublicKey)) { throw 'Distribution requires the deployed server public network key.' }
    if (!$SigningKeyStore -or !(Test-Path -LiteralPath $SigningKeyStore) -or !$SigningKeyAlias) {
        throw 'Distribution requires a permanent signing keystore and alias. Run initialize-android-signing.ps1 once.'
    }
    if (!$SigningPasswordFile -and ![Environment]::GetEnvironmentVariable($SigningPasswordEnvironment)) {
        throw 'Provide a signing password file or signing password environment variable.'
    }
}
$counter = Join-Path $repo '.build-versions/Intersect.Client.Android.package.txt'
if (!$CompileOnly) {
    $previous = 14
    if (Test-Path -LiteralPath $counter) { $previous = [Math]::Max($previous, [int](Get-Content -LiteralPath $counter -Raw)) }
    if (!$VersionCode) { $VersionCode = $previous + 1 }
    if ($Distribution -and $VersionCode -le $previous) { throw "Distribution versionCode must exceed $previous." }
    New-Item -ItemType Directory -Force (Split-Path $counter) | Out-Null
    [IO.File]::WriteAllText($counter, [string][Math]::Max($previous, $VersionCode))
} elseif (!$VersionCode) { $VersionCode = 15 }

if (!$CompileOnly) {
    if (!$Resources) { throw 'Specify -Resources with the complete player client resources directory.' }
    $Resources = (Get-Item -LiteralPath $Resources).FullName
    foreach ($required in @('config.json', 'client_strings.json', 'fonts', 'gui')) {
        if (!(Test-Path -LiteralPath (Join-Path $Resources $required))) {
            throw "Missing client resource: $required"
        }
    }
    $config = Get-Content -LiteralPath (Join-Path $Resources 'config.json') -Raw | ConvertFrom-Json
    if (!$config.Host -or $config.Host -in @('localhost', '127.0.0.1', '::1')) {
        throw 'The client config must point to a server reachable from the Android device.'
    }
}

$buildArgs = @('build', $project, '-c', $Configuration, '-m:1', "-v:$Verbosity",
    "-p:RuntimeIdentifiers=$Runtime", '-p:CRAndroidBuild=true', '-p:RunAnalyzers=false', '-p:GeneratePackageOnBuild=false',
    "-p:ApplicationVersion=$VersionCode", "-p:LogiklikBuildNumber=$VersionCode")
$buildArgs += "-p:PackageVersion=$PackageVersion", "-p:Version=$Version", "-p:ApplicationDisplayVersion=$Version"
$commit = & git -c "safe.directory=$repo" -C $repo rev-parse HEAD 2>$null
if ($LASTEXITCODE -eq 0) { $buildArgs += "-p:CommitSha=$commit" }
if ($Resources) { $buildArgs += "-p:AndroidGameResources=$Resources" }
if ($NetworkPublicKey) {
    $NetworkPublicKey = (Get-Item -LiteralPath $NetworkPublicKey).FullName
    $buildArgs += "-p:NetworkHandshakePublicKey=$NetworkPublicKey"
}
if ($Distribution) {
    $buildArgs += '-p:AndroidKeyStore=true'
    $buildArgs += "-p:AndroidSigningKeyStore=$((Get-Item -LiteralPath $SigningKeyStore).FullName)"
    $buildArgs += "-p:AndroidSigningKeyAlias=$SigningKeyAlias"
    # apksigner consumes file: sources as streams; using one single-line file
    # for both store/key passwords reaches EOF on the second read. env: is safe
    # for both and keeps the value out of MSBuild's command-line properties.
    $passwordSource = "env:$SigningPasswordEnvironment"
    $buildArgs += "-p:AndroidSigningStorePass=$passwordSource", "-p:AndroidSigningKeyPass=$passwordSource", '-p:AndroidDebuggable=false'
}
if (!$CompileOnly) { $buildArgs += '-t:SignAndroidPackage' }
$oldSigningPassword = [Environment]::GetEnvironmentVariable($SigningPasswordEnvironment)
try {
    if ($Distribution -and $SigningPasswordFile) {
        [Environment]::SetEnvironmentVariable($SigningPasswordEnvironment,
            [IO.File]::ReadAllText((Get-Item -LiteralPath $SigningPasswordFile).FullName).Trim())
    }
    & dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) { throw "Android build failed ($LASTEXITCODE)." }
} finally {
    if ($Distribution -and $SigningPasswordFile) { [Environment]::SetEnvironmentVariable($SigningPasswordEnvironment, $oldSigningPassword) }
}
if (!$CompileOnly) {
    $package = Join-Path $repo "Intersect.Client.Android/bin/$Configuration/net10.0-android/com.logiklik.corpsroyaux-Signed.apk"
    if (!(Test-Path -LiteralPath $package)) { throw 'The Android build did not produce a signed APK.' }
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    Copy-Item -LiteralPath $package -Destination $output -Force
    Write-Host "Android APK: $output"
    if ($Distribution) {
        & (Join-Path $PSScriptRoot 'package-android-release.ps1') -Apk $package -VersionCode $VersionCode -NetworkPublicKey $NetworkPublicKey -Runtime $Runtime -Version $Version
    } else {
        Write-Host 'Development signature: use -Distribution for player packages.'
    }
}
