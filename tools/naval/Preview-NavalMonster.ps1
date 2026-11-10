param([Parameter(Mandatory)][string]$OutputDirectory,[ValidateSet('Kraken','SeaSerpent','Leviathan')][string]$Monster='Kraken')
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path $PSScriptRoot);$ocean=Join-Path $repo 'resources/ocean'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
Add-Type -AssemblyName System.Drawing
$textures=@{}
function Texture([string]$name){
    if(!$textures.ContainsKey($name)){$textures[$name]=[Drawing.Bitmap]::new((Join-Path $ocean $name))}
    return $textures[$name]
}
$stem='Ship_CorpsRoyaux_ShipOfLine'
$monsterTexture=Texture ('Naval_'+$Monster+'_v1.png')
$atlases=@{}
foreach($state in @('Idle','Damaged','Sinking')){
    $name=$stem+'_'+$state+'_v5_compat.png'
    $atlases[$state]=Get-Content -LiteralPath (Join-Path $ocean ($name+'.naval.json')) -Raw | ConvertFrom-Json
}
$timeline=[Collections.Generic.List[object]]::new()
function Frame([string]$ShipState,[int]$ShipPhase,[int]$MonsterRow,[int]$MonsterPhase,[bool]$Foreground,[int]$Milliseconds){
    $canvas=[Drawing.Bitmap]::new(960,720,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g=[Drawing.Graphics]::FromImage($canvas)
    try {
        $g.Clear([Drawing.Color]::FromArgb(255,32,80,108));$g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
        $destMonster=if($Foreground){[Drawing.Rectangle]::new(65,0,830,720)}else{[Drawing.Rectangle]::new(15,16,610,610)}
        $mw=[int]($monsterTexture.Width/4);$mh=[int]($monsterTexture.Height/6)
        $srcMonster=[Drawing.Rectangle]::new($MonsterPhase*$mw,$MonsterRow*$mh,$mw,$mh)
        if($MonsterRow -ge 0 -and !$Foreground){$g.DrawImage($monsterTexture,$destMonster,$srcMonster,[Drawing.GraphicsUnit]::Pixel)}
        if($ShipState){
            $atlas=$atlases[$ShipState];$pose=$atlas.Directions[2][$ShipPhase]
            $scale=32/$atlas.PixelsPerTile;$crush=if($Foreground){$MonsterPhase/3d}else{0}
            $scale*=1-.18*$crush
            $left=480-$pose.AnchorX*$scale;$top=370-$pose.AnchorY*$scale+10*$crush
            $source=[Drawing.Rectangle]::new($pose.X,$pose.Y,$pose.Width,$pose.Height)
            $destination=[Drawing.Rectangle]::new([int]$left,[int]$top,[int]($pose.Width*$scale),[int]($pose.Height*$scale))
            function Layer([string]$filename){if($filename){$g.DrawImage((Texture $filename),$destination,$source,[Drawing.GraphicsUnit]::Pixel)}}
            Layer ($stem+'_'+$ShipState+'_v5_compat.png');Layer $atlas.ForegroundGraphics
            if($atlas.CannonGraphics){
                $gunTexture=Texture $atlas.CannonGraphics
                foreach($gun in $pose.CannonAnchors){
                    $gunSource=[Drawing.Rectangle]::new(($gun.Sprite%8)*64,[int][Math]::Floor($gun.Sprite/8)*64,64,64)
                    $gunDestination=[Drawing.Rectangle]::new([int]($left+($gun.X-32)*$scale),[int]($top+($gun.Y-32)*$scale),[int](64*$scale),[int](64*$scale))
                    $g.DrawImage($gunTexture,$gunDestination,$gunSource,[Drawing.GraphicsUnit]::Pixel)
                }
            }
            Layer $atlas.CannonOcclusionGraphics
        }
        if($MonsterRow -ge 0 -and $Foreground){$g.DrawImage($monsterTexture,$destMonster,$srcMonster,[Drawing.GraphicsUnit]::Pixel)}
        $filename='frame-{0:D3}.png' -f $timeline.Count
        $canvas.Save((Join-Path $OutputDirectory $filename),[Drawing.Imaging.ImageFormat]::Png)
        $timeline.Add([pscustomobject]@{File=$filename;Milliseconds=$Milliseconds})
    }
    finally{$g.Dispose();$canvas.Dispose()}
}
try {
    foreach($i in 0..3){Frame 'Idle' $i 1 $i $false 750}
    foreach($repeat in 0..2){foreach($i in 0..3){Frame 'Damaged' $i 2 $i $false 180}}
    foreach($i in 0..3){Frame 'Damaged' $i 5 $i $true 1000}
    foreach($i in 0..3){Frame 'Sinking' $i 4 $i $false 500}
    Frame '' 0 -1 0 $false 1000
    ConvertTo-Json -InputObject $timeline.ToArray() | Set-Content -LiteralPath (Join-Path $OutputDirectory 'manifest.json') -Encoding utf8
    python (Join-Path $PSScriptRoot 'pack-deck-apng.py') $OutputDirectory (Join-Path $OutputDirectory ($Monster+'_assaut.png'))
    if($LASTEXITCODE){throw 'Could not package the APNG preview.'}
}
finally{foreach($texture in $textures.Values){$texture.Dispose()}}
