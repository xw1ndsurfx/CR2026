param([Parameter(Mandatory)][string]$GameResources,[Parameter(Mandatory)][string]$OutputDirectory,[string]$ReplayPath)
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path $PSScriptRoot)
$bin=Join-Path $repo 'Intersect.Tests.Server/bin/Debug/net8.0/win-x64'
if(!$ReplayPath){$ReplayPath=Join-Path $bin 'deck-walk-replay.json'}
[Reflection.Assembly]::LoadFrom((Join-Path $bin 'Intersect.Framework.dll')) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $bin 'Intersect.Framework.Core.dll')) | Out-Null
Add-Type -AssemblyName System.Drawing
$refs=@([System.Drawing.Bitmap].Assembly.Location,[System.Drawing.Color].Assembly.Location,(Join-Path $PSHOME 'System.Private.Windows.GdiPlus.dll'),(Join-Path $PSHOME 'System.Private.Windows.Core.dll'))
Add-Type -ReferencedAssemblies $refs -Path (Join-Path $PSScriptRoot 'CannonLayerPixels.cs')
$ocean=Join-Path $repo 'resources/ocean'
$name='Ship_CorpsRoyaux_ThreeMast_Layered_v4_compat.png'
$atlas=[Newtonsoft.Json.JsonConvert]::DeserializeObject((Get-Content (Join-Path $ocean ($name+'.naval.json')) -Raw),[Intersect.Framework.Core.Naval.ShipSpriteAtlas])
$replay=[Newtonsoft.Json.JsonConvert]::DeserializeObject((Get-Content -LiteralPath $ReplayPath -Raw),[Intersect.Network.Packets.Server.ShipSnapshot[]])
function Pixels([string]$path){$b=[Drawing.Bitmap]::new($path);try{,[CannonLayerPixels]::Read($b)}finally{$b.Dispose()}}
$ship=Pixels (Join-Path $ocean $name);$fore=Pixels (Join-Path $ocean $atlas.ForegroundGraphics)
$cover=Pixels (Join-Path $ocean $atlas.CannonOcclusionGraphics);$guns=Pixels (Join-Path $ocean $atlas.CannonGraphics)
$sheetPaths=@('entities/player_human_male.png','paperdolls/Pants_Copper_Armor_male.png','paperdolls/Armor_Leather_male.png','paperdolls/Basic_Guard_Helmet_male.png')
$sheets=[int[][]]::new(4);$widths=[int[]]::new(4)
for($i=0;$i -lt 4;$i++){$b=[Drawing.Bitmap]::new((Join-Path $GameResources $sheetPaths[$i]));try{$sheets[$i]=[CannonLayerPixels]::Read($b);$widths[$i]=$b.Width}finally{$b.Dispose()}}
function FrameData($snapshot,[int]$index,[int]$direction=-1){
    if($direction -lt 0){$direction=[Intersect.Framework.Core.Naval.ShipMovement]::DirectionRow($snapshot.Heading)}
    $phase=[int][Math]::Floor($index*100/180)%4;$pose=$atlas.Directions[$direction][$phase]
    $entries=@()
    foreach($member in $snapshot.DeckMembers){
        $p=[Intersect.Framework.Core.Naval.DeckPoint]::new($member.X,$member.Y)
        $source=[Intersect.Framework.Core.Naval.ShipDeckProjection]::Project($pose,$p)
        $seat=$snapshot.CrewStations | Where-Object {$_.PlayerId -eq $member.PlayerId} | Select-Object -First 1
        $sx=if($seat){if($seat.Starboard){1}else{-1}}else{$member.FacingX}
        $sy=if($seat){0}else{$member.FacingY}
        $look=[Intersect.Framework.Core.Naval.ShipDeckProjection]::Project($pose,[Intersect.Framework.Core.Naval.DeckPoint]::new(($p.X+$sx*.1),($p.Y+$sy*.1)))
        $dx=$look.X-$source.X;$dy=$look.Y-$source.Y
        $row=if([Math]::Abs($dx) -gt [Math]::Abs($dy)){if($dx -lt 0){1}else{2}}else{if($dy -lt 0){3}else{0}}
        $far=$false
        if($seat){$far=($pose.CannonAnchors | Where-Object {$_.Starboard -eq $seat.Starboard -and $_.Index -eq $seat.Index}).BehindCrew}
        $entries+=@{X=$source.X;Y=$source.Y;Row=$row;Walking=$member.Walking;Far=$far}
    }
    if($entries.Count -ne 7){throw 'Replay must contain seven actual server deck members'}
    $data=[Collections.Generic.List[double]]::new();$data.AddRange([double[]]@($direction,$phase,($index*100),.75))
    foreach($entry in $entries | Sort-Object Y){$data.AddRange([double[]]@($entry.X,$entry.Y,$entry.Row,[int]$entry.Walking,[int]$entry.Far))}
    foreach($anchor in $pose.CannonAnchors){
        $seat=$snapshot.CrewStations | Where-Object {$_.Starboard -eq $anchor.Starboard -and $_.Index -eq $anchor.Index} | Select-Object -First 1
        $data.AddRange([double[]]@($anchor.X,$anchor.Y,$(if($seat){$seat.Yaw}else{0})))
    }
    ,$data.ToArray()
}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$framesDirectory=Join-Path $OutputDirectory 'frames';New-Item -ItemType Directory -Force -Path $framesDirectory | Out-Null
$paths=[Collections.Generic.List[string]]::new()
for($i=0;$i -lt $replay.Count;$i+=2){
    $data=FrameData $replay[$i] $i
    $pixels=[CannonLayerPixels]::DeckReplayFrame($ship,$fore,$cover,$guns,$sheets,$widths,$data)
    $path=Join-Path $framesDirectory ('frame-{0:D4}.png' -f $paths.Count)
    [CannonLayerPixels]::Save($pixels,768,512,$path);$paths.Add($path)
}
[CannonLayerPixels]::Contact([string[]]@($paths[0],$paths[[int]($paths.Count/3)],$paths[[int]($paths.Count*2/3)],$paths[$paths.Count-1]),2,(Join-Path $OutputDirectory 'Apercu_deplacement_75.png'))
@($paths.ToArray() | ForEach-Object {[IO.Path]::GetFileName($_)}) | ConvertTo-Json | Set-Content (Join-Path $framesDirectory 'manifest.json') -Encoding utf8
$masks=[Collections.Generic.List[string]]::new()
for($d=0;$d -lt 8;$d++){
    $data=FrameData $replay[$replay.Count-1] 0 $d
    $pixels=[CannonLayerPixels]::DeckReplayFrame($ship,$fore,$cover,$guns,$sheets,$widths,$data)
    $points=[Collections.Generic.List[double]]::new()
    for($y=-3.5;$y -le 3.5;$y+=.12){for($x=-1.1;$x -le 1.1;$x+=.12){
        $p=[Intersect.Framework.Core.Naval.DeckPoint]::new($x,$y)
        if([Intersect.Framework.Core.Naval.ShipDeck]::Contains($p)){$q=[Intersect.Framework.Core.Naval.ShipDeckProjection]::Project($atlas.Directions[$d][0],$p);$points.AddRange([double[]]@($q.X,$q.Y))}
    }}
    [CannonLayerPixels]::DotMask($pixels,$points.ToArray());$path=Join-Path $OutputDirectory ('mask-'+$d+'.png')
    [CannonLayerPixels]::Save($pixels,768,512,$path);$masks.Add($path)
}
[CannonLayerPixels]::Contact($masks.ToArray(),2,(Join-Path $OutputDirectory 'Masque_collision_8_vues.png'))
Copy-Item -LiteralPath $ReplayPath -Destination (Join-Path $OutputDirectory 'replay-serveur.json')
Write-Output ('Rendered '+$paths.Count+' frames from '+$replay.Count+' MessagePack-round-tripped server snapshots. Offline replay, not a live client capture.')
