param(
    [Parameter(Mandatory)][string]$ClientBuildDirectory,
    [Parameter(Mandatory)][string]$CompatibilityDirectory,
    [Parameter(Mandatory)][string]$WorkingDirectory,
    [Parameter(Mandatory)][string]$StageDirectory,
    [switch]$PrepareOnly
)
$ErrorActionPreference = 'Stop'
$cinematicBuild = (Resolve-Path -LiteralPath $ClientBuildDirectory).Path
$cinematicCompat = (Resolve-Path -LiteralPath $CompatibilityDirectory).Path
$cinematicWorking = (Resolve-Path -LiteralPath $WorkingDirectory).Path
$cinematicStage = [IO.Path]::GetFullPath($StageDirectory)
function Test-CinematicNestedPath([string]$Child, [string]$Parent) {
    return $Child.Equals($Parent, [StringComparison]::OrdinalIgnoreCase) -or
        $Child.StartsWith($Parent.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)
}
foreach ($cinematicSource in @($cinematicBuild, $cinematicCompat, $cinematicWorking)) {
    if ((Test-CinematicNestedPath $cinematicStage $cinematicSource) -or
        (Test-CinematicNestedPath $cinematicSource $cinematicStage)) {
        throw 'Use a separate staging directory outside the source/build/resource directories.'
    }
}
$cinematicFiles = @('opengl32.dll', 'libgallium_wgl.dll', 'vulkan_dzn.dll', 'vulkan_lvp.dll',
    'dzn_icd.x86_64.json', 'lvp_icd.x86_64.json')
foreach ($cinematicFile in $cinematicFiles) {
    if (!(Test-Path -LiteralPath (Join-Path $cinematicCompat $cinematicFile) -PathType Leaf)) {
        throw "Missing existing compatibility file: $cinematicFile"
    }
}
if (!(Test-Path -LiteralPath (Join-Path $cinematicBuild 'Corps Royaux.exe') -PathType Leaf)) {
    throw 'Build the client before preparing the isolated launch directory.'
}
$cinematicConfiguration = Get-Content -LiteralPath (Join-Path $cinematicWorking 'resources/config.json') -Raw | ConvertFrom-Json
if ($cinematicConfiguration.Host -notin @('127.0.0.1', 'localhost', '::1') -or
    [int]$cinematicConfiguration.Port -ne 15463 -or $cinematicConfiguration.UpdateUrl) {
    throw 'This QA launcher requires the local test configuration, port 15463, and no updater URL.'
}
$cinematicMarker = Join-Path $cinematicStage '.cinematic-client-sandbox'
if (Test-Path -LiteralPath $cinematicStage) {
    if (!(Test-Path -LiteralPath $cinematicMarker) -or
        (Get-Content -LiteralPath $cinematicMarker -Raw).Trim() -ne 'Royal Cinematic Director local client QA only') {
        throw 'Refusing to overwrite an unmarked existing directory.'
    }
} else {
    New-Item -ItemType Directory -Path $cinematicStage | Out-Null
    Set-Content -LiteralPath $cinematicMarker -Value 'Royal Cinematic Director local client QA only' -Encoding utf8
}
foreach ($cinematicItem in Get-ChildItem -LiteralPath $cinematicBuild -Force) {
    Copy-Item -LiteralPath $cinematicItem.FullName -Destination $cinematicStage -Recurse -Force
}
foreach ($cinematicFile in $cinematicFiles) {
    Copy-Item -LiteralPath (Join-Path $cinematicCompat $cinematicFile) -Destination (Join-Path $cinematicStage $cinematicFile) -Force
}
# Keep compatibility libraries beside the executable so the Windows OpenGL loader
# resolves them before the system library. Never replace DLLs in the user's release.
if (!$PrepareOnly) {
    Start-Process -FilePath (Join-Path $cinematicStage 'Corps Royaux.exe') -WorkingDirectory $cinematicWorking `
        -ArgumentList @('--working-directory', ('"' + $cinematicWorking + '"'), '--server', '127.0.0.1:15463') -WindowStyle Normal
}
Write-Output "Prepared isolated client: $cinematicStage"
