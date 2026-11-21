param([Parameter(Mandatory)][string]$Stem,[Parameter(Mandatory)][string]$ArtPath,[Parameter(Mandatory)][string]$CalibrationPath,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path $PSScriptRoot)
Add-Type -AssemblyName System.Drawing
$refs=@([Drawing.Bitmap].Assembly.Location,[Drawing.Color].Assembly.Location,(Join-Path $PSHOME 'System.Private.Windows.GdiPlus.dll'),(Join-Path $PSHOME 'System.Private.Windows.Core.dll'))
Add-Type -ReferencedAssemblies $refs -Path @((Join-Path $PSScriptRoot 'CannonLayerPixels.cs'),(Join-Path $PSScriptRoot 'ShipFamilyPixels.cs'))
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$name=$Stem+'_Normal_v5_compat.png'
[ShipFamilyPixels]::CanonicalContact($ArtPath,(Join-Path $OutputDirectory $name))
$royalJson=Get-Content -LiteralPath (Join-Path $repo 'resources/ocean/Ship_CorpsRoyaux_ThreeMast_Layered_v4_compat.png.naval.json') -Raw
$atlas=$royalJson | ConvertFrom-Json;$referenceAtlas=$royalJson | ConvertFrom-Json
$calibration=Get-Content -LiteralPath $CalibrationPath -Raw | ConvertFrom-Json
if($calibration.Directions.Count -ne 8){throw 'Eight calibrated directions are required'}
$atlas.PixelsPerTile=18
function Point([double]$x,[double]$y){[pscustomobject]@{X=$x;Y=$y}}
function Mix($a,$b,[double]$t){Point ($a.X+($b.X-$a.X)*$t) ($a.Y+($b.Y-$a.Y)*$t)}
$bob=@(0,-1,0,1)
for($d=0;$d -lt 8;$d++){
    $pose=$calibration.Directions[$d]
    $helm=Point $pose.Helm[0] $pose.Helm[1];$mid=Point $pose.Mid[0] $pose.Mid[1];$bow=Point $pose.Bow[0] $pose.Bow[1]
    $port=Point $pose.Port[0] $pose.Port[1];$star=Point $pose.Starboard[0] $pose.Starboard[1]
    $original=$referenceAtlas.Directions[$d][0]
    $helmFacing=($original.CrewAnchors | Where-Object Role -EQ 0 | Select-Object -First 1).Facing
    $portFacing=($original.CrewAnchors | Where-Object Role -EQ 1 | Select-Object -First 1).Facing
    $starFacing=($original.CrewAnchors | Where-Object Role -EQ 2 | Select-Object -First 1).Facing
    function Centre([double]$y){if($y -le 0){Mix $helm $mid (($y+3.4)/3.4)}else{Mix $mid $bow ($y/3.5)}}
    for($phase=0;$phase -lt 4;$phase++){
        $frame=$atlas.Directions[$d][$phase];$dy=$bob[$phase]
        $frame.X=($d%2)*1536+$phase*384;$frame.Y=[int][Math]::Floor($d/2)*256
        $frame.AnchorX=$mid.X;$frame.AnchorY=$mid.Y
        $frame.DeckMasts=@(-2.5,0,2.5) | ForEach-Object {$p=Centre $_;Point $p.X ($p.Y+$dy)}
        $frame.DeckBow=Point $bow.X ($bow.Y+$dy)
        $frame.DeckPort=@(-2,0,2) | ForEach-Object {$p=Centre $_;$factor=1-.1*[Math]::Abs($_);Point ($p.X+($port.X-$mid.X)*$factor) ($p.Y+($port.Y-$mid.Y)*$factor+$dy)}
        $frame.DeckStarboard=@(-2,0,2) | ForEach-Object {$p=Centre $_;$factor=1-.1*[Math]::Abs($_);Point ($p.X+($star.X-$mid.X)*$factor) ($p.Y+($star.Y-$mid.Y)*$factor+$dy)}
        $crew=[Collections.Generic.List[object]]::new();$guns=[Collections.Generic.List[object]]::new()
        $crew.Add([pscustomobject]@{Role=0;Index=0;X=$helm.X;Y=$helm.Y+$dy;Facing=$helmFacing})
        for($side=0;$side -lt 2;$side++){
            $facing=if($side -eq 0){$portFacing}else{$starFacing}
            $points=if($side -eq 0){$pose.GunPort}else{$pose.GunStarboard}
            for($index=0;$index -lt 6;$index++){
                $sample=$original.CannonAnchors | Where-Object { $_.Index -eq ($index%3) -and $_.Starboard -eq ($side -eq 1) } | Select-Object -First 1
                $pivot=$points[$index];$foot=$pose.Crew[$side*6+$index]
                $crew.Add([pscustomobject]@{Role=1+$side;Index=$index;X=[double]$foot[0];Y=[double]$foot[1]+$dy;Facing=$facing})
                $guns.Add([pscustomobject]@{Starboard=($side -eq 1);Index=$index;Sprite=$sample.Sprite;X=[double]$pivot[0];Y=[double]$pivot[1]+$dy;BehindCrew=$sample.BehindCrew})
            }
        }
        $frame.CrewAnchors=$crew.ToArray();$frame.CannonAnchors=$guns.ToArray()
        $frame | Add-Member -NotePropertyName Slices -NotePropertyValue $null -Force
        $frame | Add-Member -NotePropertyName FlipHorizontal -NotePropertyValue $false -Force
        $frame | Add-Member -NotePropertyName RotationDegrees -NotePropertyValue 0 -Force
    }
}
$gunName=$Stem+'_Cannons_v5_compat.png'
$sprites=[int[]]@($atlas.Directions | ForEach-Object { $_[0].CannonAnchors | ForEach-Object {$_.Sprite}} | Sort-Object -Unique)
$gunScale=[ShipFamilyPixels]::ScaleCannonCells((Join-Path $repo ('resources/ocean/'+$referenceAtlas.CannonGraphics)),(Join-Path $OutputDirectory $gunName),$sprites,1)
$atlas.CannonGraphics=$gunName
$atlas.SourceSha256=(Get-FileHash -LiteralPath (Join-Path $OutputDirectory $name) -Algorithm SHA256).Hash.ToLowerInvariant()
$atlas.ReviewNotes='Ship of the line calibrated deck template: virtual centreline landmarks, real station anchors, 75% actors. Independent cannon raster scale '+$gunScale+'. Validate the eight views and combat-map passage after assembly.'
$json=$atlas | ConvertTo-Json -Depth 30 -Compress
if([Text.Encoding]::UTF8.GetByteCount($json) -gt 131072){throw 'Line ship metadata exceeds 128KiB'}
[IO.File]::WriteAllText((Join-Path $OutputDirectory ($name+'.naval.json')),$json,[Text.UTF8Encoding]::new($false))
Write-Output 'Created line-ship canonical reference and calibrated template.'
