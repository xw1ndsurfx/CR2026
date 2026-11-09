param([Parameter(Mandatory)][string]$OutputDirectory,[string]$Stem='Ship_CorpsRoyaux_ThreeMast')
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path $PSScriptRoot);$ocean=Join-Path $repo 'resources/ocean'
Add-Type -AssemblyName System.Drawing
$refs=@([Drawing.Bitmap].Assembly.Location,[Drawing.Color].Assembly.Location,(Join-Path $PSHOME 'System.Private.Windows.GdiPlus.dll'),(Join-Path $PSHOME 'System.Private.Windows.Core.dll'))
Add-Type -ReferencedAssemblies $refs -Path @((Join-Path $PSScriptRoot 'CannonLayerPixels.cs'),(Join-Path $PSScriptRoot 'ShipFamilyPixels.cs'))
$normal=if($Stem -eq 'Ship_CorpsRoyaux_ThreeMast'){'Ship_CorpsRoyaux_ThreeMast_Layered_v4_compat.png'}else{$Stem+'_Normal_v5_compat.png'}
$names=@($normal,($Stem+'_Idle_v5_compat.png'),($Stem+'_Damaged_v5_compat.png'),($Stem+'_Sinking_v5_compat.png'))
$covers=@();$fronts=@();$states=@();$pivots=[double[]]::new(96)
foreach($name in $names){$states+=Join-Path $ocean $name;$atlas=Get-Content -LiteralPath (Join-Path $ocean ($name+'.naval.json')) -Raw | ConvertFrom-Json;$covers+=if($atlas.CannonOcclusionGraphics){Join-Path $ocean $atlas.CannonOcclusionGraphics}else{''};$fronts+=if($atlas.ForegroundGraphics){Join-Path $ocean $atlas.ForegroundGraphics}else{''}}
$atlas=Get-Content -LiteralPath (Join-Path $ocean ($normal+'.naval.json')) -Raw | ConvertFrom-Json
for($d=0;$d -lt 8;$d++){for($g=0;$g -lt 6;$g++){$a=$atlas.Directions[$d][0].CannonAnchors[$g];$pivots[($d*6+$g)*2]=$a.X;$pivots[($d*6+$g)*2+1]=$a.Y}}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$guns=Join-Path $ocean $atlas.CannonGraphics
[ShipFamilyPixels]::Preview($states,$fronts,$covers,$guns,$pivots,(Join-Path $OutputDirectory ($Stem+'_navigation_idle_degats_naufrage.png')),$false)
[ShipFamilyPixels]::Preview($states,$fronts,$covers,$guns,$pivots,(Join-Path $OutputDirectory ($Stem+'_naufrage_4_phases.png')),$true)
Write-Output 'Created two registered contact sheets: eight views, four states / four sinking phases.'
