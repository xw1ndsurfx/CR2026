# Fishing rule and reward checks

The default program tests the production shared rules with no external packages. It covers zone/tool/condition intersections, UTC windows, depth, independent legendary rarity, pressure/release simulation, the four skills, legendary interruption and timed challenge, audio overrides, configuration validation and English/French/Spanish coverage. Legendary tests distinguish an ignored automatic invitation (auto resumes after its 1,500 ms failure delay), an ignored manual invitation, acceptance followed by challenge success/failure, and explicit cancellation of pending automatic resumption.

```sh
dotnet run --project Utilities/FishingRulesTests/FishingRulesTests.csproj
```

The server variant uses the real `Player`, `Item`, inventory slots and `FishingRewards.Commit`. The injected save callback tests atomic staging and failure recovery without connecting to a player database. It additionally verifies bounded native stacks, partial inventory capacity, rollback after save failures/exceptions, receipt replay and restoration, equipped bait protection, profession XP and level rewards, localized messages and MessagePack serialization.

Its connection regression also opens a real loopback UDP connection through `ClientNetwork` and `LiteNetLibInterface`, using an ephemeral RSA handshake key and OS-assigned ports. The test observes the actual connection state before connection/disconnection consumers run, supplies that connection to the native Event fixture, sends the fishing opening packet over the encrypted transport, and checks the next fishing tick. It covers pending connections, shutdown, native endpoint migration and a fresh connection after disconnect. It never calls `HandleConnected()` or assigns a connection-state field to make the test pass. Player/map/inventory fixtures still isolate this test from a production account database; this is not a graphical client smoke test. Socket waits are bounded and both native managers are stopped even if an assertion fails.

```sh
dotnet run --project Utilities/FishingRulesTests/FishingRulesTests.csproj -p:TestServer=true
```

If MSBuild cannot inspect processes in a restricted container, the domain suite can be compiled using the installed SDK's standard Roslyn compiler without changing the runtime:

```sh
python3 Utilities/FishingRulesTests/run-standalone.py --dotnet /path/to/dotnet
```

The standalone command executes only the shared-rule suite. It does not substitute for compiling the complete client, editor and server, or for running the native reward variant and checking the game in a live session.

When the server has already been built, the native suite can also reuse its assemblies without rebuilding shared dependencies:

```sh
python3 Utilities/FishingRulesTests/run-native.py --dotnet /path/to/dotnet
```

This reads the existing server dependency graph from `obj/project.assets.json`, compiles only the test program against the built DLLs, and runs `bin/native/Intersect.Tests.Server.dll`. It deliberately does not claim that those DLLs are newer than every source file; the complete build remains a separate verification step.
