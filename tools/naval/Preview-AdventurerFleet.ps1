param([Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path $PSScriptRoot);$ocean=Join-Path $repo 'resources/ocean'
Add-Type -AssemblyName System.Drawing
$refs=@([Drawing.Bitmap].Assembly.Location,[Drawing.Color].Assembly.Location,(Join-Path $PSHOME 'System.Private.Windows.GdiPlus.dll'),(Join-Path $PSHOME 'System.Private.Windows.Core.dll'))
Add-Type -ReferencedAssemblies $refs -Path @((Join-Path $PSScriptRoot 'CannonLayerPixels.cs'),(Join-Path $PSScriptRoot 'ShipFamilyPixels.cs'))
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$entries=@(@('ThreeMast','Trois-mâts aventurier','7 joueurs · 6 canons'),@('Fishing','Bateau de pêche aventurier','7 joueurs · 6 postes de pêche'),@('Sloop','Sloop aventurier','3 joueurs · 2 canons'),@('Raft','Radeau aventurier','2 joueurs · 1 canon avant'))
$result=[Drawing.Bitmap]::new(1120,870);$g=[Drawing.Graphics]::FromImage($result)
$font=[Drawing.Font]::new('Segoe UI',18,[Drawing.FontStyle]::Bold);$small=[Drawing.Font]::new('Segoe UI',12)
try{
    $g.Clear([Drawing.Color]::FromArgb(24,37,46));$g.DrawString('Bases aventurier · Échelle relative en jeu',$font,[Drawing.Brushes]::Ivory,20,12)
    for($i=0;$i -lt $entries.Count;$i++){
        $entry=$entries[$i];$stem='Ship_Adventurer_'+$entry[0];$name=$stem+'_Normal_v5_compat.png'
        $meta=Get-Content -LiteralPath (Join-Path $ocean ($name+'.naval.json')) -Raw | ConvertFrom-Json
        $frame=$meta.Directions[3][0];$stations=[Collections.Generic.List[double]]::new()
        foreach($gun in $frame.CannonAnchors){$stations.AddRange([double[]]@($gun.Sprite,$gun.X,$gun.Y))}
        $gunPath=if($meta.CannonGraphics){Join-Path $ocean $meta.CannonGraphics}else{''}
        $path=Join-Path $OutputDirectory ($stem+'_view.png')
        [ShipFamilyPixels]::Thumbnail((Join-Path $ocean $name),(Join-Path $ocean $meta.ForegroundGraphics),(Join-Path $ocean $meta.CannonOcclusionGraphics),$gunPath,$stations.ToArray(),$frame.X,$frame.Y,$path)
        $bitmap=[Drawing.Bitmap]::new($path)
        try{
            $x=($i%2)*560;$y=50+[int][Math]::Floor($i/2)*410
            $g.FillRectangle([Drawing.Brushes]::SteelBlue,$x+4,$y,552,405)
            $g.DrawString($entry[1],$font,[Drawing.Brushes]::Ivory,$x+18,$y+8)
            $g.DrawString($entry[2],$small,[Drawing.Brushes]::Ivory,$x+18,$y+40)
            $scale=32/$meta.PixelsPerTile;$width=[int](384*$scale);$height=[int](256*$scale)
            $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
            $g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
            $g.DrawImage($bitmap,[Drawing.Rectangle]::new($x+[int]((560-$width)/2),$y+400-$height,$width,$height),[Drawing.Rectangle]::new(0,0,384,256),[Drawing.GraphicsUnit]::Pixel)
        }finally{$bitmap.Dispose()}
    }
    $result.Save((Join-Path $OutputDirectory 'Aventurier_quatre_types.png'),[Drawing.Imaging.ImageFormat]::Png)
}finally{$g.Dispose();$result.Dispose();$font.Dispose();$small.Dispose()}
Write-Output 'Created four neutral player boats at their relative in-game sprite scale.'
