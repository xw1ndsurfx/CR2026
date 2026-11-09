param([Parameter(Mandatory)][string]$OutputDirectory,[string]$Stem='Ship_CorpsRoyaux_ThreeMast')
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path $PSScriptRoot);$ocean=Join-Path $repo 'resources/ocean'
Add-Type -AssemblyName System.Drawing
$refs=@([Drawing.Bitmap].Assembly.Location,[Drawing.Color].Assembly.Location,(Join-Path $PSHOME 'System.Private.Windows.GdiPlus.dll'),(Join-Path $PSHOME 'System.Private.Windows.Core.dll'))
Add-Type -ReferencedAssemblies $refs -Path @((Join-Path $PSScriptRoot 'CannonLayerPixels.cs'),(Join-Path $PSScriptRoot 'ShipFamilyPixels.cs'))
$normal=if($Stem -eq 'Ship_CorpsRoyaux_ThreeMast'){'Ship_CorpsRoyaux_ThreeMast_Layered_v4_compat.png'}else{$Stem+'_Normal_v5_compat.png'}
$names=@($normal,($Stem+'_Idle_v5_compat.png'),($Stem+'_Damaged_v5_compat.png'),($Stem+'_Sinking_v5_compat.png'))
$covers=@();$fronts=@();$states=@();$pivots=[double[]]::new(96);$sprites=[int[]]::new(48)
for($i=0;$i -lt 48;$i++){$sprites[$i]=-1;$pivots[$i*2]=-1000000;$pivots[$i*2+1]=-1000000}
foreach($name in $names){$states+=Join-Path $ocean $name;$atlas=Get-Content -LiteralPath (Join-Path $ocean ($name+'.naval.json')) -Raw | ConvertFrom-Json;$covers+=if($atlas.CannonOcclusionGraphics){Join-Path $ocean $atlas.CannonOcclusionGraphics}else{''};$fronts+=if($atlas.ForegroundGraphics){Join-Path $ocean $atlas.ForegroundGraphics}else{''}}
$atlas=Get-Content -LiteralPath (Join-Path $ocean ($normal+'.naval.json')) -Raw | ConvertFrom-Json
for($d=0;$d -lt 8;$d++){foreach($a in $atlas.Directions[$d][0].CannonAnchors){$g=[int]$a.Index+$(if($a.Starboard){3}else{0});$n=$d*6+$g;$pivots[$n*2]=$a.X;$pivots[$n*2+1]=$a.Y;$sprites[$n]=$a.Sprite}}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$guns=if($atlas.CannonGraphics){Join-Path $ocean $atlas.CannonGraphics}else{''}
[ShipFamilyPixels]::Preview($states,$fronts,$covers,$guns,$pivots,$sprites,(Join-Path $OutputDirectory ($Stem+'_navigation_idle_degats_naufrage.png')),$false)
[ShipFamilyPixels]::Preview($states,$fronts,$covers,$guns,$pivots,$sprites,(Join-Path $OutputDirectory ($Stem+'_naufrage_4_phases.png')),$true)
Write-Output 'Created two registered contact sheets: eight views, four states / four sinking phases.'
