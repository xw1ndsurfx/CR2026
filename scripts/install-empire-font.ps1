[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ZipPath,

    [string]$ResourcesDirectory = "",

    [string]$MonoGameVersion = "3.8.2.1105",

    [int[]]$Sizes = @(8, 10, 12, 14, 16, 18, 20, 22, 24, 26),

    [switch]$SkipConfigUpdate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-FullPath {
    param([Parameter(Mandatory = $true)][string]$Path)
    return [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $Path).Path)
}

function Get-EmpireCharacterRegions {
    param([Parameter(Mandatory = $true)][string]$ParametersFile)

    $lines = Get-Content -LiteralPath $ParametersFile -Encoding UTF8
    $markerIndex = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i].Trim() -eq "Character set:") {
            $markerIndex = $i
            break
        }
    }

    if ($markerIndex -lt 0) {
        throw "Could not find 'Character set:' in $ParametersFile."
    }

    $characterLine = $null
    for ($i = $markerIndex + 1; $i -lt $lines.Count; $i++) {
        if (-not [string]::IsNullOrWhiteSpace($lines[$i])) {
            $characterLine = $lines[$i]
            break
        }
    }

    if ([string]::IsNullOrWhiteSpace($characterLine)) {
        throw "The Empire 7 character set is empty in $ParametersFile."
    }

    # Space is defined in the supplied spacing table but is not repeated on the Character set line.
    $codePoints = (" " + $characterLine).ToCharArray() |
        ForEach-Object { [int]$_ } |
        Sort-Object -Unique

    $regions = New-Object System.Collections.Generic.List[object]
    $start = $null
    $previous = $null

    foreach ($codePoint in $codePoints) {
        if ($null -eq $start) {
            $start = $codePoint
            $previous = $codePoint
            continue
        }

        if ($codePoint -eq ($previous + 1)) {
            $previous = $codePoint
            continue
        }

        $regions.Add([pscustomobject]@{ Start = $start; End = $previous })
        $start = $codePoint
        $previous = $codePoint
    }

    if ($null -ne $start) {
        $regions.Add([pscustomobject]@{ Start = $start; End = $previous })
    }

    return $regions
}

function New-SpriteFontXml {
    param(
        [Parameter(Mandatory = $true)][int]$Size,
        [Parameter(Mandatory = $true)]$Regions
    )

    $regionXml = ($Regions | ForEach-Object {
        $startHex = $_.Start.ToString("X4")
        $endHex = $_.End.ToString("X4")
        "      <CharacterRegion><Start>&#x$startHex;</Start><End>&#x$endHex;</End></CharacterRegion>"
    }) -join [Environment]::NewLine

    return @"
<?xml version="1.0" encoding="utf-8"?>
<XnaContent xmlns:Graphics="Microsoft.Xna.Framework.Content.Pipeline.Graphics">
  <Asset Type="Graphics:FontDescription">
    <FontName>Empire 7.ttf</FontName>
    <Size>$Size</Size>
    <Spacing>0</Spacing>
    <UseKerning>false</UseKerning>
    <Style>Regular</Style>
    <DefaultCharacter>?</DefaultCharacter>
    <CharacterRegions>
$regionXml
    </CharacterRegions>
  </Asset>
</XnaContent>
"@
}

function Invoke-Mgcb {
    param(
        [Parameter(Mandatory = $true)][string]$ResponseFile,
        [Parameter(Mandatory = $true)][string]$TempDirectory,
        [Parameter(Mandatory = $true)][string]$Version
    )

    $mgcb = Get-Command mgcb -ErrorAction SilentlyContinue
    if ($null -ne $mgcb) {
        & $mgcb.Source "/@:$ResponseFile" /rebuild
        if ($LASTEXITCODE -ne 0) {
            throw "MGCB failed with exit code $LASTEXITCODE."
        }
        return
    }

    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $dotnet) {
        throw "MGCB was not found and the .NET SDK is unavailable. Install the .NET 8 SDK, then run the script again."
    }

    $toolDirectory = Join-Path $TempDirectory "mgcb-tool"
    New-Item -ItemType Directory -Path $toolDirectory -Force | Out-Null

    & $dotnet.Source tool install dotnet-mgcb --tool-path $toolDirectory --version $Version
    if ($LASTEXITCODE -ne 0) {
        throw "Could not install dotnet-mgcb $Version."
    }

    $mgcbExecutable = Get-ChildItem -LiteralPath $toolDirectory -File |
        Where-Object { $_.BaseName -eq "mgcb" -or $_.Name -eq "mgcb.exe" } |
        Select-Object -First 1

    if ($null -eq $mgcbExecutable) {
        throw "dotnet-mgcb was installed but the mgcb executable could not be located."
    }

    & $mgcbExecutable.FullName "/@:$ResponseFile" /rebuild
    if ($LASTEXITCODE -ne 0) {
        throw "MGCB failed with exit code $LASTEXITCODE."
    }
}

if (-not (Test-Path -LiteralPath $ZipPath -PathType Leaf)) {
    throw "ZIP not found: $ZipPath"
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$zipFullPath = Resolve-FullPath -Path $ZipPath

if ([string]::IsNullOrWhiteSpace($ResourcesDirectory)) {
    $rootResources = Join-Path $repoRoot "resources"
    $assetResources = Join-Path $repoRoot "assets/development/client/resources"

    if (Test-Path -LiteralPath $rootResources -PathType Container) {
        $ResourcesDirectory = $rootResources
    }
    elseif (Test-Path -LiteralPath $assetResources -PathType Container) {
        $ResourcesDirectory = $assetResources
    }
    else {
        $ResourcesDirectory = $rootResources
    }
}

$ResourcesDirectory = [System.IO.Path]::GetFullPath($ResourcesDirectory)
$fontsOutputDirectory = Join-Path $ResourcesDirectory "fonts"
New-Item -ItemType Directory -Path $fontsOutputDirectory -Force | Out-Null

$tempDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("cr-empire7-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempDirectory -Force | Out-Null

try {
    Expand-Archive -LiteralPath $zipFullPath -DestinationPath $tempDirectory -Force

    $fontFile = Get-ChildItem -LiteralPath $tempDirectory -Recurse -File -Filter "Empire 7.ttf" | Select-Object -First 1
    $parametersFile = Get-ChildItem -LiteralPath $tempDirectory -Recurse -File -Filter "empire_7.txt" | Select-Object -First 1

    if ($null -eq $fontFile) {
        throw "Empire 7.ttf was not found in the supplied ZIP."
    }
    if ($null -eq $parametersFile) {
        throw "empire_7.txt was not found in the supplied ZIP."
    }

    $buildRoot = Join-Path $tempDirectory "build"
    $fontSourceDirectory = Join-Path $buildRoot "Fonts"
    New-Item -ItemType Directory -Path $fontSourceDirectory -Force | Out-Null
    Copy-Item -LiteralPath $fontFile.FullName -Destination (Join-Path $fontSourceDirectory "Empire 7.ttf") -Force

    $regions = Get-EmpireCharacterRegions -ParametersFile $parametersFile.FullName

    $mgcbLines = New-Object System.Collections.Generic.List[string]
    $mgcbLines.Add("/outputDir:bin/DesktopGL")
    $mgcbLines.Add("/intermediateDir:obj/DesktopGL")
    $mgcbLines.Add("/platform:DesktopGL")
    $mgcbLines.Add("/config:")
    $mgcbLines.Add("/profile:Reach")
    $mgcbLines.Add("/compress:False")
    $mgcbLines.Add("")

    foreach ($size in ($Sizes | Sort-Object -Unique)) {
        if ($size -lt 1) {
            throw "Font sizes must be positive integers. Invalid size: $size"
        }

        $spriteFontName = "empire7_$size.spritefont"
        $spriteFontPath = Join-Path $fontSourceDirectory $spriteFontName
        New-SpriteFontXml -Size $size -Regions $regions | Set-Content -LiteralPath $spriteFontPath -Encoding UTF8

        $mgcbLines.Add("#begin Fonts/$spriteFontName")
        $mgcbLines.Add("/importer:FontDescriptionImporter")
        $mgcbLines.Add("/processor:FontDescriptionProcessor")
        $mgcbLines.Add("/processorParam:PremultiplyAlpha=True")
        $mgcbLines.Add("/processorParam:TextureFormat=NoChange")
        $mgcbLines.Add("/build:Fonts/$spriteFontName")
        $mgcbLines.Add("")
    }

    $mgcbPath = Join-Path $buildRoot "Empire7.mgcb"
    $mgcbLines | Set-Content -LiteralPath $mgcbPath -Encoding UTF8

    Push-Location $buildRoot
    try {
        Invoke-Mgcb -ResponseFile $mgcbPath -TempDirectory $tempDirectory -Version $MonoGameVersion
    }
    finally {
        Pop-Location
    }

    foreach ($size in ($Sizes | Sort-Object -Unique)) {
        $builtFont = Join-Path $buildRoot "bin/DesktopGL/Fonts/empire7_$size.xnb"
        if (-not (Test-Path -LiteralPath $builtFont -PathType Leaf)) {
            throw "Expected XNB was not generated: $builtFont"
        }
        Copy-Item -LiteralPath $builtFont -Destination (Join-Path $fontsOutputDirectory "empire7_$size.xnb") -Force
    }

    Write-Host "Empire 7 XNB files installed in: $fontsOutputDirectory"

    if (-not $SkipConfigUpdate) {
        $configPath = Join-Path $ResourcesDirectory "config.json"
        if (Test-Path -LiteralPath $configPath -PathType Leaf) {
            $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
            $backupPath = "$configPath.empire7-backup-$timestamp"
            Copy-Item -LiteralPath $configPath -Destination $backupPath -Force

            $config = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
            $properties = @("GameFont", "UIFont", "EntityNameFont", "ChatBubbleFont", "ActionMsgFont")

            foreach ($property in $properties) {
                $jsonProperty = $config.PSObject.Properties[$property]
                if ($null -eq $jsonProperty) {
                    continue
                }

                $current = [string]$jsonProperty.Value
                $fontSize = if ($property -eq "UIFont") { 10 } else { 8 }
                if ($current -match ',\s*(\d+)\s*$') {
                    $fontSize = [int]$Matches[1]
                }

                $nearestSize = $Sizes |
                    Sort-Object { [Math]::Abs($_ - $fontSize) }, { $_ } |
                    Select-Object -First 1

                $jsonProperty.Value = "empire7,$nearestSize"
            }

            $config | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $configPath -Encoding UTF8
            Write-Host "Updated font settings in: $configPath"
            Write-Host "Config backup: $backupPath"
        }
        else {
            Write-Warning "No config.json was found in $ResourcesDirectory. XNB files were installed, but font settings were not changed."
            Write-Warning "Set GameFont, UIFont, EntityNameFont, ChatBubbleFont and ActionMsgFont to empire7,<size> in the client config."
        }
    }

    Write-Host "Empire 7 migration complete. The original TTF was only used from the ZIP and was not copied into the repository."
}
finally {
    if (Test-Path -LiteralPath $tempDirectory) {
        Remove-Item -LiteralPath $tempDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }
}
