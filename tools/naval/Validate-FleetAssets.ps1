param([string]$OceanDirectory,[Parameter(Mandatory)][string[]]$Stems)
$ErrorActionPreference='Stop'
if(!$OceanDirectory){$OceanDirectory=Join-Path (Split-Path (Split-Path $PSScriptRoot)) 'resources/ocean'}
Add-Type -AssemblyName System.Drawing
$refs=@([Drawing.Bitmap].Assembly.Location,[Drawing.Color].Assembly.Location,(Join-Path $PSHOME 'System.Private.Windows.GdiPlus.dll'),(Join-Path $PSHOME 'System.Private.Windows.Core.dll'))
Add-Type -ReferencedAssemblies $refs -Path @((Join-Path $PSScriptRoot 'CannonLayerPixels.cs'),(Join-Path $PSScriptRoot 'ShipFamilyPixels.cs'))
$checked=0
foreach($stem in $Stems){
    foreach($state in @('Normal','Idle','Damaged','Sinking')){
        $name=if($stem -eq 'Ship_CorpsRoyaux_ThreeMast' -and $state -eq 'Normal'){'Ship_CorpsRoyaux_ThreeMast_Layered_v4_compat.png'}else{$stem+'_'+$state+'_v5_compat.png'}
        $path=Join-Path $OceanDirectory $name
        $meta=Get-Content -LiteralPath ($path+'.naval.json') -Raw | ConvertFrom-Json
        if((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ine $meta.SourceSha256){throw "Source hash differs: $name"}
        if($meta.Directions.Count -ne 8){throw "Expected eight directions: $name"}
        foreach($direction in $meta.Directions){if($direction.Count -ne 4){throw "Expected four phases: $name"}}
        $sinking=$state -eq 'Sinking'
        if($sinking -and ($meta.Loop -or $meta.ForegroundGraphics -or $meta.CannonGraphics)){throw "Sinking must not loop or retain live crew/gun layers: $name"}
        $front=if($sinking){''}else{Join-Path $OceanDirectory $meta.ForegroundGraphics}
        $cover=if($sinking){''}else{Join-Path $OceanDirectory $meta.CannonOcclusionGraphics}
        $spectral=$stem -like 'Ship_Ghost_*'
        $null=[ShipFamilyPixels]::Validate($path,$front,$cover,$sinking,$spectral)
        if(!$sinking -and !(Test-Path -LiteralPath (Join-Path $OceanDirectory $meta.CannonGraphics))){throw "Missing independent cannon atlas: $name"}
        $checked++
    }
}
Write-Output "PASS $checked ship states: eight directions, four phases, matching hashes/layers, transparent final sinking phase and disjoint translucent ghost passes."
