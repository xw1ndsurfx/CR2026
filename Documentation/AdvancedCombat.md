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

## Third iteration: endurance, confirmed combat feedback and enraged bosses

**Depends on the phase 2 PR:** this work is a stacked PR against `feature/advanced-combat-pattern-editor-20261106`. Merge that PR first; then retarget phase 3 to `main`. Do not deploy phase 3 to a client or server running only the first combat version.

### Endurance

The server owns all endurance values, spent on **successful** actions:

| Action | Cost | Feedback |
| --- | ---: | --- |
| Dodge (Left Shift) | 25 | blue status flash, swish |
| Perfect parry with shield (Q) | 15 | gold flash, metallic clang |
| Counterattack (R) | 10 | bronze flash, impact sound |
| Not enough endurance | none | warning and muted tone |

A default bar holds 100 points, regenerates 18 per second after a 1.4-second delay from the latest spend. Failed moves and blocked directions do **not** spend resources. The server sends a validated state packet (`AdvancedCombatStatePacket`) after actions and during regeneration; the client never predicts or alters endurance. The bar appears under normal gameplay, hides in modal gameplay views, and uses the game's existing draggable HUD system.

All options are server-side: `AdvancedStaminaMaximum`, `AdvancedStaminaRegenPerSecond`, `AdvancedStaminaRegenDelayMs`, `AdvancedDodgeStaminaCost`, `AdvancedPerfectParryStaminaCost`, `AdvancedCounterStaminaCost`. The system is still disabled by default through `Combat.EnableAdvancedCombat`.

Audio includes five *original* synthetic WAV samples in `resources/sounds/advanced-combat/`. They must be included in the deployed client sound resources: `dodge.wav`, `perfect-parry.wav`, `counterattack.wav`, `exhausted.wav`, `boss-enrage.wav`. Missing audio never changes combat authority.

### Boss enrage

A boss can optionally enable `Enrage under 35% HP (boss only)` in **NPC Editor > Combat > Attack telegraphs**. It is **off by default** for legacy bosses. Once it drops to 35% HP or below, its already announced melee strikes:
- shorten telegraph windup by 20%, with a safe minimum of 250 ms;
- gain a 25% base damage bonus;
- render warning tiles with an orange/red tint, and show the combat alert and audio the first time it enrages.

The server options `AdvancedBossEnrageHealthPercent`, `AdvancedBossEnrageDamageBonusPercent` and `AdvancedBossEnrageWindupReductionPercent` tune this. The enrage boolean is stored with the NPC descriptor using the new `20261106073000_NpcBossEnrage` SQLite/MySQL migrations. No PvP, pet, spell or existing non-opted-in boss behavior changes.

### Phase 3 verification

1. Enable advanced combat on staging; test full bar and gradual regeneration, including after relog, late packets and switching characters.
2. Repeated Dodge should not cross blocked tiles or spend stamina when it fails; when low, attempts receive an exhaustion warning.
3. Parry should only cost endurance when it is truly perfect, and the resulting counter should cost endurance once (not twice).
4. Observe phase-two combat: line, cone and circle warnings should remain consistent with server hit zones.
5. In NPC Editor, enable boss enrage; save/reopen, restart SQLite and MySQL servers. Lower health under configured threshold; verify faster telegraphs, bonus damage, first-enrage cue, and no change to bosses that are not opted in.
6. Run the CI `advanced-combat-phase3.yml` on Windows; test with actual packaged sound assets and low/high-latency clients before merging to production.

**Later phases:** custom illustrated Admurin UI sprites, stamina consumables/buffs, combo attack sequences, boss spell telegraphs, combat tutorials and balancing passes.

## Fourth iteration: boss multi-strike combos

This is a **stacked PR** on top of phase 3, which itself depends on phase 2. It does not touch `main`. Merge and validate in the order **#241 → #242 → phase 4**.

Boss NPCs can now opt into **2- or 3-strike** telegraphed attack sequences in `NPC Editor → Combat → Boss attack combos`, independent of their optional enraged phase. Existing bosses remain single-strike when not opted in.

The editor provides:
- Toggle: **Enable multi-hit boss attacks**, boss-only.
- **Number of attacks**: 2 or 3; capped server-side regardless of editor data.
- **Attack 2 shape** and **Attack 3 shape**, each chosen from target, line, cone or circle.
- **Next warning duration**: 250–2000 ms, default 550 ms. Every hit gets its own fully visible warning before it lands.
- **Preview attack number**: 1, 2 or 3. The main warning preview reflects the currently selected strike in the combo and uses the same geometry functions as the server.
- Localized labels in FR/EN/ES.

### Server and client contract

The first strike uses the NPC's original telegraph shape and windup. After it resolves, the server re-evaluates the original target's **current** position and announces the next authored shape. The footprint is frozen for each windup and strikes all valid players still inside that same footprint when its countdown expires. Players can evade every strike separately.

The server cancels the entire combo if the target dies, leaves the original instance, becomes unavailable or moves too far away, or if the attacker dies, teleports or is stunned. Each attack chain consumes one normal attack cooldown after its final hit or cancellation. An enraged boss can still receive the phase-3 windup reduction and damage bonus on each strike.

`CombatTelegraphPacket` keys 7 and 8 carry optional 1-based `ComboStep` and `ComboTotal`; zero is the legacy no-combo value. The client displays a **boss combo step** message and plays the new original `advanced-combat/boss-combo.wav` sound at the start of a chain. No client packets can start or advance a combo or authorize hit damage.

The five NPC descriptor fields are saved in the SQLite/MySQL `20261106090000_NpcBossCombo` migrations. Advanced combat stays globally disabled unless `Combat.EnableAdvancedCombat=true`.

### Phase 4 acceptance checklist

1. Verify old bosses with combos disabled still perform one telegraph and strike.
2. Enable 2-hit combo (target → cone). Save, reload NPC editor, restart staging server; compare preview geometry to actual red warning tiles.
3. Enable 3-hit combo (target → line → circle). Change direction and walk out of each footprint independently. Each step must have its own visible warning and **at most one hit per player**.
4. Attempt map edge, dungeon instance boundaries, logout, boss death, stun, target death, long-range movement. The remaining combo must cancel safely; no cross-instance hits.
5. Verify combo alert 1/3, 2/3, 3/3 and the sound resource in the packaged client. Check French, English and Spanish labels and cooldowns.
6. Enable optional enraged boss and confirm each combo step adjusts speed/damage correctly; confirm ordinary NPCs never activate boss-only combos.
7. Validate migrations against both SQLite and MySQL and compile/test client, server and editor, including the prior 51+ checks. Run actual gameplay and latency testing before enabling in production.

**Out of scope:** boss spell/AoE projectile combos, animation-specific hitframes, player combo attacks, bespoke Admurin sprite atlases and a combat tutorial.
