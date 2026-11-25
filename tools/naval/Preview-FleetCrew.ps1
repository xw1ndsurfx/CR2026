param([Parameter(Mandatory)][string]$GameResources,[Parameter(Mandatory)][string]$OutputDirectory,[Parameter(Mandatory)][string]$Graphics)
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path $PSScriptRoot);$ocean=Join-Path $repo 'resources/ocean'
Add-Type -AssemblyName System.Drawing
$refs=@([Drawing.Bitmap].Assembly.Location,[Drawing.Color].Assembly.Location,(Join-Path $PSHOME 'System.Private.Windows.GdiPlus.dll'),(Join-Path $PSHOME 'System.Private.Windows.Core.dll'))
Add-Type -ReferencedAssemblies $refs -Path (Join-Path $PSScriptRoot 'CannonLayerPixels.cs')
function Pixels([string]$path){$bitmap=[Drawing.Bitmap]::new($path);try{,[CannonLayerPixels]::Read($bitmap)}finally{$bitmap.Dispose()}}
$atlas=Get-Content -LiteralPath (Join-Path $ocean ($Graphics+'.naval.json')) -Raw | ConvertFrom-Json
$body=Pixels (Join-Path $ocean $Graphics);$front=Pixels (Join-Path $ocean $atlas.ForegroundGraphics);$cover=Pixels (Join-Path $ocean $atlas.CannonOcclusionGraphics)
$guns=if($atlas.CannonGraphics){Pixels (Join-Path $ocean $atlas.CannonGraphics)}else{[int[]]::new(512*384)}
$sheetPaths=@('entities/player_human_male.png','paperdolls/Pants_Copper_Armor_male.png','paperdolls/Armor_Leather_male.png','paperdolls/Basic_Guard_Helmet_male.png')
$sheets=[int[][]]::new(4);$widths=[int[]]::new(4)
for($i=0;$i -lt 4;$i++){$bitmap=[Drawing.Bitmap]::new((Join-Path $GameResources $sheetPaths[$i]));try{$sheets[$i]=[CannonLayerPixels]::Read($bitmap);$widths[$i]=$bitmap.Width}finally{$bitmap.Dispose()}}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$files=[Collections.Generic.List[string]]::new()
for($d=0;$d -lt 8;$d++){
    $pose=$atlas.Directions[$d][0];$people=@($pose.CrewAnchors | Where-Object {[int]$_.Role -ne 3});$anchors=@($pose.CannonAnchors)
    if($Graphics -match '_ShipOfLine_'){
        foreach($person in $people | Where-Object {[int]$_.Role -in @(1,2) -and $_.Index -lt 3}){
            $index=[int]$person.Index;$point=if([int]$person.Role -eq 1){$pose.DeckPort[$index]}else{$pose.DeckStarboard[$index]}
            $helm=$pose.CrewAnchors | Where-Object Role -EQ 0 | Select-Object -First 1
            $y=($index-1)*2;$a=if($y -le 0){$pose.DeckMasts[0]}else{$pose.DeckMasts[1]};$b=if($y -le 0){$pose.DeckMasts[1]}else{$pose.DeckMasts[2]};$t=if($y -le 0){($y+2.5)/2.5}else{$y/2.5}
            $cx=$a.X+($b.X-$a.X)*$t;$cy=$a.Y+($b.Y-$a.Y)*$t
            $person.X=$cx+($point.X-$cx)*1.15/.78;$person.Y=$cy+($point.Y-$cy)*1.15/.78
        }
    }elseif($Graphics -notmatch '_(Sloop|Raft)_'){
        foreach($person in $people | Where-Object {[int]$_.Role -in @(1,2)}){
            $point=if([int]$person.Role -eq 1){$pose.DeckPort[$person.Index]}else{$pose.DeckStarboard[$person.Index]}
            $person.X=$point.X;$person.Y=$point.Y
        }
    }
    $people=@($people | Sort-Object Y)
    $data=[Collections.Generic.List[double]]::new();$data.AddRange([double[]]@($d,0,0,($atlas.PixelsPerTile/32)))
    foreach($person in $people){
        $row=switch([int]$person.Facing){0{0}1{3}2{1}3{2}default{0}}
        $gun=$anchors | Where-Object {$_.Index -eq $person.Index -and [int]$person.Role -eq $(if($_.Starboard){2}else{1})} | Select-Object -First 1
        $data.AddRange([double[]]@($person.X,$person.Y,$row,0,[int][bool]$gun.BehindCrew))
    }
    foreach($gun in $anchors){$data.AddRange([double[]]@($gun.X,$gun.Y,$(if($gun.Starboard){25}else{-25})))}
    $sprites=[int[]]@($anchors | ForEach-Object {$_.Sprite})
    $pixels=[CannonLayerPixels]::DeckActorsFrame($body,$front,$cover,$guns,$sheets,$widths,$data.ToArray(),$people.Count,$anchors.Count,$sprites)
    $path=Join-Path $OutputDirectory ('crew-'+$d+'.png');[CannonLayerPixels]::Save($pixels,768,512,$path);$files.Add($path)
}
[CannonLayerPixels]::Contact($files.ToArray(),2,(Join-Path $OutputDirectory 'Equipage_75_8_angles.png'))
Write-Output 'Rendered actual character layers at all authored stations, with opposing cannon aim and foreground. Offline preview, not a connected client capture.'
