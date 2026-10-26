param([ValidateSet('Debug','Release')][string]$Configuration = 'Debug')
$ErrorActionPreference = 'Stop'
$joustRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $joustRepo
try {
    & dotnet build Intersect.Network/Intersect.Network.csproj -c $Configuration -m:1 -p:RunAnalyzers=false -v:q
    if ($LASTEXITCODE -ne 0) { throw 'Network build failed.' }
    $joustPublic = Join-Path $joustRepo "Intersect.Network/bin/$Configuration/keys/network.handshake.bkey.pub"
    $joustPrivate = Join-Path $joustRepo "Intersect.Network/bin/$Configuration/keys/network.handshake.bkey"
    foreach ($joustProject in @('Intersect.Server/Intersect.Server.csproj','Intersect.Client/Intersect.Client.csproj','Intersect.Editor/Intersect.Editor.csproj')) {
        & dotnet build $joustProject -c $Configuration -m:1 -p:RunAnalyzers=false -v:q "-p:NetworkHandshakePublicKey=$joustPublic" "-p:NetworkHandshakePrivateKey=$joustPrivate"
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $joustProject" }
    }
    & dotnet run --project Utilities/JoustingTests/JoustingTests.csproj -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw 'Jousting simulation verification failed.' }
    & dotnet test Intersect.Tests.Server/Intersect.Tests.Server.csproj -c $Configuration --filter 'FullyQualifiedName~JoustWorldTests|FullyQualifiedName~JoustExchangeTests|FullyQualifiedName~MountTests|FullyQualifiedName~CinematicPlayerRouteTests' -m:1 -p:RunAnalyzers=false -v:q "-p:NetworkHandshakePublicKey=$joustPublic" "-p:NetworkHandshakePrivateKey=$joustPrivate"
    if ($LASTEXITCODE -ne 0) { throw 'Mount regression verification failed.' }
    & dotnet test Intersect.Tests.Client/Intersect.Tests.Client.csproj -c $Configuration --filter FullyQualifiedName~JoustClientTests -m:1 -p:RunAnalyzers=false -v:q "-p:NetworkHandshakePublicKey=$joustPublic"
    if ($LASTEXITCODE -ne 0) { throw 'Client jousting verification failed.' }
    & dotnet test Intersect.Tests/Intersect.Tests.csproj -c $Configuration --filter 'FullyQualifiedName~JoustCinematicTests|FullyQualifiedName~JoustEconomyTests|FullyQualifiedName~CinematicCompilerTests' -m:1 -p:RunAnalyzers=false -v:q
    if ($LASTEXITCODE -ne 0) { throw 'Native cinematic verification failed.' }
}
finally { Pop-Location }
