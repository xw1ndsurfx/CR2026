param([ValidateSet('Debug','Release')][string]$Configuration = 'Debug')
$ErrorActionPreference = 'Stop'
$fishingRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $fishingRepo
try {
    & dotnet build Intersect.Network/Intersect.Network.csproj -c $Configuration -m:1 -p:RunAnalyzers=false -v:q
    if ($LASTEXITCODE -ne 0) { throw 'Network build failed.' }
    $fishingPublic = Join-Path $fishingRepo "Intersect.Network/bin/$Configuration/keys/network.handshake.bkey.pub"
    $fishingPrivate = Join-Path $fishingRepo "Intersect.Network/bin/$Configuration/keys/network.handshake.bkey"
    foreach ($fishingProject in @('Intersect.Server/Intersect.Server.csproj','Intersect.Client/Intersect.Client.csproj','Intersect.Editor/Intersect.Editor.csproj')) {
        & dotnet build $fishingProject -c $Configuration -m:1 -p:RunAnalyzers=false -v:q "-p:NetworkHandshakePublicKey=$fishingPublic" "-p:NetworkHandshakePrivateKey=$fishingPrivate"
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $fishingProject" }
    }
    & dotnet run --project Utilities/FishingRulesTests/FishingRulesTests.csproj -c $Configuration -p:TestServer=true -p:RunAnalyzers=false "-p:NetworkHandshakePublicKey=$fishingPublic" "-p:NetworkHandshakePrivateKey=$fishingPrivate"
    if ($LASTEXITCODE -ne 0) { throw 'Fishing domain and native reward checks failed.' }
    & dotnet test Intersect.Tests.Client/Intersect.Tests.Client.csproj -c $Configuration -p:RunAnalyzers=false "-p:NetworkHandshakePublicKey=$fishingPublic" --filter "FullyQualifiedName~FishingEventInteractionTests|FullyQualifiedName~FishingWindowTests|FullyQualifiedName~FishingAudioTests|FullyQualifiedName~FishingSceneGeometryTests|FullyQualifiedName~FontGlyphFallbackTests|FullyQualifiedName~UiTextureMirrorTests|FullyQualifiedName~ExportationContrastTests|FullyQualifiedName~WeaponSkillHudTests"
    if ($LASTEXITCODE -ne 0) { throw 'Fishing event interaction and native window checks failed.' }
    & dotnet run --project Utilities/InterfaceEditorTests/Intersect.InterfaceEditorTests.csproj -c $Configuration -p:RuntimeIdentifiers=win-x64 -p:RunAnalyzers=false "-p:NetworkHandshakePublicKey=$fishingPublic" -- --fishing-only
    if ($LASTEXITCODE -ne 0) { throw 'Fishing editor checks failed.' }
    & dotnet run --no-build --project Utilities/InterfaceEditorTests/Intersect.InterfaceEditorTests.csproj -c $Configuration -p:RuntimeIdentifiers=win-x64 -p:RunAnalyzers=false "-p:NetworkHandshakePublicKey=$fishingPublic" -- --hub-only
    if ($LASTEXITCODE -ne 0) { throw 'Fishing Hub entry and editor navigation checks failed.' }
    & python scripts/fishing/verify_assets.py
    if ($LASTEXITCODE -ne 0) { throw 'Fishing asset integrity checks failed.' }
}
finally { Pop-Location }
