param(
    [string]$PublishDir = "",
    [string]$SteamworksNetVersion = "2024.8.0"
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $PublishDir = Join-Path $PSScriptRoot "..\Intersect.Client\bin\Release\net8.0\win-x64\publish"
}

$PublishDir = [System.IO.Path]::GetFullPath($PublishDir)
if (-not (Test-Path $PublishDir)) {
    throw "Client publish directory not found: $PublishDir"
}

$cacheRoot = Join-Path $PSScriptRoot "..\.steam-runtime"
$versionRoot = Join-Path $cacheRoot $SteamworksNetVersion
$archive = Join-Path $cacheRoot "Steamworks.NET-Standalone_$SteamworksNetVersion.zip"
$steamApiTarget = Join-Path $PublishDir "steam_api64.dll"
$appIdTarget = Join-Path $PublishDir "steam_appid.txt"

New-Item -ItemType Directory -Force -Path $cacheRoot | Out-Null

if (-not (Test-Path $versionRoot)) {
    $url = "https://github.com/rlabrecque/Steamworks.NET/releases/download/$SteamworksNetVersion/Steamworks.NET-Standalone_$SteamworksNetVersion.zip"
    Write-Host "Downloading matching Steamworks.NET native runtime $SteamworksNetVersion..."
    Invoke-WebRequest -Uri $url -OutFile $archive

    if (Test-Path $versionRoot) {
        Remove-Item -Recurse -Force $versionRoot
    }

    Expand-Archive -Path $archive -DestinationPath $versionRoot -Force
}

$steamApi = Get-ChildItem -Path $versionRoot -Recurse -File -Filter "steam_api64.dll" |
    Select-Object -First 1

if (-not $steamApi) {
    throw "steam_api64.dll was not found in Steamworks.NET Standalone $SteamworksNetVersion."
}

Copy-Item -Path $steamApi.FullName -Destination $steamApiTarget -Force
Copy-Item -Path (Join-Path $PSScriptRoot "..\Intersect.Client.Core\steam_appid.txt") -Destination $appIdTarget -Force

$appId = (Get-Content $appIdTarget -Raw).Trim()
if ($appId -ne "4317960") {
    throw "Unexpected Steam AppID: $appId"
}

$dll = Get-Item $steamApiTarget
$hash = Get-FileHash $steamApiTarget -Algorithm SHA256

Write-Host ""
Write-Host "Steam runtime ready."
Write-Host "  Publish: $PublishDir"
Write-Host "  steam_api64.dll: $($dll.Length) bytes"
Write-Host "  SHA256: $($hash.Hash)"
Write-Host "  AppID: $appId"
Write-Host ""
Write-Host "IMPORTANT: Do not overwrite this DLL with the copy from Steamworks SDK 1.65."
