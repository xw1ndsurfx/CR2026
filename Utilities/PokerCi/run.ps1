param(
    [Parameter(Mandatory = $true)][string]$Project,
    [ValidateSet('build', 'run')][string]$Mode = 'run',
    [string]$Configuration = 'Debug',
    [string]$SummaryKey = 'summary'
)
# Preserve the native exit code; make a short diagnostic available without hiding full logs.
$ErrorActionPreference = 'Continue'
$PSNativeCommandUseErrorActionPreference = $false
if ($Mode -eq 'build') {
    $lines = @(& dotnet build $Project --configuration $Configuration --nologo 2>&1)
} else {
    $lines = @(& dotnet run --project $Project --configuration $Configuration 2>&1)
}
$code = $LASTEXITCODE
$lines | ForEach-Object { Write-Output $_ }
$summary = 'PASS'
if ($code -ne 0) {
    $candidate = $lines | Where-Object {
        ($_ -match 'error (CS|MSB|NETSDK)[0-9]+:') -or
        ($_ -match 'Exception:' -and $_ -notmatch 'TargetInvocationException|TypeInitializationException')
    } | Select-Object -First 1
    if (-not $candidate) { $candidate = $lines | Where-Object { $_ -match 'FAIL|error' } | Select-Object -First 1 }
    $summary = if ($candidate) { "$candidate".Trim() } else { "exit $code; see log" }
    $summary = $summary -replace '^.*(error (?:CS|MSB|NETSDK)[0-9]+:)', '$1'
    $summary = $summary -replace '[\r\n]', ' '
    if ($summary.Length -gt 120) { $summary = $summary.Substring(0, 120) }
}
if ($env:GITHUB_OUTPUT) { "$SummaryKey=$summary" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding utf8 -Append }
exit $code
