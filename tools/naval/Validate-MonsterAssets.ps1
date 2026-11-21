param([string]$ResourceDirectory)
$ErrorActionPreference='Stop'
if(!$ResourceDirectory){$ResourceDirectory=Join-Path (Split-Path (Split-Path $PSScriptRoot)) 'resources'}
Add-Type -AssemblyName System.Drawing
function Check-Sheet([string]$Path,[int]$Columns,[int]$Rows){
    $bitmap=[Drawing.Bitmap]::new($Path)
    try {
        if($bitmap.Width%$Columns -or $bitmap.Height%$Rows -or $bitmap.Width/$Columns -ne $bitmap.Height/$Rows){throw "Wrong frame grid: $Path"}
        $cell=[int]($bitmap.Width/$Columns)
        if($cell -lt 32 -or $cell -gt 512){throw "Unsupported frame size: $Path"}
        for($r=0;$r -lt $Rows;$r++){
            for($c=0;$c -lt $Columns;$c++){
                $visible=$clear=0
                for($y=0;$y -lt $cell;$y+=4){for($x=0;$x -lt $cell;$x+=4){
                    $alpha=$bitmap.GetPixel($c*$cell+$x,$r*$cell+$y).A
                    if($alpha -gt 32){$visible++}
                    if($alpha -eq 0){$clear++}
                }}
                if($visible -lt 4 -or $clear -lt 4){throw "Empty or opaque frame $c,$r in $Path"}
            }
        }
    }
    finally {$bitmap.Dispose()}
}
foreach($pair in @(@('Kraken','KrakenTentacle'),@('SeaSerpent','SeaSerpentNeck'),@('Leviathan','LeviathanClaw'))){
    Check-Sheet (Join-Path $ResourceDirectory ('ocean/Naval_'+$pair[0]+'_v1.png')) 4 6
    $stem='entities/NPC_Naval_'+$pair[1]+'_v1'
    foreach($suffix in @('','_attack')){Check-Sheet (Join-Path $ResourceDirectory ($stem+$suffix+'.png')) 6 4}
    foreach($suffix in @('_idle','_4f','_4f_idle','_4f_attack')){Check-Sheet (Join-Path $ResourceDirectory ($stem+$suffix+'.png')) 4 4}
}
Write-Output 'PASS 3 monster atlases and 18 limb sheets: frame grids, visible poses and real transparency.'
