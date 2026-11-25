param([Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$resources=Join-Path (Split-Path (Split-Path $PSScriptRoot)) 'resources'
$factions=@('Adventurer','CorpsRoyaux','Herbonia','Majestya','Skeleton','Ghost','Pirate')
$hulls=@('ThreeMast','Sloop','Raft','Fishing','ShipOfLine')
$assets=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
function Ocean([string]$Name){
    if(!$Name){return}
    if([IO.Path]::GetFileName($Name) -ne $Name -or $Name -in @('.','..')){throw "Invalid ocean asset: $Name"}
    [void]$assets.Add('ocean/'+$Name)
}
foreach($faction in $factions){foreach($hull in $hulls){
    $stem='Ship_'+$faction+'_'+$hull
    Ocean ($stem+'_Preview_v5.png')
    foreach($state in @('Normal','Idle','Damaged','Sinking')){
        $name=if($stem -eq 'Ship_CorpsRoyaux_ThreeMast' -and $state -eq 'Normal'){$stem+'_Layered_v4_compat.png'}else{$stem+'_'+$state+'_v5_compat.png'}
        Ocean $name;Ocean ($name+'.naval.json')
        $atlas=Get-Content -LiteralPath (Join-Path $resources ('ocean/'+$name+'.naval.json')) -Raw | ConvertFrom-Json
        Ocean $atlas.ForegroundGraphics;Ocean $atlas.CannonGraphics;Ocean $atlas.CannonOcclusionGraphics
    }
}}
if(Test-Path -LiteralPath (Join-Path $resources 'ocean/Ship_Deck_Hole_v5.png')){Ocean 'Ship_Deck_Hole_v5.png'}
foreach($boss in @('Kraken','SeaSerpent','Leviathan')){
    Ocean ('Naval_'+$boss+'_v1.png');Ocean ('Naval_'+$boss+'_v1.png.monster.json')
}
foreach($limb in @('KrakenTentacle','SeaSerpentNeck','LeviathanClaw')){
    foreach($suffix in @('','_idle','_attack','_4f','_4f_idle','_4f_attack')){[void]$assets.Add('entities/NPC_Naval_'+$limb+'_v1'+$suffix+'.png')}
}
$entries=foreach($relative in ($assets | Sort-Object)){
    $file=Get-Item -LiteralPath (Join-Path $resources $relative)
    [pscustomobject]@{Path=('resources/'+$relative);Bytes=$file.Length;Sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$manifest=Join-Path $OutputDirectory 'Manifeste_flotte.json';$archive=Join-Path $OutputDirectory 'Flotte_et_monstres_CR2026.zip'
if(Test-Path -LiteralPath $archive){throw "Output archive already exists: $archive"}
@{Models=35;ShipStates=140;MonsterAtlases=3;LimbSheets=18;Files=@($entries)} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifest -Encoding utf8
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=[IO.Compression.ZipFile]::Open($archive,[IO.Compression.ZipArchiveMode]::Create)
try {
    foreach($entry in $entries){
        $file=Join-Path $resources $entry.Path.Substring(10)
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$file,$entry.Path,[IO.Compression.CompressionLevel]::Fastest)
    }
    [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$manifest,'Manifeste_flotte.json')
}
finally{$zip.Dispose()}
Write-Output ('Packaged '+$entries.Count+' referenced resources: '+$archive)
