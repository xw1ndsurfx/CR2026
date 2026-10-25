# Validation / validation connectée

## Automated checks

See `scripts/Verify-Cinematics.ps1`. The tests exercise native route compilation, unsupported player-speed prevention, blocked/outside/empty paths, actor anchors, schedule overlap, dialogue barriers, choice branches, control handoff, immutable saved source, serialization compatibility, camera easing and packet registration.

The hidden `Utilities/CinematicEditorSmoke` executable checks construction/layout and real control mouse handlers using an explicitly synthetic map bitmap. It does not load native map assets, connect to a server or prove GPU rendering/performance.

## Optional native GPU check

Export a populated map from a consistent copy of `gamedata.db` (not `playerdata.db`):

```text
python scripts/Export-CinematicMapFixture.py <gamedata-snapshot> <server-config> <map-guid> <new-fixture-folder>
```

Build `Utilities/CinematicEditorSmoke` and run its executable with `--native-map <fixture-folder> <output-folder>`, using a separate working directory containing a copy of the matching `resources/` assets. Keep all fixture data outside the source checkout. The exporter uses only Python's standard library and opens the game DB read-only. It exports rendering options only, excluding database and SMTP settings.

This check initializes the real MonoGame device, loads native tilesets and map Event descriptors, compiles and round-trips a scene, and renders the Director through its actual map renderer. It rejects missing referenced tilesets and checks terrain coverage with fog/darkness disabled, then captures the native fog/overlay render. A deliberately clipped editor view verifies that offscreen rendering covers the full map and restores the editor's view. It does not authenticate a client, execute gameplay choices or replace the connected checklist below.

Local validation on 2026-10-24 also started a separate server with a copied game DB: 80 maps, 300 Events, 0 accounts and 0 characters loaded. The hosted server was not changed. The checks used matching Debug network keys and `RunAnalyzers=false`; existing compiler/package warnings remain. See `Progress.txt` for the latest results.

## Connected acceptance checklist — requires the actual game installation

Use editor, client and server built together: the new packet changes the engine protocol fingerprint.

- Create a scene on a real map; drag and reorder waypoints; save, close, reopen and compare the source/timeline.
- Call it from a personal Event and from a quest common Event. Confirm scene page conditions prevent invalid execution.
- Stand at the exact player and actor start tiles. Confirm native movement, animations, local audio, camera pan/zoom and final control restoration.
- Start from the wrong tile: confirm localized feedback and no subsequent reward/quest commands from the aborted call stack.
- Read a dialogue slowly; make each choice; confirm branch Events, existing translations, persisted variables and server quest validation.
- Allow gameplay control during a common Event and require a server-observed action before returning. Confirm the scene resumes only when that Event finishes.
- Block a route dynamically; wait for its bounded timeout; confirm controls/camera and original Event movement settings are restored. Test a missing/despawned actor too.
- Change map, die, disconnect and reconnect during movement and during dialogue. Confirm no stale camera, route, HUD/control changes or repeated rewards.
- Keep a second player nearby: personal Event movement and camera must not take over that player's actors or controls. Attempt a global actor in the editor and confirm rejection.
- Run historical Event Move Routes, animation, dialogue, sound/voice and quest examples unchanged.
- Test dark maps, fog, tileset Event graphics, native map edge clamps and different viewport sizes; compare preview with the client.
- On the target GPU, profile 5 Hz map readback, repeated open/close, memory and resize. Verify no old map image persists when maps change.

No connected test result is implied by this checklist. Record the installation, map/scene IDs, test accounts, build commit and observed result when executing it.
