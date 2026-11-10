param([Parameter(Mandatory)][string]$ArtPath,[Parameter(Mandatory)][string]$OutputDirectory,[string]$Stem='Ship_CorpsRoyaux_ThreeMast',[switch]$Spectral,[string]$TemplatePath)
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path $PSScriptRoot)
Add-Type -AssemblyName System.Drawing
$refs=@([Drawing.Bitmap].Assembly.Location,[Drawing.Color].Assembly.Location,(Join-Path $PSHOME 'System.Private.Windows.GdiPlus.dll'),(Join-Path $PSHOME 'System.Private.Windows.Core.dll'))
Add-Type -ReferencedAssemblies $refs -Path @((Join-Path $PSScriptRoot 'CannonLayerPixels.cs'),(Join-Path $PSScriptRoot 'ShipFamilyPixels.cs'))
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$name=$Stem+'_Sinking_v5_compat.png'
[ShipFamilyPixels]::Sinking($ArtPath,(Join-Path $OutputDirectory $name),$Spectral.IsPresent)
if(!$TemplatePath){$TemplatePath=Join-Path $repo 'resources/ocean/Ship_CorpsRoyaux_ThreeMast_Layered_v4_compat.png.naval.json'}
$atlas=Get-Content -LiteralPath $TemplatePath -Raw | ConvertFrom-Json
$atlas.Loop=$false;$atlas.ForegroundGraphics='';$atlas.CannonGraphics='';$atlas.CannonOcclusionGraphics=''
foreach($direction in $atlas.Directions){foreach($frame in $direction){$frame.CrewAnchors=@();$frame.CannonAnchors=@();$frame | Add-Member -NotePropertyName Slices -NotePropertyValue $null -Force}}
$atlas.SourceSha256=(Get-FileHash -LiteralPath (Join-Path $OutputDirectory $name) -Algorithm SHA256).Hash.ToLowerInvariant()
$atlas.ReviewNotes=$Stem+' sinking: eight authored broken-hull source views, four descending frames clipped below the fixed waterline, no crew or gun layers, final frame transparent. Play at 500ms/frame.'
$json=$atlas | ConvertTo-Json -Depth 30 -Compress
[IO.File]::WriteAllText((Join-Path $OutputDirectory ($name+'.naval.json')),$json,[Text.UTF8Encoding]::new($false))
Write-Output 'Created eight sinking views with four frames and a transparent final state.'
