[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [Alias("ConvertedZipPath")]
    [string]$ZipPath,

    [string]$ResourcesDirectory = "",

    [switch]$SkipConfigUpdate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-FullPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    return [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $Path).Path)
}

if (-not (Test-Path -LiteralPath $ZipPath -PathType Leaf)) {
    throw "Converted Empire 7 ZIP not found: $ZipPath"
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
$fontsOutputDirectory = Join-Path $ResourcesDirectory "fonts"
New-Item -ItemType Directory -Path $fontsOutputDirectory -Force | Out-Null

$tempDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("cr-empire7-xnb-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempDirectory -Force | Out-Null

try {
    Expand-Archive -LiteralPath $zipFullPath -DestinationPath $tempDirectory -Force

    $convertedFonts = Get-ChildItem -LiteralPath $tempDirectory -Recurse -File -Filter "*.xnb" |
        ForEach-Object {
            $match = [regex]::Match(
                $_.Name,
                '^Empire\s*7_(\d+)(?:_Regular)?\.xnb$',
                [System.Text.RegularExpressions.RegexOptions]::IgnoreCase
            )

            if (-not $match.Success) {
                return
            }

            [pscustomobject]@{
                File = $_
                Size = [int]$match.Groups[1].Value
            }
        } |
        Where-Object { $null -ne $_ } |
        Sort-Object Size

    if ($convertedFonts.Count -eq 0) {
        throw "No converted Empire 7 XNB files were found. Expected names such as 'Empire 7_16_Regular.xnb'."
    }

    $sizes = @($convertedFonts | Select-Object -ExpandProperty Size -Unique)
    $requiredSizes = @(8, 10, 12, 14, 16, 18, 20, 22, 24, 26)
    $missingRecommended = @($requiredSizes | Where-Object { $_ -notin $sizes })

    if ($missingRecommended.Count -gt 0) {
        Write-Warning ("The converted ZIP is missing recommended sizes: " + ($missingRecommended -join ", "))
    }

    foreach ($font in $convertedFonts) {
        $destination = Join-Path $fontsOutputDirectory ("empire7_{0}.xnb" -f $font.Size)
        Copy-Item -LiteralPath $font.File.FullName -Destination $destination -Force
        Write-Host ("Installed Empire 7 size {0}: {1}" -f $font.Size, $destination)
    }

    Write-Host ""
    Write-Host ("Empire 7 converted XNB files installed: {0}" -f $convertedFonts.Count)
    Write-Host ("Font directory: {0}" -f $fontsOutputDirectory)

    if (-not $SkipConfigUpdate) {
        $configPath = Join-Path $ResourcesDirectory "config.json"

        if (Test-Path -LiteralPath $configPath -PathType Leaf) {
            $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
            $backupPath = "$configPath.empire7-backup-$timestamp"
            Copy-Item -LiteralPath $configPath -Destination $backupPath -Force

            $config = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json

            $recommendedSizes = [ordered]@{
                GameFont       = 16
                UIFont         = 20
                EntityNameFont = 16
                ChatBubbleFont = 16
                ActionMsgFont  = 16
            }

            foreach ($property in $recommendedSizes.Keys) {
                $jsonProperty = $config.PSObject.Properties[$property]
                if ($null -eq $jsonProperty) {
                    continue
                }

                $targetSize = [int]$recommendedSizes[$property]
                $currentValue = [string]$jsonProperty.Value

                # Preserve an explicitly selected Empire 7 size when it exists in the converted pack.
                if ($currentValue -match '^empire7,\s*(\d+)\s*$') {
                    $currentSize = [int]$Matches[1]
                    if ($currentSize -in $sizes) {
                        $targetSize = $currentSize
                    }
                }

                if ($targetSize -notin $sizes) {
                    $targetSize = $sizes |
                        Sort-Object { [Math]::Abs($_ - $targetSize) }, { $_ } |
                        Select-Object -First 1
                }

                $jsonProperty.Value = "empire7,$targetSize"
            }

            $config | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $configPath -Encoding UTF8

            Write-Host ("Updated font settings in: {0}" -f $configPath)
            Write-Host ("Config backup: {0}" -f $backupPath)
        }
        else {
            Write-Warning "No config.json was found in $ResourcesDirectory."
            Write-Warning "The XNB files were installed, but the font settings were not changed."
            Write-Warning "Recommended values: GameFont=empire7,16; UIFont=empire7,20; EntityNameFont=empire7,16; ChatBubbleFont=empire7,16; ActionMsgFont=empire7,16."
        }
    }

    Write-Host ""
    Write-Host "Empire 7 installation complete. No licensed font binaries were added to the CR2026 repository."
}
finally {
    if (Test-Path -LiteralPath $tempDirectory) {
        Remove-Item -LiteralPath $tempDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }
}
