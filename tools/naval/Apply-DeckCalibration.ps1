param([string]$AtlasPath)
$ErrorActionPreference='Stop'
if(!$AtlasPath){$AtlasPath=Join-Path (Split-Path (Split-Path $PSScriptRoot)) 'resources/ocean/Ship_CorpsRoyaux_ThreeMast_Layered_v4_compat.png.naval.json'}
$atlas=Get-Content -LiteralPath $AtlasPath -Raw | ConvertFrom-Json
$calibration=Get-Content (Join-Path $PSScriptRoot 'deck-calibration.json') -Raw | ConvertFrom-Json
$bob=@(0,-1,0,1)
for($d=0;$d -lt 8;$d++){for($f=0;$f -lt 4;$f++){
    $bow=$calibration.Bow[$d]
    $atlas.Directions[$d][$f] | Add-Member DeckBow @{X=12+$bow[0]*.234375;Y=8+$bow[1]*.234375+$bob[$f]} -Force
    foreach($name in @('Masts','Port','Starboard')){
        $points=@($calibration.$name[$d] | ForEach-Object {@{X=12+$_[0]*.234375;Y=8+$_[1]*.234375+$bob[$f]}})
        $atlas.Directions[$d][$f] | Add-Member ('Deck'+$name) $points -Force
    }
}}
$json=$atlas | ConvertTo-Json -Depth 30 -Compress
if([Text.Encoding]::UTF8.GetByteCount($json) -gt 65536){throw 'Deck metadata exceeds the loader limit'}
[IO.File]::WriteAllText([IO.Path]::GetFullPath($AtlasPath),$json,[Text.UTF8Encoding]::new($false))
Write-Output ('Applied eight calibrated walking surfaces: '+[Text.Encoding]::UTF8.GetByteCount($json)+' bytes.')
