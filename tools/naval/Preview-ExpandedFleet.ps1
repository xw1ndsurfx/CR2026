param([Parameter(Mandatory)][string]$OutputPath)
$ErrorActionPreference='Stop'
$ocean=Join-Path (Split-Path (Split-Path $PSScriptRoot)) 'resources/ocean'
Add-Type -AssemblyName System.Drawing
$factions=@('Adventurer','CorpsRoyaux','Herbonia','Majestya','Skeleton','Ghost','Pirate')
$titles=@('Aventurier','Corps Royaux','Herbonia','Majestya','Squelette','Fantôme','Pirate')
$hulls=@('ShipOfLine','ThreeMast','Sloop','Raft','Fishing');$rows=@('Vaisseau de ligne','Trois-mâts','Sloop','Radeau','Bateau de pêche')
$bitmap=[Drawing.Bitmap]::new(2530,1510,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g=[Drawing.Graphics]::FromImage($bitmap)
$heading=[Drawing.Font]::new('Segoe UI',30,[Drawing.FontStyle]::Bold,[Drawing.GraphicsUnit]::Pixel)
$label=[Drawing.Font]::new('Segoe UI',21,[Drawing.FontStyle]::Bold,[Drawing.GraphicsUnit]::Pixel)
$ink=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,232,222,186))
$grid=[Drawing.Pen]::new([Drawing.Color]::FromArgb(255,78,119,140),1)
try {
    $g.Clear([Drawing.Color]::FromArgb(255,37,77,102));$g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
    $g.DrawString('Corps Royaux - 35 navires, à la même échelle',$heading,$ink,24,18)
    for($column=0;$column -lt $factions.Count;$column++){
        $left=210+$column*330
        $g.DrawString($titles[$column],$label,$ink,$left+20,66)
        $g.DrawLine($grid,$left,98,$left,1500)
        for($row=0;$row -lt $hulls.Count;$row++){
            $top=102+$row*280
            if($column -eq 0){$g.DrawString($rows[$row],$label,$ink,20,$top+110);$g.DrawLine($grid,14,$top,2519,$top)}
            $stem='Ship_'+$factions[$column]+'_'+$hulls[$row]
            $filename=if($stem -eq 'Ship_CorpsRoyaux_ThreeMast'){$stem+'_Layered_v4_compat.png'}else{$stem+'_Normal_v5_compat.png'}
            $atlas=Get-Content -LiteralPath (Join-Path $ocean ($filename+'.naval.json')) -Raw | ConvertFrom-Json
            $pose=$atlas.Directions[2][0];$scale=15/$atlas.PixelsPerTile
            $x=$left+165-$pose.Width*$scale/2;$y=$top+149-$pose.Height*$scale/2
            $origin=[Drawing.Rectangle]::new($pose.X,$pose.Y,$pose.Width,$pose.Height)
            $dest=[Drawing.Rectangle]::new([int]$x,[int]$y,[int]($pose.Width*$scale),[int]($pose.Height*$scale))
            function Draw-Layer([string]$Name){if($Name){$layer=[Drawing.Bitmap]::new((Join-Path $ocean $Name));try{$g.DrawImage($layer,$dest,$origin,[Drawing.GraphicsUnit]::Pixel)}finally{$layer.Dispose()}}}
            Draw-Layer $filename;Draw-Layer $atlas.ForegroundGraphics
            if($atlas.CannonGraphics){
                $guns=[Drawing.Bitmap]::new((Join-Path $ocean $atlas.CannonGraphics))
                try{foreach($gun in $pose.CannonAnchors){
                    $src=[Drawing.Rectangle]::new(($gun.Sprite%8)*64,[int][Math]::Floor($gun.Sprite/8)*64,64,64)
                    $gunDest=[Drawing.Rectangle]::new([int]($x+($gun.X-32)*$scale),[int]($y+($gun.Y-32)*$scale),[int](64*$scale),[int](64*$scale))
                    $g.DrawImage($guns,$gunDest,$src,[Drawing.GraphicsUnit]::Pixel)
                }}finally{$guns.Dispose()}
            }
            Draw-Layer $atlas.CannonOcclusionGraphics
        }
    }
    $bitmap.Save($OutputPath,[Drawing.Imaging.ImageFormat]::Png)
}
finally{$grid.Dispose();$ink.Dispose();$label.Dispose();$heading.Dispose();$g.Dispose();$bitmap.Dispose()}
