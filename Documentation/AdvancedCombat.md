# Corps Royaux — Advanced PvE combat

## First iteration: dodge, parry and riposte

Deploy the client and server together; set `Combat.EnableAdvancedCombat = true` in the server configuration and restart the server. The option remains disabled by default. Existing **E attack**, **Q/right-click shield block** continue working. **Left Shift dodge** and **R counterattack** are remappable in Controls.

- Dodge: one server collision-checked tile using the existing dash transport and animation; 3 s cooldown and 180 ms protection against PvE NPC **weapon** strikes.
- Perfect parry: block with an equipped shield within 200 ms; negate a single NPC weapon strike and open a 1.5 s counter window.
- Counter: server verifies target, reach, cooldown, position and instance; exactly one +25% bonus weapon hit against the NPC that was parried.
- This iteration does not change PvP or spells.

## Second iteration: configurable monster warning patterns

Use **NPC Editor > Combat > Attack telegraphs**. Bosses announce their melee strikes automatically when advanced combat is on, retaining their old single-tile setting unless configured. For other NPCs, check **Warn before this NPC attacks**. Pick a pattern, reach and warning duration (0 uses the server's default) and see the exact footprint in the 9×9 editor preview:

| Pattern | Resolution |
| --- | --- |
| Target tile | Original target tile (legacy boss behavior) |
| Straight line | 1–4 tiles ahead of the NPC |
| Forward cone | Widening cone in facing direction, 1–4 tiles |
| Circle around target | Radius 1–4 tiles around original target position |

The server freezes the footprint when the windup starts, clips tiles to map bounds and broadcasts them to players in the map instance. The client draws red warning tiles that intensify toward impact. When the server windup expires (250–2000 ms), each eligible player still standing in those tiles receives one normal NPC weapon hit; **dodging away avoids the attack**. Stuns, deaths, map changes and instance changes cancel pending warnings. Repeated warnings respect attack cooldowns and don't track moved targets.

NPC settings are stored on each `NPCDescriptor` with new SQLite and MySQL migrations `20261106040000_NpcAdvancedTelegraphs`. The editor's control labels and options are localized en/fr/es.

### Recommended QA

1. Run migrations on a staging server. Change an NPC's telegraph settings, save, close the editor, reopen it, and check values survive a restart.
2. Set a normal monster to **Circle**, radius 2, 1000 ms. Verify a nearby player sees the warning and receives a strike only if still on one of the highlighted tiles.
3. Try **Line** and **Cone**, including near map edges. Confirm the drawn area is exactly what can be hit, and nobody in another map instance receives damage.
4. Check that bosses without custom settings still announce their one-tile attack. Verify pet attacks, NPC-vs-NPC, spell casts and PvP remain unchanged.
5. Test success and failure of dodge, perfect parry and counterattack during a warning. Confirm deaths/teleports/stuns cancel the queued hit.
6. Check low and high latency, solo and group combat and in dungeons before enabling in production.

**Not included yet:** stamina meter, bespoke Admurin telegraph sprites, SFX, spell telegraphs or individual skill attack sequences. These are separate follow-up changes.

Server combat options: `EnableAdvancedCombat`, `AdvancedDodgeCooldownMs`, `AdvancedDodgeInvulnerabilityMs`, `AdvancedParryWindowMs`, `AdvancedCounterWindowMs`, `AdvancedCounterBonusPercent`, `BossTelegraphDurationMs` (fallback for NPC windup values ≤ 0).
