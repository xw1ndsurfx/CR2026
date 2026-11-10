param([Parameter(Mandatory)][string]$ArtPath,[Parameter(Mandatory)][string]$MaskPath,[Parameter(Mandatory)][string]$OutputDirectory,[ValidateSet('Normal','Idle','Damaged')][string]$State='Idle',[string]$Stem='Ship_CorpsRoyaux_ThreeMast',[ValidateRange(0,5)][int]$Palette=0,[string]$TemplatePath,[string]$ReferenceGraphicPath,[switch]$NoFixedGunRemoval,[switch]$NoCannons)
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path $PSScriptRoot)
$ocean=Join-Path $repo 'resources/ocean'
Add-Type -AssemblyName System.Drawing
$refs=@([Drawing.Bitmap].Assembly.Location,[Drawing.Color].Assembly.Location,(Join-Path $PSHOME 'System.Private.Windows.GdiPlus.dll'),(Join-Path $PSHOME 'System.Private.Windows.Core.dll'))
Add-Type -ReferencedAssemblies $refs -Path @((Join-Path $PSScriptRoot 'CannonLayerPixels.cs'),(Join-Path $PSScriptRoot 'ShipFamilyPixels.cs'))
$sailing='Ship_CorpsRoyaux_ThreeMast_Layered_v4_compat.png'
$name=$Stem+'_'+$State+'_v5_compat.png'
$fringe=$Stem+'_'+$State+'_v5_fringe_compat.png'
$cover=$Stem+'_'+$State+'_v5_canopy_compat.png'
if(!$TemplatePath){$TemplatePath=Join-Path $ocean ($sailing+'.naval.json')}
if(!$ReferenceGraphicPath){$ReferenceGraphicPath=Join-Path $ocean $sailing}
$atlas=Get-Content -LiteralPath $TemplatePath -Raw | ConvertFrom-Json
function SourceAsset([string]$asset){
    foreach($folder in @($OutputDirectory,(Split-Path $TemplatePath),$ocean)){
        $candidate=Join-Path $folder $asset;if(Test-Path -LiteralPath $candidate){return $candidate}
    }
    throw ('Missing source asset: '+$asset)
}
if($NoCannons){foreach($row in $atlas.Directions){foreach($frame in $row){$frame.CannonAnchors=@()}}}
$pivots=[double[]]::new(96)
for($i=0;$i -lt $pivots.Length;$i++){$pivots[$i]=-1000000}
for($d=0;$d -lt 8;$d++){foreach($a in $atlas.Directions[$d][0].CannonAnchors){$slot=[int]$a.Index+$(if($a.Starboard){3}else{0});$pivots[($d*6+$slot)*2]=$a.X;$pivots[($d*6+$slot)*2+1]=$a.Y}}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$registration=[ShipFamilyPixels]::Assemble($ArtPath,$MaskPath,$ReferenceGraphicPath,(SourceAsset $atlas.ForegroundGraphics),$pivots,(Join-Path $OutputDirectory $name),(Join-Path $OutputDirectory $fringe),$Palette,!$NoFixedGunRemoval.IsPresent)
$gunName=if($atlas.CannonGraphics){$atlas.CannonGraphics}else{(Get-Content -LiteralPath (Join-Path $ocean ($sailing+'.naval.json')) -Raw | ConvertFrom-Json).CannonGraphics}
$gunAsset=SourceAsset $gunName
$front=[Drawing.Bitmap]::new((Join-Path $OutputDirectory $fringe));$guns=[Drawing.Bitmap]::new($gunAsset)
try{
    if($Palette -in @(3,4)){
        $referenceName=if($State -eq 'Normal'){$atlas.CannonOcclusionGraphics}else{'Ship_CorpsRoyaux_ThreeMast_'+$State+'_v5_canopy_compat.png'}
        $referenceCover=[Drawing.Bitmap]::new((Join-Path $ocean $referenceName))
        try{$pixels=[ShipFamilyPixels]::RegisteredCannonCover([CannonLayerPixels]::Read($front),[CannonLayerPixels]::Read($referenceCover),[CannonLayerPixels]::Read($guns),$pivots)}finally{$referenceCover.Dispose()}
    }else{$pixels=[CannonLayerPixels]::CannonCover([CannonLayerPixels]::Read($front),[CannonLayerPixels]::Read($guns),$pivots,($Palette -eq 5))}
    [CannonLayerPixels]::Save($pixels,3072,1024,(Join-Path $OutputDirectory $cover))
}finally{$front.Dispose();$guns.Dispose()}
if($Palette -eq 4){
    $spectralGuns=$Stem+'_Cannons_v5_compat.png'
    [ShipFamilyPixels]::SpectralLayers((Join-Path $OutputDirectory $name),(Join-Path $OutputDirectory $fringe),(Join-Path $OutputDirectory $cover),$gunAsset,(Join-Path $OutputDirectory $spectralGuns))
    $atlas.CannonGraphics=$spectralGuns
}
if($NoCannons){$atlas.CannonGraphics=''}
$atlas.ForegroundGraphics=$fringe;$atlas.CannonOcclusionGraphics=$cover
$atlas.SourceSha256=(Get-FileHash -LiteralPath (Join-Path $OutputDirectory $name) -Algorithm SHA256).Hash.ToLowerInvariant()
$atlas.ReviewNotes=$Stem+' '+$State+' state. Generated art registered to the supplied hull reference and calibrated station pivots. Foreground selects authored image pixels. Walking and hull registration require live visual verification.'
$json=$atlas | ConvertTo-Json -Depth 30 -Compress
if([Text.Encoding]::UTF8.GetByteCount($json) -gt 65536){throw 'Idle metadata exceeds 64KiB'}
[IO.File]::WriteAllText((Join-Path $OutputDirectory ($name+'.naval.json')),$json,[Text.UTF8Encoding]::new($false))
$registration | ConvertTo-Json | Set-Content (Join-Path $OutputDirectory 'registration.json') -Encoding utf8
Write-Output ('Created '+$Stem+' '+$State+' layers and '+[Text.Encoding]::UTF8.GetByteCount($json)+' bytes of metadata.')
