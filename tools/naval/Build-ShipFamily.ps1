param(
    [Parameter(Mandatory)][string]$Family,
    [Parameter(Mandatory)][string]$Stem,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidateRange(0,5)][int]$Palette=5,
    [string]$TemplatePath,
    [string]$ReferenceGraphicPath,
    [switch]$NoCannons,
    [switch]$Spectral
)
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path $PSScriptRoot)
foreach($state in @('Normal','Idle','Damaged')){
    $argsList=@('-NoProfile','-File',(Join-Path $PSScriptRoot 'Build-RoyalIdle.ps1'),
        '-ArtPath',(Join-Path $OutputDirectory ('sources/'+$Family+'-'+$state.ToLower()+'.png')),
        '-MaskPath',(Join-Path $OutputDirectory ('sources/'+$Family+'-'+$state.ToLower()+'-fringe.png')),
        '-OutputDirectory',(Join-Path $OutputDirectory $state.ToLower()),'-State',$state,'-Stem',$Stem,'-Palette',$Palette)
    if($TemplatePath){$argsList+=@('-TemplatePath',$TemplatePath)}
    if($ReferenceGraphicPath){$argsList+=@('-ReferenceGraphicPath',$ReferenceGraphicPath)}
    if($NoCannons){$argsList+='-NoCannons'}
    & pwsh @argsList
    if($LASTEXITCODE){throw ('Failed '+$state+' assembly')}
}
$argsList=@('-NoProfile','-File',(Join-Path $PSScriptRoot 'Build-RoyalSinking.ps1'),
    '-ArtPath',(Join-Path $OutputDirectory ('sources/'+$Family+'-sinking.png')),
    '-OutputDirectory',(Join-Path $OutputDirectory 'sinking'),'-Stem',$Stem)
if($TemplatePath){$argsList+=@('-TemplatePath',$TemplatePath)}
if($Spectral){$argsList+='-Spectral'}
& pwsh @argsList
if($LASTEXITCODE){throw 'Failed sinking assembly'}
Get-ChildItem -LiteralPath $OutputDirectory -Recurse -File |
    Where-Object {$_.Name -like ($Stem+'*')} | Copy-Item -Destination (Join-Path $repo 'resources/ocean')
foreach($script in @('Validate-FleetAssets.ps1','Export-FleetThumbnails.ps1')){
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot $script) -Stems $Stem
    if($LASTEXITCODE){throw ('Failed '+$script)}
}
& pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Preview-RoyalStates.ps1') -OutputDirectory (Join-Path $OutputDirectory 'review') -Stem $Stem
if($LASTEXITCODE){throw 'Failed preview assembly'}
