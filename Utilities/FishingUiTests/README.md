# Fishing UI rendering checks

Run from the repository root on Windows:

```sh
dotnet run --project Utilities/FishingUiTests/FishingUiTests.csproj -c Release
```

The harness constructs the production fishing controls, loads the delivered fishing PNGs and applies explicit server-state fixtures. It renders Ready, Fighting, LegendaryPrompt and Loot in English, French and Spanish at 800×600, 1280×720 and 1920×1080, checking visible button bounds. The 36 PNGs are written to `validation-local/fishing-ui/`.

Rendering uses the existing GDI+ test renderer and substitute font metrics. It does not connect to a server, grant items, exercise GPU drivers or play audio. Packet interaction, input lifecycle and sound failures are covered separately by `Intersect.Tests.Client/Fishing*Tests.cs`; the native server suite covers event activation and real loopback transport.

Real persistence checks are in `Intersect.Tests.Server/FishingPersistenceTests.cs`. They use `User.Save` and SQLite, including an injected database write failure, rather than a fake successful checkpoint:

```sh
dotnet test Intersect.Tests.Server/Intersect.Tests.Server.csproj --filter FullyQualifiedName~FishingPersistenceTests
```

Optionally set `FISHING_TEST_DB` to a private, isolated `playerdata.db`. Each test uses SQLite's backup API to create a disposable database, adds its own fixture account and never writes to the supplied source. Omit the variable to use an empty database. Never point testing tools at production.
