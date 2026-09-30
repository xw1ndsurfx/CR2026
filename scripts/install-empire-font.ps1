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

function Get-EmpireMetrics {
    param(
        [Parameter(Mandatory = $true)][string]$ParametersFile,
        [Parameter(Mandatory = $true)][int]$Columns
    )

    $raw = Get-Content -LiteralPath $ParametersFile -Raw -Encoding UTF8

    $characterWidthMatch = [regex]::Match($raw, 'Character width:\s*(\d+)')
    $characterHeightMatch = [regex]::Match($raw, 'Character height:\s*(\d+)')
    $characterSetMatch = [regex]::Match($raw, 'Character set:\s*\r?\n([^\r\n]+)')

    if (-not $characterWidthMatch.Success -or -not $characterHeightMatch.Success -or -not $characterSetMatch.Success) {
        throw "Could not read the Empire 7 grid or character set from $ParametersFile."
    }

    $cellWidth = [int]$characterWidthMatch.Groups[1].Value
    $cellHeight = [int]$characterHeightMatch.Groups[1].Value
    $characterLine = $characterSetMatch.Groups[1].Value

    $widths = [ordered]@{}
    $spacingMatches = [regex]::Matches($raw, '\[(\d+),"((?:\\.|[^"])*)"\]')
    foreach ($spacingMatch in $spacingMatches) {
        $width = [int]$spacingMatch.Groups[1].Value
        $encodedCharacters = $spacingMatch.Groups[2].Value
        $decodedCharacters = [regex]::Unescape($encodedCharacters)
        foreach ($character in $decodedCharacters.ToCharArray()) {
            $widths[[string][int]$character] = $width
        }
    }

    if (-not $widths.Contains("32")) {
        $widths["32"] = 4
    }

    $characterCodes = @($characterLine.ToCharArray() | ForEach-Object { [int]$_ })
    foreach ($codePoint in $characterCodes) {
        $key = [string]$codePoint
        if (-not $widths.Contains($key)) {
            throw "No spacing value was found for Empire 7 character U+$($codePoint.ToString('X4'))."
        }
    }

    return [pscustomobject]@{
        CellWidth = $cellWidth
        CellHeight = $cellHeight
        Columns = $Columns
        Characters = $characterCodes
        Widths = $widths
    }
}

function Get-EmpirePixelScale {
    param([Parameter(Mandatory = $true)][int]$RequestedSize)

    # Whole-number nearest-neighbour scaling keeps the original bitmap pixel grid.
    # 16-22 maps to 2x, which matches the readable Corps Royaux HUD scale well.
    return [Math]::Max(1, [Math]::Floor($RequestedSize / 8))
}

function Resize-PngNearest {
    param(
        [Parameter(Mandatory = $true)][string]$SourcePath,
        [Parameter(Mandatory = $true)][string]$DestinationPath,
        [Parameter(Mandatory = $true)][int]$Scale
    )

    if ($Scale -lt 1) {
        throw "PNG scale must be at least 1."
    }

    if ($Scale -eq 1) {
        Copy-Item -LiteralPath $SourcePath -Destination $DestinationPath -Force
        return
    }

    Add-Type -AssemblyName System.Drawing

    $source = [System.Drawing.Bitmap]::FromFile($SourcePath)
    try {
        $destination = New-Object System.Drawing.Bitmap(
            $source.Width * $Scale,
            $source.Height * $Scale,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb
        )

        try {
            $graphics = [System.Drawing.Graphics]::FromImage($destination)
            try {
                $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
                $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighSpeed
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
                $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half

                $destinationRect = New-Object System.Drawing.Rectangle(0, 0, $destination.Width, $destination.Height)
                $graphics.DrawImage(
                    $source,
                    $destinationRect,
                    0,
                    0,
                    $source.Width,
                    $source.Height,
                    [System.Drawing.GraphicsUnit]::Pixel
                )
            }
            finally {
                $graphics.Dispose()
            }

            $destination.Save($DestinationPath, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $destination.Dispose()
        }
    }
    finally {
        $source.Dispose()
    }
}

function Get-DotNetCommand {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $dotnet) {
        throw "The .NET SDK is required to build the Empire 7 bitmap-font pipeline. Install the .NET 8 SDK, then run the script again."
    }

    return $dotnet.Source
}

function Build-EmpireProcessor {
    param(
        [Parameter(Mandatory = $true)][string]$TempDirectory,
        [Parameter(Mandatory = $true)][string]$Version
    )

    $dotnet = Get-DotNetCommand
    $processorSourceDirectory = Join-Path $PSScriptRoot "EmpireBitmapFontProcessor"
    if (-not (Test-Path -LiteralPath $processorSourceDirectory -PathType Container)) {
        throw "Empire bitmap-font processor sources were not found: $processorSourceDirectory"
    }

    $processorBuildDirectory = Join-Path $TempDirectory "processor-source"
    $processorOutputDirectory = Join-Path $TempDirectory "processor-bin"
    Copy-Item -LiteralPath $processorSourceDirectory -Destination $processorBuildDirectory -Recurse -Force
    New-Item -ItemType Directory -Path $processorOutputDirectory -Force | Out-Null

    $projectPath = Join-Path $processorBuildDirectory "EmpireBitmapFontProcessor.csproj"
    & $dotnet build $projectPath -c Release -o $processorOutputDirectory "-p:MonoGameVersion=$Version"
    if ($LASTEXITCODE -ne 0) {
        throw "Could not build the Empire bitmap-font processor."
    }

    $processorDll = Join-Path $processorOutputDirectory "EmpireBitmapFontProcessor.dll"
    if (-not (Test-Path -LiteralPath $processorDll -PathType Leaf)) {
        throw "Empire bitmap-font processor DLL was not generated: $processorDll"
    }

    return $processorDll
}

function Get-MgcbExecutable {
    param(
        [Parameter(Mandatory = $true)][string]$TempDirectory,
        [Parameter(Mandatory = $true)][string]$Version
    )

    $dotnet = Get-DotNetCommand
    $toolDirectory = Join-Path $TempDirectory "mgcb-tool"
    New-Item -ItemType Directory -Path $toolDirectory -Force | Out-Null

    & $dotnet tool install dotnet-mgcb --tool-path $toolDirectory --version $Version
    if ($LASTEXITCODE -ne 0) {
        throw "Could not install dotnet-mgcb $Version."
    }

    $mgcbExecutable = Get-ChildItem -LiteralPath $toolDirectory -File |
        Where-Object { $_.BaseName -eq "mgcb" -or $_.Name -eq "mgcb.exe" } |
        Select-Object -First 1

    if ($null -eq $mgcbExecutable) {
        throw "dotnet-mgcb was installed but the mgcb executable could not be located."
    }

    return $mgcbExecutable.FullName
}

function Invoke-Mgcb {
    param(
        [Parameter(Mandatory = $true)][string]$ResponseFile,
        [Parameter(Mandatory = $true)][string]$TempDirectory,
        [Parameter(Mandatory = $true)][string]$Version
    )

    $mgcbExecutable = Get-MgcbExecutable -TempDirectory $TempDirectory -Version $Version
    & $mgcbExecutable "/@:$ResponseFile" /rebuild
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

    $fontImage = Get-ChildItem -LiteralPath $tempDirectory -Recurse -File -Filter "empire_7.png" | Select-Object -First 1
    $parametersFile = Get-ChildItem -LiteralPath $tempDirectory -Recurse -File -Filter "empire_7.txt" | Select-Object -First 1

    if ($null -eq $fontImage) {
        throw "empire_7.png was not found in the supplied ZIP."
    }
    if ($null -eq $parametersFile) {
        throw "empire_7.txt was not found in the supplied ZIP."
    }

    Add-Type -AssemblyName System.Drawing
    $sourceImage = [System.Drawing.Image]::FromFile($fontImage.FullName)
    try {
        $rawParameters = Get-Content -LiteralPath $parametersFile.FullName -Raw -Encoding UTF8
        $widthMatch = [regex]::Match($rawParameters, 'Character width:\s*(\d+)')
        if (-not $widthMatch.Success) {
            throw "Could not read the Empire 7 character width."
        }

        $cellWidth = [int]$widthMatch.Groups[1].Value
        if ($cellWidth -lt 1 -or ($sourceImage.Width % $cellWidth) -ne 0) {
            throw "Empire 7 sprite sheet width does not match its character grid."
        }

        $columns = [int]($sourceImage.Width / $cellWidth)
    }
    finally {
        $sourceImage.Dispose()
    }

    $buildRoot = Join-Path $tempDirectory "build"
    $fontSourceDirectory = Join-Path $buildRoot "Fonts"
    New-Item -ItemType Directory -Path $fontSourceDirectory -Force | Out-Null

    $metrics = Get-EmpireMetrics -ParametersFile $parametersFile.FullName -Columns $columns
    $metricsPath = Join-Path $fontSourceDirectory "empire7.metrics.json"
    $metrics | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $metricsPath -Encoding UTF8

    $processorDll = Build-EmpireProcessor -TempDirectory $tempDirectory -Version $MonoGameVersion

    $mgcbLines = New-Object System.Collections.Generic.List[string]
    $mgcbLines.Add("/outputDir:bin/DesktopGL")
    $mgcbLines.Add("/intermediateDir:obj/DesktopGL")
    $mgcbLines.Add("/platform:DesktopGL")
    $mgcbLines.Add("/config:")
    $mgcbLines.Add("/profile:Reach")
    $mgcbLines.Add("/compress:False")
    $mgcbLines.Add("/reference:$processorDll")
    $mgcbLines.Add("")

    foreach ($size in ($Sizes | Sort-Object -Unique)) {
        if ($size -lt 1) {
            throw "Font sizes must be positive integers. Invalid size: $size"
        }

        $scale = Get-EmpirePixelScale -RequestedSize $size
        $bitmapName = "empire7_$size.png"
        $bitmapPath = Join-Path $fontSourceDirectory $bitmapName
        Resize-PngNearest -SourcePath $fontImage.FullName -DestinationPath $bitmapPath -Scale $scale

        $mgcbLines.Add("#begin Fonts/$bitmapName")
        $mgcbLines.Add("/importer:TextureImporter")
        $mgcbLines.Add("/processor:EmpireBitmapFontProcessor")
        $mgcbLines.Add("/processorParam:MetricsFile=empire7.metrics.json")
        $mgcbLines.Add("/processorParam:Scale=$scale")
        $mgcbLines.Add("/build:Fonts/$bitmapName;Fonts/empire7_$size")
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
            throw "Expected bitmap-font XNB was not generated: $builtFont"
        }

        Copy-Item -LiteralPath $builtFont -Destination (Join-Path $fontsOutputDirectory "empire7_$size.xnb") -Force
    }

    Write-Host "Empire 7 pixel-perfect XNB files installed in: $fontsOutputDirectory"

    if (-not $SkipConfigUpdate) {
        $configPath = Join-Path $ResourcesDirectory "config.json"
        if (Test-Path -LiteralPath $configPath -PathType Leaf) {
            $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
            $backupPath = "$configPath.empire7-backup-$timestamp"
            Copy-Item -LiteralPath $configPath -Destination $backupPath -Force

            $config = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
            $minimumSizes = @{
                GameFont = 16
                UIFont = 20
                EntityNameFont = 16
                ChatBubbleFont = 16
                ActionMsgFont = 16
            }

            foreach ($property in $minimumSizes.Keys) {
                $jsonProperty = $config.PSObject.Properties[$property]
                if ($null -eq $jsonProperty) {
                    continue
                }

                $fontSize = [int]$minimumSizes[$property]
                $current = [string]$jsonProperty.Value
                if ($current -match ',\s*(\d+)\s*$') {
                    $fontSize = [Math]::Max($fontSize, [int]$Matches[1])
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
            Write-Warning "Recommended CR settings: GameFont 16, UIFont 20, EntityNameFont 16, ChatBubbleFont 16, ActionMsgFont 16."
        }
    }

    Write-Host "Empire 7 bitmap migration complete. The supplied PNG/TTF remain outside the public repository."
}
finally {
    if (Test-Path -LiteralPath $tempDirectory) {
        Remove-Item -LiteralPath $tempDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }
}
