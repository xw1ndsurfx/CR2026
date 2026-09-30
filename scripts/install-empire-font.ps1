[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ZipPath,

    [string]$ResourcesDirectory = "",

    [int]$GameSize = 16,
    [int]$UiSize = 16,
    [int]$EntityNameSize = 16,
    [int]$ChatBubbleSize = 16,
    [int]$ActionMsgSize = 16,

    [switch]$SkipConfigUpdate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-FullPath {
    param([Parameter(Mandatory = $true)][string]$Path)
    return [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $Path).Path)
}

if (-not (Test-Path -LiteralPath $ZipPath -PathType Leaf)) {
    throw "Empire 7 source ZIP not found: $ZipPath"
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$zipFullPath = Resolve-FullPath -Path $ZipPath

if ([string]::IsNullOrWhiteSpace($ResourcesDirectory)) {
    $rootResources = Join-Path $repoRoot "resources"
    $assetResources = Join-Path $repoRoot "assets/development/client/resources"

    if (Test-Path -LiteralPath (Join-Path $rootResources "config.json") -PathType Leaf) {
        $ResourcesDirectory = $rootResources
    }
    elseif (Test-Path -LiteralPath (Join-Path $assetResources "config.json") -PathType Leaf) {
        $ResourcesDirectory = $assetResources
    }
    else {
        $ResourcesDirectory = $rootResources
    }
}

$ResourcesDirectory = [System.IO.Path]::GetFullPath($ResourcesDirectory)
$fontsDirectory = Join-Path $ResourcesDirectory "fonts"
New-Item -ItemType Directory -Path $fontsDirectory -Force | Out-Null

$tempDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("cr-empire7-bitmap-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempDirectory -Force | Out-Null

try {
    Expand-Archive -LiteralPath $zipFullPath -DestinationPath $tempDirectory -Force

    $spriteSheet = Get-ChildItem -LiteralPath $tempDirectory -Recurse -File -Filter "empire_7.png" |
        Select-Object -First 1
    $metricsFile = Get-ChildItem -LiteralPath $tempDirectory -Recurse -File -Filter "empire_7.txt" |
        Select-Object -First 1

    if ($null -eq $spriteSheet) {
        throw "empire_7.png was not found in the supplied ZIP."
    }

    if ($null -eq $metricsFile) {
        throw "empire_7.txt was not found in the supplied ZIP."
    }

    $bitmapDestination = Join-Path $fontsDirectory "empire7.bitmapfont.png"
    $metricsDestination = Join-Path $fontsDirectory "empire7.bitmapfont.txt"

    Copy-Item -LiteralPath $spriteSheet.FullName -Destination $bitmapDestination -Force
    Copy-Item -LiteralPath $metricsFile.FullName -Destination $metricsDestination -Force

    # Remove stale TTF-generated/converter-generated Empire XNBs. The client now
    # loads the original bitmap sheet directly and gives it priority over XNB fonts.
    Get-ChildItem -LiteralPath $fontsDirectory -File -Filter "empire7_*.xnb" -ErrorAction SilentlyContinue |
        Remove-Item -Force

    Write-Host "Installed Empire 7 original bitmap sheet:"
    Write-Host "  $bitmapDestination"
    Write-Host "Installed Empire 7 Construct metrics:"
    Write-Host "  $metricsDestination"

    if (-not $SkipConfigUpdate) {
        $configPath = Join-Path $ResourcesDirectory "config.json"

        if (Test-Path -LiteralPath $configPath -PathType Leaf) {
            $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
            $backupPath = "$configPath.empire7-bitmap-backup-$timestamp"
            Copy-Item -LiteralPath $configPath -Destination $backupPath -Force

            $config = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
            $requestedSizes = [ordered]@{
                GameFont       = $GameSize
                UIFont         = $UiSize
                EntityNameFont = $EntityNameSize
                ChatBubbleFont = $ChatBubbleSize
                ActionMsgFont  = $ActionMsgSize
            }

            foreach ($property in $requestedSizes.Keys) {
                $jsonProperty = $config.PSObject.Properties[$property]
                if ($null -eq $jsonProperty) {
                    continue
                }

                $size = [Math]::Min(26, [Math]::Max(8, [int]$requestedSizes[$property]))
                $jsonProperty.Value = "empire7,$size"
            }

            $config | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $configPath -Encoding UTF8

            Write-Host "Updated font settings in: $configPath"
            Write-Host "Config backup: $backupPath"
        }
        else {
            Write-Warning "No config.json was found in $ResourcesDirectory."
            Write-Warning "The bitmap font files were installed, but client font settings were not changed."
        }
    }

    Write-Host ""
    Write-Host "Empire 7 bitmap installation complete."
    Write-Host "Restart the Corps Royaux client. No font conversion/XNB generation is required."
}
finally {
    if (Test-Path -LiteralPath $tempDirectory) {
        Remove-Item -LiteralPath $tempDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }
}
