param(
    [Parameter(Mandatory=$true)][string]$ResourceRoot,
    [Parameter(Mandatory=$true)][string]$MapScreenshot,
    [string]$CatalogPath = "$PSScriptRoot/../Intersect.Editor/Resources/EditorHub/Catalog.json",
    [string]$OutputDirectory = "$PSScriptRoot/../Intersect.Editor/Resources/EditorHub/Art"
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$definitions = Get-Content -LiteralPath $CatalogPath -Raw | ConvertFrom-Json
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$map = [System.Drawing.Bitmap]::FromFile($MapScreenshot)
$sources = [System.Collections.Generic.List[object]]::new()
function Draw-Asset($graphics, [string]$relative, [int]$centerX, [int]$bottom, [bool]$sheet = $false) {
    $path = Join-Path $ResourceRoot $relative
    if (!(Test-Path -LiteralPath $path)) { throw "Missing authorized game resource: $relative" }
    $image = [System.Drawing.Bitmap]::FromFile($path)
    try {
        $w = $image.Width; $h = $image.Height
        if ($sheet) {
            $w = [Math]::Min(128,$w); $h = [Math]::Min(128,$h)
            $left=$w; $top=$h; $right=-1; $edge=-1
            for ($yy=0; $yy -lt $h; $yy++) { for ($xx=0; $xx -lt $w; $xx++) {
                if ($image.GetPixel($xx,$yy).A -gt 0) { $left=[Math]::Min($left,$xx); $top=[Math]::Min($top,$yy); $right=[Math]::Max($right,$xx); $edge=[Math]::Max($edge,$yy) }
            } }
            if ($right -ge $left) { $trim=$image.Clone([System.Drawing.Rectangle]::new($left,$top,$right-$left+1,$edge-$top+1),[System.Drawing.Imaging.PixelFormat]::Format32bppArgb); $image.Dispose(); $image=$trim; $w=$image.Width; $h=$image.Height }
        }
        if ($relative.StartsWith('animations/AFFICHE_')) { $w = 74; $h = 87 }
        $scale = [Math]::Max(1, [Math]::Min(2, [int][Math]::Floor(80 / [Math]::Max($w,$h))))
        $dest = [System.Drawing.Rectangle]::new($centerX - [int]($w*$scale/2), $bottom - $h*$scale, $w*$scale, $h*$scale)
        $graphics.DrawImage($image, $dest, 0, 0, $w, $h, [System.Drawing.GraphicsUnit]::Pixel)
    } finally { $image.Dispose() }
}
try {
    $index = 0
    foreach ($entry in $definitions) {
        $canvas = [System.Drawing.Bitmap]::new(256,96)
        $g = [System.Drawing.Graphics]::FromImage($canvas)
        try {
            $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
            $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
            $crop = [System.Drawing.Rectangle]::new(430 + ($index % 12)*24,145 + ($index % 12)*30,256,96)
            $g.DrawImage($map,[System.Drawing.Rectangle]::new(0,0,256,96),$crop,[System.Drawing.GraphicsUnit]::Pixel)
            $opacity = if ($entry.id -eq 'map') { 18 } elseif ($entry.id -in @('npcEditorToolStripMenuItem','classEditorToolStripMenuItem','mapEvents','balanceLabEditorToolStripMenuItem')) { 110 } else { 215 }
            $veil = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb($opacity,12,21,33))
            try { $g.FillRectangle($veil,0,0,256,96) } finally { $veil.Dispose() }
            $used = [System.Collections.Generic.List[string]]::new()
            $sign = "animations/AFFICHE_$($entry.sign).png"
            switch ($entry.id) {
                'map' { }
                'mapEvents' { Draw-Asset $g 'entities/Soldier_Polonya.png' 94 92 $true; Draw-Asset $g $sign 180 92; $used.Add('entities/Soldier_Polonya.png') }
                'classEditorToolStripMenuItem' {
                    Draw-Asset $g 'entities/Soldier_Polonya.png' 58 94 $true
                    Draw-Asset $g 'entities/Body_Lizard.png' 125 94 $true
                    Draw-Asset $g 'entities/Soldier2_Polonya.png' 194 94 $true
                    $used.Add('entities/Soldier_Polonya.png'); $used.Add('entities/Body_Lizard.png'); $used.Add('entities/Soldier2_Polonya.png')
                }
                'npcEditorToolStripMenuItem' { Draw-Asset $g 'entities/Soldier_Polonya.png' 125 94 $true; $used.Add('entities/Soldier_Polonya.png') }
                'itemEditorToolStripMenuItem' {
                    foreach ($pair in @(@('items/Shield_Wooden_Item.png',54),@('items/Potion_of_Health_Vitality_Greater.png',126),@('items/Sword_Copper.png',199))) { Draw-Asset $g $pair[0] $pair[1] 86; $used.Add($pair[0]) }
                }
                'balanceLabEditorToolStripMenuItem' {
                    Draw-Asset $g 'entities/M_Rat.png' 125 95 $true; $used.Add('entities/M_Rat.png')
                    $g.FillRectangle([System.Drawing.Brushes]::Black,69,10,118,10); $g.FillRectangle([System.Drawing.Brushes]::DarkRed,72,13,112,4); $g.FillRectangle([System.Drawing.Brushes]::OrangeRed,72,13,78,4)
                }
                'uiDesigner' {
                    $g.FillRectangle([System.Drawing.Brushes]::SlateGray,52,12,152,76); $g.FillRectangle([System.Drawing.Brushes]::MidnightBlue,55,15,146,70)
                    $g.FillRectangle([System.Drawing.Brushes]::SteelBlue,55,15,146,10); $g.FillRectangle([System.Drawing.Brushes]::SlateGray,63,33,58,8)
                    $g.FillRectangle([System.Drawing.Brushes]::RoyalBlue,117,64,68,12)
                    Draw-Asset $g 'items/Potion_of_Mana_Greater.png' 164 60; $used.Add('items/Potion_of_Mana_Greater.png')
                }
                default {
                    Draw-Asset $g $sign 128 92; $used.Add($sign)
                    if ($entry.category -eq 'Game' -and $entry.tags -contains 'Economy') { Draw-Asset $g 'items/Currency_Aureons.png' 58 88; $used.Add('items/Currency_Aureons.png') }
                    elseif ($entry.category -eq 'Events') { Draw-Asset $g 'entities/Soldier_Polonya.png' 61 94 $true; $used.Add('entities/Soldier_Polonya.png') }
                }
            }
            $canvas.Save((Join-Path $OutputDirectory $entry.art),[System.Drawing.Imaging.ImageFormat]::Png)
            $sources.Add([pscustomobject]@{Id=$entry.id;Artwork=$entry.art;Source='User-provided Map Editor capture + '+($used -join '; ');Crop='256x96 scenery; entity first 128x128 cell with transparent padding trimmed; sign first 74x87 frame';Technique='Independent composition; nearest-neighbour; original aspect ratios; no image text'})
        } finally { $g.Dispose(); $canvas.Dispose() }
        $index++
    }
    $banner = $map.Clone([System.Drawing.Rectangle]::new(420,140,720,168),[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try { $banner.Save((Join-Path $OutputDirectory 'banner.png'),[System.Drawing.Imaging.ImageFormat]::Png) } finally { $banner.Dispose() }
    $sources | Export-Csv -LiteralPath (Join-Path $OutputDirectory '../ArtSources.csv') -NoTypeInformation -Encoding utf8
} finally { $map.Dispose() }
