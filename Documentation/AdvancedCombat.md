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

## Fifth iteration: native hostile targeted spell telegraphs

**This change is a stacked PR based on phase 4**; merge in order #241, #242, #243, then phase 5. It does not modify main directly.

### What players experience

For selected NPCs, **hostile native single-target combat spells without splash** now telegraph the target's tile in **blue** throughout the casting windup. A targeted player receives the localized `INCOMING SPELL - MOVE!` HUD prompt and an original `spell-warning.wav` cue. Moving away before the cast finishes avoids the targeted native spell. Remaining on the warned tile permits **the existing native spell implementation** to execute: mana, effects, stats and its normal cooldown are untouched. Moving off the tile cancels the spell at resolution and **still starts the spell's normal cooldown**. The client does not authorize movement, damage or spell impact.

An eligible spell must:
- be a hostile `CombatSpell` with target type `Single`, `HitRadius=0`, no pet owner;
- use a natural cast duration no greater than five seconds;
- be cast at a living, valid player on the same map instance;
- have `Combat.EnableAdvancedCombat` and the NPC's opt-in `AdvancedSpellTelegraphEnabled` enabled.

**Native AoE (including single-target spells with splash), projectiles, friendly/self spells, traps, warps and spells with longer casts are intentionally unchanged.** These need a separate geometry/hit resolution model before they can be made dodgeable; do not enable new warnings for them by treating the target cell as their entire hit area.

### Authoring

Open **NPC Editor → Combat → Attack telegraphs**, check `Warn before targeted hostile spells`, and set warning duration (250–2000 ms; 650 ms default). The actual warning lasts at least as long as the original spell's cast duration; it never shortens an existing cast. These two NPC descriptor fields are stored via `20261106100000_NpcAdvancedSpellTelegraph` on both SQLite and MySQL.

Blue means native spell warning; red/orange remains a server-resolved melee/AoE-pattern warning. The existing phase 3 stamina and phase 4 combos remain unaffected. Global advanced combat stays disabled by default.

### Verification

1. Use a boss with a **hostile single-target combat spell**, `HitRadius=0`, cast duration 0–5000 ms; enable telegraphs in NPC Editor and save/reload. Verify blue tile and cue on the target.
2. Move off the blue tile during cast. There must be **no spell hit**, and the spell must observe its cooldown. Wait in the tile: the native spell resolves normally, including effects and resource costs.
3. Change instances, die, log out or stun/silence the caster mid-cast. The spell must never land outside the original target/map instance.
4. Test with pets, heals, self-buffs, projectile spells, AoE/splash spells, warps, long casts and NPCs with feature disabled; all must behave exactly as before.
5. Test the blue marker in dungeons and on map edges. Confirm other players see the tile without receiving a target-only sound.
6. Validate the two new migrations, run CI builds/tests for Windows client/server/editor and verify resource packaging includes `resources/sounds/advanced-combat/spell-warning.wav`.

This phase does **not** introduce ground-locked AoE native spell execution or projectile prediction.

## Sixth iteration: server-confirmed player combos (PvE melee)

**Stacked PR:** this iteration is based on phase 5 and requires the previous phases in order (#241 → #242 → #243 → #244 → phase 6). No direct changes to `main` or unrelated gameplay are included.

### Activation and rules

The feature remains disabled unless both `Combat.EnableAdvancedCombat = true` and `Combat.AdvancedPlayerCombosEnabled = true` are configured on the server. It uses the existing **E attack** and ordinary server-authorized weapon/unarmed melee attacks; no new player action button or client-supplied damage value is introduced.

- **First successful hit:** starts a chain against that exact NPC and map instance; no damage bonus.
- **Second successful hit** within 2.5 seconds against the same NPC: +10% damage after normal damage calculation.
- **Third successful hit** within 2.5 seconds: +20% damage if at least 20 stamina is available, then spends 20 stamina *after the confirmed hit*. The chain resets after the third hit, even if it has insufficient endurance to grant the bonus.
- Hits must reduce a living NPC's actual health. Swings into empty space, blocked hits, casts, projectiles, pets, resources, other players and attacks without applied damage cannot advance the combo.
- A successful counterattack from phase 1 is excluded from player combo progression and does not stack its existing bonus with the new combo bonus.
- Timeouts, a new NPC target, death, disabling the feature or changing maps/instances invalidate/reset a chain.

The server owns `AdvancedPlayerCombo` state; the client only displays the optional additional fields in `AdvancedCombatStatePacket` (MessagePack keys 6–8). The combat HUD shows **Combo 1/3** or **Combo 2/3** with remaining time. The third hit displays **FINISHER 3/3!** and plays the original `resources/sounds/advanced-combat/player-finisher.wav` cue.

### Balance settings

| Server option | Default |
| --- | ---: |
| `AdvancedPlayerCombosEnabled` | false |
| `AdvancedPlayerComboWindowMs` | 2500 ms |
| `AdvancedPlayerComboSecondHitBonusPercent` | 10% |
| `AdvancedPlayerComboFinisherBonusPercent` | 20% |
| `AdvancedPlayerComboFinisherStaminaCost` | 20 |

The window is clamped to 600–6000 ms; bonus percentages to 0–100%. The finisher never circumvents the existing stamina limit, normal attack timers, PvP checks or combat protection.

### QA checklist

1. Build client/server/editor and run `AdvancedPlayerComboTests` plus all earlier phase tests.
2. Enable both server options on staging. Strike the same NPC three times with melee/unarmed hits, check 1/3 → 2/3 → finisher, and confirm the health damage bonus applies once per successful hit.
3. Miss, attack air, hit a resource, cast a spell, attack a pet/PvP player, switch to another NPC or another map/instance, wait out the window, or die; these must not improperly advance or retain a combo.
4. Repeat with less than 20 stamina. The third hit should not receive its bonus or spend stamina; the combo should reset normally. Confirm stamina is spent only after an eligible third hit inflicts real damage.
5. Perfect-parry then retaliate on the same NPC, and confirm the counterattack is excluded from the three-hit chain.
6. Test group combat, boss dungeon instances, low/high latency and all localization strings. Confirm the audio is packaged with the deployed client and `main` is untouched until review.
