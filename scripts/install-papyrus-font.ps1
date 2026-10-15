[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ZipPath,

    [string]$ResourcesDirectory = "",

    [int]$GameSize = 12,
    [int]$UiSize = 14,
    [int]$EntityNameSize = 12,
    [int]$ChatBubbleSize = 12,
    [int]$ActionMsgSize = 12,

    [switch]$SkipConfigUpdate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-FullPath {
    param([Parameter(Mandatory = $true)][string]$Path)
    return [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $Path).Path)
}

function Get-NearestSize {
    param(
        [Parameter(Mandatory = $true)][int]$RequestedSize,
        [Parameter(Mandatory = $true)][int[]]$AvailableSizes
    )

    return $AvailableSizes |
        Sort-Object { [Math]::Abs($_ - $RequestedSize) }, { $_ } |
        Select-Object -First 1
}

if (-not (Test-Path -LiteralPath $ZipPath -PathType Leaf)) {
    throw "Papyrus ZIP not found: $ZipPath"
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

$tempDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("cr-papyrus-xnb-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempDirectory -Force | Out-Null

try {
    Expand-Archive -LiteralPath $zipFullPath -DestinationPath $tempDirectory -Force

    $convertedFonts = Get-ChildItem -LiteralPath $tempDirectory -Recurse -File -Filter "*.xnb" |
        ForEach-Object {
            $match = [regex]::Match(
                $_.Name,
                '^Papyrus_(\d+)(?:_Regular)?\.xnb$',
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
        throw "No converted Papyrus XNB files were found. Expected names such as 'Papyrus_14_Regular.xnb'."
    }

    $sizes = @($convertedFonts | Select-Object -ExpandProperty Size -Unique)

    foreach ($font in $convertedFonts) {
        $destination = Join-Path $fontsOutputDirectory ("papyrus_{0}.xnb" -f $font.Size)
        Copy-Item -LiteralPath $font.File.FullName -Destination $destination -Force
        Write-Host ("Installed Papyrus size {0}: {1}" -f $font.Size, $destination)
    }

    Write-Host ""
    Write-Host ("Papyrus converted XNB files installed: {0}" -f $convertedFonts.Count)
    Write-Host ("Available sizes: {0}" -f ($sizes -join ", "))
    Write-Host ("Font directory: {0}" -f $fontsOutputDirectory)

    if (-not $SkipConfigUpdate) {
        $configPath = Join-Path $ResourcesDirectory "config.json"

        if (Test-Path -LiteralPath $configPath -PathType Leaf) {
            $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
            $backupPath = "$configPath.papyrus-backup-$timestamp"
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

                $nearestSize = Get-NearestSize -RequestedSize ([int]$requestedSizes[$property]) -AvailableSizes $sizes
                $jsonProperty.Value = "papyrus,$nearestSize"
            }

            $config | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $configPath -Encoding UTF8

            Write-Host ("Updated font settings in: {0}" -f $configPath)
            Write-Host ("Config backup: {0}" -f $backupPath)
            Write-Host ("GameFont: papyrus,{0}" -f (Get-NearestSize -RequestedSize $GameSize -AvailableSizes $sizes))
            Write-Host ("UIFont: papyrus,{0}" -f (Get-NearestSize -RequestedSize $UiSize -AvailableSizes $sizes))
            Write-Host ("EntityNameFont: papyrus,{0}" -f (Get-NearestSize -RequestedSize $EntityNameSize -AvailableSizes $sizes))
            Write-Host ("ChatBubbleFont: papyrus,{0}" -f (Get-NearestSize -RequestedSize $ChatBubbleSize -AvailableSizes $sizes))
            Write-Host ("ActionMsgFont: papyrus,{0}" -f (Get-NearestSize -RequestedSize $ActionMsgSize -AvailableSizes $sizes))
        }
        else {
            Write-Warning "No config.json was found in $ResourcesDirectory."
            Write-Warning "The Papyrus XNB files were installed, but font settings were not changed."
        }
    }

    Write-Host ""
    Write-Host "Papyrus installation complete. Restart the Corps Royaux client to test the new font."
}
finally {
    if (Test-Path -LiteralPath $tempDirectory) {
        Remove-Item -LiteralPath $tempDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }
}
