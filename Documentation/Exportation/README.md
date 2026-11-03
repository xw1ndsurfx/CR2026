# Exportation — foundational implementation

**Current status: code foundation, not playable in the live client.** No live client, quest, NPC, editor, or naval event integration has been wired yet.

- Authored route configuration has source NPC/map, recipient NPC/map, existing Quest ID, profession minimum, requested goods grouped by cargo type and reward.
- `ExportationRules` validates the order packing, sealed cargo, bound ship embarkation, verified docking and one-time delivery payout.
- Death destroys sealed cargo until turn-in; sinking of the bound ship during the sea leg also destroys it. Failure returns the player to the *same* merchant and resets the packing.
- `ExportationText` has EN/FR/ES translations embedded in the project.
- `Intersect.Tests.Server/ExportationRulesTests.cs` adds NUnit regression tests for packing, wrong ship, transport, loss, retries, rewards and serialization.

## Runtime integration remaining

1. Add network requests/state and a persisted player order. Construct actor context on the server only. Only one active order per player.
2. At seal, persist quest activation and the run together. At completion, persist currency, profession XP and quest completion atomically; restore state on save failure. Use existing quest and economy APIs.
3. Wire embarkation and actual docking from the naval system, and death/sinking signals. Prohibit inter-island teleport bypasses.
4. Register Exportation into the native profession system after connecting its XP storage; no XP for packing alone.
5. Implement the in-game preparation window, command editor, event hooks, and assets. The concept backplate, 4x3 sprite atlas, and ten WAV SFX are delivered separately and are **not yet committed to the repository**.

Do not merge as a playable feature until runtime integration and end-to-end tests pass.
