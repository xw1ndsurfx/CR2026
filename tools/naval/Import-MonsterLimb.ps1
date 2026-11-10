param([Parameter(Mandatory)][string]$Source,[Parameter(Mandatory)][string]$OutputDirectory,[Parameter(Mandatory)][string]$Stem)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$master=[Drawing.Bitmap]::new($Source)
try {
    if($master.Width%6 -ne 0 -or $master.Height%4 -ne 0){throw 'The limb master requires six frames in each of four direction rows.'}
    $sourceWidth=[int]($master.Width/6);$sourceHeight=[int]($master.Height/4);$cell=64
    function Export-Sprite([string]$Name,[int[]]$Frames){
        $bitmap=[Drawing.Bitmap]::new($cell*$Frames.Count,$cell*4,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $canvas=[Drawing.Graphics]::FromImage($bitmap)
        try {
            $canvas.CompositingMode=[Drawing.Drawing2D.CompositingMode]::SourceCopy
            $canvas.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
            $canvas.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
            for($row=0;$row -lt 4;$row++){
                for($frame=0;$frame -lt $Frames.Count;$frame++){
                    $destination=[Drawing.Rectangle]::new($frame*$cell,$row*$cell,$cell,$cell)
                    $origin=[Drawing.Rectangle]::new($Frames[$frame]*$sourceWidth,$row*$sourceHeight,$sourceWidth,$sourceHeight)
                    $canvas.DrawImage($master,$destination,$origin,[Drawing.GraphicsUnit]::Pixel)
                }
            }
            $bitmap.Save((Join-Path $OutputDirectory ($Name+'.png')),[Drawing.Imaging.ImageFormat]::Png)
        }
        finally {$canvas.Dispose();$bitmap.Dispose()}
    }
    # Corps Royaux uses six normal and attack frames and four idle frames.
    Export-Sprite $Stem @(0,1,2,3,4,5)
    Export-Sprite ($Stem+'_idle') @(0,1,0,5)
    Export-Sprite ($Stem+'_attack') @(0,1,2,3,4,5)
    # Keep a separate set for clients running the stock four-frame settings.
    Export-Sprite ($Stem+'_4f') @(0,1,4,5)
    Export-Sprite ($Stem+'_4f_idle') @(0,1,0,5)
    Export-Sprite ($Stem+'_4f_attack') @(1,2,3,4)
}
finally {$master.Dispose()}
