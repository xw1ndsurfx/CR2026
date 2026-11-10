param(
    [string]$OceanDirectory = (Join-Path (Split-Path (Split-Path $PSScriptRoot)) 'resources/ocean'),
    [string[]]$Stems,
    [ValidateSet('Normal','Idle','Damaged')][string[]]$States = @('Normal','Idle','Damaged')
)
$ErrorActionPreference = 'Stop'
$calibration = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'fleet-deck-calibration.json') -Raw | ConvertFrom-Json -AsHashtable
if (!$Stems) {
    $Stems = @(Get-ChildItem -LiteralPath $OceanDirectory -Filter 'Ship_*_Sinking_v5_compat.png' |
        ForEach-Object { $_.Name -replace '_Sinking_v5_compat\.png$','' })
}
if (!$Stems) { throw 'No fleet models found' }
if ($States -contains 'Idle' -and -not ('ShipFamilyPixels' -as [type])) {
    Add-Type -AssemblyName System.Drawing
    $refs = @([Drawing.Bitmap].Assembly.Location,[Drawing.Color].Assembly.Location,
        (Join-Path $PSHOME 'System.Private.Windows.GdiPlus.dll'),(Join-Path $PSHOME 'System.Private.Windows.Core.dll'))
    Add-Type -ReferencedAssemblies $refs -Path @((Join-Path $PSScriptRoot 'CannonLayerPixels.cs'),
        (Join-Path $PSScriptRoot 'ShipFamilyPixels.cs'))
}

$bob = @(0,-1,0,1)
$encoding = [Text.UTF8Encoding]::new($false)
foreach ($stem in $Stems) {
    $hull = ($stem -split '_')[-1]
    $profile = $calibration[$hull]
    if (!$profile -or $profile.Helm.Count -ne 8) { throw "Missing deck calibration: $stem" }
    foreach ($state in $States) {
        $filename = if ($stem -eq 'Ship_CorpsRoyaux_ThreeMast' -and $state -eq 'Normal' -and
            (Test-Path -LiteralPath (Join-Path $OceanDirectory ($stem+'_Layered_v4_compat.png')))) {
            $stem+'_Layered_v4_compat.png'
        } else { $stem+'_'+$state+'_v5_compat.png' }
        $body = Join-Path $OceanDirectory $filename
        $path = $body+'.naval.json'
        $atlas = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        if ($atlas.Directions.Count -ne 8) { throw "Expected eight directions: $filename" }
        $changed = $false
        for ($d=0; $d -lt 8; $d++) {
            $row = $atlas.Directions[$d]
            if ($row.Count -ne 4) { throw "Expected four phases: $filename" }
            $x,$y = $profile.Helm[$d]
            for ($phase=0; $phase -lt 4; $phase++) {
                $frame = $row[$phase]
                if ($frame.Width -ne 384 -or $frame.Height -ne 256 -or
                    $frame.X -ne ($d%2)*1536+$phase*384 -or $frame.Y -ne [Math]::Floor($d/2)*256) {
                    throw "Unexpected fleet frame layout: $filename"
                }
                $helm = $frame.CrewAnchors | Where-Object { $_.Role -eq 0 -and $_.Index -eq 0 } | Select-Object -First 1
                if (!$helm) { throw "Missing helm: $filename direction $d" }
                if ($d -eq 4 -and $profile.SouthSternGuide) {
                    # The painted quarterdeck starts above the first mast in this view.
                    # Keep a centreline point between the new helm and the main deck.
                    $guideX,$guideY = $profile.SouthSternGuide
                    $guideY += $bob[$phase]
                    $guide = $frame.DeckMasts[0]
                    if ($guide.X -ne $guideX -or $guide.Y -ne $guideY) {
                        $guide.X = $guideX
                        $guide.Y = $guideY
                        $changed = $true
                    }
                }
                $dx = $x-$helm.X
                $dy = $y+$bob[$phase]-$helm.Y
                if ($dx -eq 0 -and $dy -eq 0) { continue }
                $helm.X = $x
                $helm.Y = $y+$bob[$phase]
                if ($profile.MoveSternGuides) {
                    # These hulls use virtual guides interpolated from the helm.
                    $frame.DeckMasts[0].X += $dx*(1-.9/3.4)
                    $frame.DeckMasts[0].Y += $dy*(1-.9/3.4)
                    foreach ($edge in @($frame.DeckPort[0],$frame.DeckStarboard[0])) {
                        $edge.X += $dx*(1-1.4/3.4)
                        $edge.Y += $dy*(1-1.4/3.4)
                    }
                }
                $changed = $true
            }
        }

        if ($state -eq 'Idle') {
            $regions = [Collections.Generic.List[int]]::new()
            $bands = $profile.IdleFarRails
            if ($profile.NarrowSternFarRails -and $stem -match '^Ship_(Ghost|Pirate|Skeleton)_') {
                # These hulls have a narrower stern and an offset mast in the south view.
                $bands = @($bands | Where-Object { $_[0] -ne 4 }) + $profile.NarrowSternFarRails
            }
            foreach ($band in $bands) {
                $regions.AddRange([int[]]$band)
            }
            $moved = [ShipFamilyPixels]::MoveFarRailsToHull($body,(Join-Path $OceanDirectory $atlas.ForegroundGraphics),
                (Join-Path $OceanDirectory $atlas.CannonOcclusionGraphics),$regions.ToArray())
            if ($moved -gt 0) {
                $hash = (Get-FileHash -LiteralPath $body -Algorithm SHA256).Hash.ToLowerInvariant()
                if ($atlas.SourceSha256 -ne $hash) {
                    $atlas.SourceSha256 = $hash
                    $changed = $true
                }
                Write-Output "$stem moved $moved far rail pixels below the crew"
            }
        }

        if ($changed) {
            $json = $atlas | ConvertTo-Json -Depth 30 -Compress
            if ($encoding.GetByteCount($json) -gt 131072) { throw "Deck metadata exceeds the loader limit: $filename" }
            [IO.File]::WriteAllText($path,$json,$encoding)
        }
    }
}
Write-Output "Calibrated $($Stems.Count) models across $($States.Count) playable states."
