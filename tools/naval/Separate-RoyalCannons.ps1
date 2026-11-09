param([Parameter(Mandatory)][string]$MaskDirectory,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$refs=@([System.Drawing.Bitmap].Assembly.Location,[System.Drawing.Color].Assembly.Location,(Join-Path $PSHOME 'System.Private.Windows.GdiPlus.dll'),(Join-Path $PSHOME 'System.Private.Windows.Core.dll'))
Add-Type -ReferencedAssemblies $refs -Path (Join-Path $PSScriptRoot 'CannonLayerPixels.cs')
$repo=Split-Path (Split-Path $PSScriptRoot)
$ocean=Join-Path $repo 'resources/ocean'
$old='Ship_CorpsRoyaux_ThreeMast_Layered_v3_compat.png'
$new='Ship_CorpsRoyaux_ThreeMast_Layered_v4_compat.png'
$foreground='Ship_CorpsRoyaux_ThreeMast_Layered_v4_fringe_compat.png'
$cannons='Ship_CorpsRoyaux_ThreeMast_Cannons_v4.png'
$plan=Get-Content (Join-Path $PSScriptRoot 'cannon-cuts.json') -Raw | ConvertFrom-Json
$atlas=Get-Content (Join-Path $ocean ($old+'.naval.json')) -Raw | ConvertFrom-Json
function Pixels([string]$path){$image=[System.Drawing.Bitmap]::new($path);try{,[CannonLayerPixels]::Read($image)}finally{$image.Dispose()}}
$ship=Pixels (Join-Path $ocean $old)
$fringe=Pixels (Join-Path $ocean $atlas.ForegroundGraphics)
$guns=[int[]]::new(512*384)
$positions=[double[]]::new(96)
$counts=@()
for($d=0;$d -lt 8;$d++){
    for($s=0;$s -lt 6;$s++){
        $cut=$plan.cuts[$d][$s]
        if($null -eq $cut){continue}
        $r=$cut[0];$p=$cut[1]
        $box=[int[]]@([Math]::Floor(12+$r[0]*.234375),[Math]::Floor(8+$r[1]*.234375),([Math]::Ceiling($r[2]*.234375)+1),([Math]::Ceiling($r[3]*.234375)+1))
        $px=[int][Math]::Round(12+$p[0]*.234375);$py=[int][Math]::Round(8+$p[1]*.234375)
        $mask=if($d -eq 7){''}else{Join-Path $MaskDirectory ($plan.directions[$d]+'.png')}
        $count=[CannonLayerPixels]::Cut($ship,$fringe,$guns,$d,$s,$box,$px,$py,$mask)
        $positions[($d*6+$s)*2]=$px;$positions[($d*6+$s)*2+1]=$py
        $counts+=@{Direction=$plan.directions[$d];Station=$s;Pixels=$count}
    }
}
# Far diagonal metal is hidden in the original painting. Its fixed wooden carriage
# stays on the hull; reuse the existing far iron barrel, behind the sail fringe.
foreach($d in @(1,3,5,7)){
    for($i=0;$i -lt 3;$i++){
        $s=if($d -in 1,3){$i}else{$i+3}
        $angle=if($d -in 1,5){-45}else{45}
        [CannonLayerPixels]::Proxy($guns,12,$d*6+$s,$angle)
        $p=$plan.proxyPivots."$d"[$i]
        $positions[($d*6+$s)*2]=[Math]::Round(12+$p[0]*.234375)
        $positions[($d*6+$s)*2+1]=[Math]::Round(8+$p[1]*.234375)
    }
}
$bob=@(0,-1,0,1)
for($d=0;$d -lt 8;$d++){
    for($f=0;$f -lt 4;$f++){
        $frame=$atlas.Directions[$d][$f];$anchors=@()
        for($s=0;$s -lt 6;$s++){
            $star=$s -ge 3;$index=$s%3;$n=$d*6+$s
            $behind=($d -eq 2 -and !$star) -or ($d -eq 6 -and $star) -or ($d -in 1,3 -and !$star) -or ($d -in 5,7 -and $star)
            $px=$positions[$n*2];$py=$positions[$n*2+1]+$bob[$f]
            $anchors+=@{Starboard=$star;Index=$index;Sprite=$n;X=$px;Y=$py;BehindCrew=$behind}
            $role=if($star){2}else{1}
            $crew=$frame.CrewAnchors | Where-Object {$_.Role -eq $role -and $_.Index -eq $index}
            $beam=($d*45+$(if($star){90}else{-90}))*[Math]::PI/180
            $crew.X=$px-[Math]::Sin($beam)*12
            $crew.Y=$py+[Math]::Cos($beam)*7+10
        }
        $frame | Add-Member CannonAnchors $anchors -Force
    }
}
$atlas.ForegroundGraphics=$foreground
$atlas | Add-Member CannonGraphics $cannons -Force
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
[CannonLayerPixels]::Save($ship,3072,1024,(Join-Path $OutputDirectory $new))
[CannonLayerPixels]::Save($fringe,3072,1024,(Join-Path $OutputDirectory $foreground))
[CannonLayerPixels]::Save($guns,512,384,(Join-Path $OutputDirectory $cannons))
$atlas.SourceSha256=(Get-FileHash -LiteralPath (Join-Path $OutputDirectory $new) -Algorithm SHA256).Hash.ToLowerInvariant()
$atlas.ReviewNotes='Independent iron cannons, fixed wooden carriages, real crew, sail fringe. 2D pivot animation; far diagonal barrels reuse the profile barrel behind occlusion. Live multiplayer validation pending.'
$json=$atlas | ConvertTo-Json -Depth 30 -Compress
if([Text.Encoding]::UTF8.GetByteCount($json) -gt 65536){throw 'Metadata exceeds the client limit'}
[IO.File]::WriteAllText((Join-Path $OutputDirectory ($new+'.naval.json')),$json,[Text.UTF8Encoding]::new($false))
$counts | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $OutputDirectory 'extraction-counts.json') -Encoding utf8
[CannonLayerPixels]::Preview($ship,$fringe,$guns,$positions,(Join-Path $OutputDirectory 'Apercu_canons_gauche_centre_droite.png'))
foreach($name in @($new,$foreground,$cannons,($new+'.naval.json'))){Copy-Item -LiteralPath (Join-Path $OutputDirectory $name) -Destination $ocean -Force}
Write-Output ('Created 48 cannon sprites and '+[Text.Encoding]::UTF8.GetByteCount($json)+' bytes of metadata.')
