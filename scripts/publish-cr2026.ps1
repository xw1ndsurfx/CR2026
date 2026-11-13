[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('android-arm64','android-x64','all-x64')][string]$Runtime,
    [ValidateSet('Release')][string]$Configuration = 'Release',
    [string]$PackageVersion = '0.8.0-beta',
    [string]$Version = '0.8.0',
    [switch]$PlanOnly
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$commit = & git -c "safe.directory=$repo" -C $repo rev-parse --verify HEAD 2>$null
if ($LASTEXITCODE -ne 0 -or $commit -notmatch '^[0-9a-fA-F]{40}$') { throw 'Cannot determine the repository revision.' }
$desktopRuntimes = @()
if ($Runtime -eq 'all-x64') { $desktopRuntimes = @('linux-x64','win-x64') }
$androidRuntime = if ($Runtime -eq 'all-x64') { 'android-arm64' } else { $Runtime }
Push-Location $repo
try {
    foreach ($rid in $desktopRuntimes) {
        $projects = @('Server','Client')
        if ($rid -eq 'win-x64') { $projects += 'Editor' }
        foreach ($project in $projects) {
            Write-Host "CR2026 publish: $project / $rid / $Configuration / $Version"
            if (!$PlanOnly) {
                & dotnet publish "Intersect.$project/Intersect.$project.csproj" -p:Configuration=$Configuration -p:PackageVersion=$PackageVersion -p:Version=$Version -p:CommitSha=$commit -r $rid -p:RunAnalyzers=false
                if ($LASTEXITCODE -ne 0) { throw "$project / $rid publication failed." }
            }
        }
    }
    Write-Host "CR2026 publish: Android player APK / $androidRuntime / $Configuration / $Version"
    if ($PlanOnly) { return }
    $androidArgs = @{ Distribution = $true; Runtime = $androidRuntime; Configuration = $Configuration; PackageVersion = $PackageVersion; Version = $Version }
    if ($Runtime -eq 'all-x64') {
        $androidArgs.NetworkPublicKey = Join-Path $repo 'Intersect.Network/bin/Release/keys/network.handshake.bkey.pub'
    }
    & ./scripts/build-android-client.ps1 @androidArgs
} finally { Pop-Location }
