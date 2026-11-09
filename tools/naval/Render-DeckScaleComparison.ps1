param([Parameter(Mandatory)][string]$GameResources,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$refs=@([System.Drawing.Bitmap].Assembly.Location,[System.Drawing.Color].Assembly.Location,(Join-Path $PSHOME 'System.Private.Windows.GdiPlus.dll'),(Join-Path $PSHOME 'System.Private.Windows.Core.dll'))
Add-Type -ReferencedAssemblies $refs -Path (Join-Path $PSScriptRoot 'CannonLayerPixels.cs')
$repo=Split-Path (Split-Path $PSScriptRoot)
$ocean=Join-Path $repo 'resources/ocean'
$name='Ship_CorpsRoyaux_ThreeMast_Layered_v4_compat.png'
$atlas=Get-Content (Join-Path $ocean ($name+'.naval.json')) -Raw | ConvertFrom-Json
$crew=[Collections.Generic.List[double]]::new();$pivots=[Collections.Generic.List[double]]::new()
for($d=0;$d -lt 8;$d++){
    $pose=$atlas.Directions[$d][0]
    foreach($a in $pose.CrewAnchors | Where-Object {$_.Role -ne 3}){
        $far=$pose.CannonAnchors | Where-Object {$a.Role -in 1,2 -and $_.Index -eq $a.Index -and $_.Starboard -eq ($a.Role -eq 2) -and $_.BehindCrew}
        $crew.AddRange([double[]]@($a.X,$a.Y,$a.Facing,[int][bool]$far))
    }
    foreach($a in $pose.CannonAnchors){$pivots.AddRange([double[]]@($a.X,$a.Y))}
}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
[CannonLayerPixels]::ScaleComparison((Join-Path $ocean $name),(Join-Path $ocean $atlas.ForegroundGraphics),(Join-Path $ocean $atlas.CannonOcclusionGraphics),(Join-Path $ocean $atlas.CannonGraphics),(Join-Path $GameResources 'entities/player_human_male.png'),(Join-Path $GameResources 'paperdolls/Pants_Copper_Armor_male.png'),(Join-Path $GameResources 'paperdolls/Armor_Leather_male.png'),(Join-Path $GameResources 'paperdolls/Basic_Guard_Helmet_male.png'),$crew.ToArray(),$pivots.ToArray(),(Join-Path $OutputDirectory 'Comparatif_7_joueurs_100_75_50.png'))
Write-Output 'Rendered seven sample characters, six independent cannon angles, eight ship directions; columns 100%, 75%, 50%. Offline proof only.'
