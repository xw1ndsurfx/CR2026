param([string]$OceanDirectory,[Parameter(Mandatory)][string[]]$Stems)
$ErrorActionPreference='Stop'
if(!$OceanDirectory){$OceanDirectory=Join-Path (Split-Path (Split-Path $PSScriptRoot)) 'resources/ocean'}
Add-Type -AssemblyName System.Drawing
$refs=@([Drawing.Bitmap].Assembly.Location,[Drawing.Color].Assembly.Location,(Join-Path $PSHOME 'System.Private.Windows.GdiPlus.dll'),(Join-Path $PSHOME 'System.Private.Windows.Core.dll'))
Add-Type -ReferencedAssemblies $refs -Path @((Join-Path $PSScriptRoot 'CannonLayerPixels.cs'),(Join-Path $PSScriptRoot 'ShipFamilyPixels.cs'))
foreach($stem in $Stems){
    $name=if($stem -eq 'Ship_CorpsRoyaux_ThreeMast'){'Ship_CorpsRoyaux_ThreeMast_Layered_v4_compat.png'}else{$stem+'_Normal_v5_compat.png'}
    $meta=Get-Content -LiteralPath (Join-Path $OceanDirectory ($name+'.naval.json')) -Raw | ConvertFrom-Json
    $frame=$meta.Directions[4][0];$stations=[Collections.Generic.List[double]]::new()
    foreach($gun in $frame.CannonAnchors){$stations.Add($gun.Sprite);$stations.Add($gun.X);$stations.Add($gun.Y)}
    $gunPath=if($meta.CannonGraphics){Join-Path $OceanDirectory $meta.CannonGraphics}else{''}
    [ShipFamilyPixels]::Thumbnail((Join-Path $OceanDirectory $name),(Join-Path $OceanDirectory $meta.ForegroundGraphics),(Join-Path $OceanDirectory $meta.CannonOcclusionGraphics),$gunPath,$stations.ToArray(),$frame.X,$frame.Y,(Join-Path $OceanDirectory ($stem+'_Preview_v5.png')))
}
Write-Output ('Created '+$Stems.Count+' transparent ship previews with foreground and independent guns.')
