param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug')
$ErrorActionPreference = 'Stop'
$cinematicRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $cinematicRepo
try {
    function Invoke-CinematicCheck([string[]]$Arguments) {
        & dotnet @Arguments
        if ($LASTEXITCODE -ne 0) { throw "Cinematic verification failed: dotnet $($Arguments -join ' ')" }
    }
    Invoke-CinematicCheck -Arguments @('build', 'Intersect.Network/Intersect.Network.csproj', '-c', $Configuration, '-m:1')
    # The existing repo defaults consumers to Release keys even in Debug. Use the generated key explicitly.
    $cinematicPublicKey = Join-Path $cinematicRepo "Intersect.Network/bin/$Configuration/keys/network.handshake.bkey.pub"
    $cinematicPrivateKey = Join-Path $cinematicRepo "Intersect.Network/bin/$Configuration/keys/network.handshake.bkey"
    if (!(Test-Path -LiteralPath $cinematicPublicKey)) { throw 'Build the network project and check its key-generation log.' }
    foreach ($cinematicProject in @('Intersect.Editor/Intersect.Editor.csproj', 'Intersect.Client/Intersect.Client.csproj', 'Intersect.Server/Intersect.Server.csproj')) {
        Invoke-CinematicCheck -Arguments @('build', $cinematicProject, '-c', $Configuration, '-m:1',
            "-p:NetworkHandshakePublicKey=$cinematicPublicKey", "-p:NetworkHandshakePrivateKey=$cinematicPrivateKey")
    }
    Invoke-CinematicCheck -Arguments @('test', 'Intersect.Tests/Intersect.Tests.csproj', '-c', $Configuration,
        '--filter', 'FullyQualifiedName~CinematicCompilerTests', '--logger', 'trx', '-m:1')
    Invoke-CinematicCheck -Arguments @('run', '--project', 'Utilities/CinematicEditorSmoke/CinematicEditorSmoke.csproj', '-c', $Configuration,
        "-p:NetworkHandshakePublicKey=$cinematicPublicKey")
}
finally { Pop-Location }
