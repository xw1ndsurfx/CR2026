# Royal Weapon Mastery / Royal Archery — integration slices 1–6

Status: **draft, not a production-complete Royal Archery Tournament**. Audited base:
`aaab6f7ce8d23083ebc1ca9f697b2c68e5da2a51` on `main`.

The second slice adds talent allocation, native spellbook unlocks and weapon ammunition
selection. See [progression/ammunition setup and exact limits](ProgressionAndAmmunition.md).
The third slice adds authored rounds, moving targets, countdowns, an aim-preserving zoom
and a native perspective target editor. See [range authoring and timing](RangeAuthoring.md).
The fourth slice adds local-range multiplayer registration, normalized shooting cards and
live/final session standings. See [competition setup and exact limits](Competitions.md).
The fifth slice records completed ranked cards and adds day/week/season best-score boards
in the client and native Wiki API. See [rankings, persistence and operational limits](Rankings.md).
The sixth slice adds online public mastery profiles and an opt-in paid category talent reset
with a saved recovery record. See [mastery management and recovery](MasteryManagement.md).

## Audit and source of truth

The supplied mandate covers weapon mastery, universal ammunition, immersive archery,
production artwork, tournaments, cinematics and two editors. The referenced ChatGPT
conversation was read. Its final answers are content-reference markers; no image attachment
or usable ten-panel mockup was returned. The detailed mandate supplied with the task is the
functional reference. No composite poster has been cut into supposed production sprites.

The repository contains an Intersect/MonoGame client, server, WinForms editor, native
combat/projectiles, professions, NPC events, mini-games, guild/party systems, achievements,
naval ammunition, Cinematic Director, map-perspective helpers and EN/FR/ES localization.
Relevant existing extension points:

| Existing system | Source / decision |
| --- | --- |
| Weapon damage, actual health damage | `Intersect.Server.Core/Entities/Entity.cs`; attach mastery to native attacks |
| Kill credit and AFK status | `Intersect.Server.Core/Entities/Player.cs`; award only server-credited NPC kills |
| Projectile ammo | `ProjectileDescriptor.AmmoItemId` / `AmmoRequired`; existing item IDs remain authoritative |
| World projectile consumption | `Intersect.Server.Core/Networking/PacketHandler.cs`; retain native items/consumption, stop firing if deduction fails |
| Naval ammo inventory | `Intersect.Server.Core/Naval/NavalSystem.AmmoInventory.cs`; no migration or redesign |
| Server mini-game sessions | `Player.Jousting.cs`, `Player.MiniGames.cs`; follow native packet/hold/lifecycle patterns |
| Events and NPC entry | `StartMiniGameCommand`; append `Archery = 8`, preserve every existing serialized value |
| Cinematics | `FrmCinematicDirector`, native cinematic commands; events may play an existing scene before opening training |
| Editor navigation | `frmMain.EditorHub.cs`; register mastery under Content Editors and archery under Mini-Games |
| Player language | `ClientLanguages` + shared `QualityOfLifeText` catalog; new captions have EN/FR/ES |

Production asset directories contain `.gitkeep` files, not the game's Admurin tiles, bows,
arrows, icons, paperdolls, maps or databases. No real item/map IDs are invented. No inventory
icon, equipment sprite, paperdoll, naval ammo definition or account schema is replaced.
Inspecting those actual assets and their licensing remains required before producing art.

## Delivered code

### Mastery foundation, with real combat effects

* Configurable categories, explicit existing weapon-item IDs, independent character XP,
  cap (default 30), base XP curve and damage percentage per level.
* Defaults: Archery, Swordsmanship, Daggers, Axes & Hammers, Staves & Magic. Weapon mappings
  start empty. Cannoneering must be integrated with the existing naval gunnery progression
  in a later change, not approximated by mapping cannons to ordinary equipment.
* Each level requires `BaseExperience * level²`; total XP is retained independently of RPG
  level. Default damage bonus is `(masteryLevel - 1) * 0.5%`, applied to native weapon attack
  base damage before the normal combat formula. Native rounding/formulas still apply.
* Both melee and item projectiles pass the original source weapon into the damage routine.
  Equipping another weapon while an arrow is in flight does not transfer that arrow's bonus.
* Each server-credited NPC kill gives the mastery the same rolled NPC reward and
  party/equipment/title/event/potion bonuses used for the character. The category
  comes from the recorded source weapon, including projectiles fired before a
  weapon swap. Consume the per-NPC hit record once. No two-second/minute/level-gap
  limit discards rapid kills; the category's configured maximum still applies.
  Characters at their RPG cap can keep training weapons without banking character XP.
* The existing timed profession-progress HUD displays the category, actual clamped
  XP gain and level progress, then disappears automatically after five seconds.
* Weapon mastery level is a native condition (serialized id 1006) available in
  Event branches/pages and item usage requirements. Select the configured mastery
  category, comparison and level. Direct/event-driven equipping also checks the
  item's requirements. Missing categories deny access, including negated conditions.

* Transactional SQLite storage in `resources/weapon-masteries.db`, keyed by character and
  category; commit before advertising the new bonus. Existing account/item tables are untouched.
* Character window action displays categories, level, XP, percentage, remaining XP and
  available points. It is self-only, with scrollable categories/talents, rank allocation and
  confirmation. Online Character Information also displays mastery progress and opens a
  read-only talent tree for other characters. Level-up animation remains future work.
* Talent ranks 0–5, level/prerequisite gates, configurable points per level, transactionally
  persisted purchases and request receipts. Default archery templates include passive damage,
  speed and critical bonuses; active talents need real existing spell mappings before purchase.
  Active abilities use the native spellbook, targeting, cooldowns and spell/projectile engine.
* Inventory > Ammunition shows owned ammo and compatible options for the equipped weapon,
  reuses existing icons, persists selection per weapon/character and links to native cannon
  ammunition. Alternate ammo points to authored projectile definitions, retaining their effects
  and quantities. World weapon firing resolves the server choice; shortages do not auto-switch
  ammo. Native projectile spells retain their explicitly authored ammo IDs.

### Free solo archery training

* NPC event and Character window entry; configured map and exact shooting tile validated
  by the server. Combat, casting, incompatible activities and movement holds reject entry.
* Native Gwen viewport backed by the MonoGame client: foreground bow geometry, pointer
  aim, timed draw/release, sway, a moving projectile, perspective ground and projected target.
  This is a live scene driven by trajectory data, not a static screenshot.
* One physical target plane per round, configurable target positions and radii (default
  0.6 m); ballistic gravity 9.81 m/s². Initial speed
  `20 + 35 * charge` m/s. Charge reaches full power after 1.2 seconds. Wind is a horizontal
  acceleration in m/s² and can be configured between -3 and +3.
* Defaults: three rounds of five arrows at 10, 25 and 50 m, 100 seconds each; radial rings
  award 0–10, maximum 150. Authors may configure 1–6 rounds, 1–10 arrows per round,
  5–100 m, 15–300 seconds, round wind and up to eight static/periodically moving targets.
  Impacts become visible after the flight deadline, remain attached to the moving target,
  and are scored exactly once. Zero-score misses are not drawn as arrows in the target.
* No inventory call occurs in the training session. Arrows are virtual; no currency,
  inventory reward or mastery XP is granted. Player mastery never changes training physics.
* The client submits only draw/release, bounded aim, session ID, sequence and expected round/shot. The server
  computes charge, sway, flight time, collision and score; rejects stale/wrong-session inputs.
* A short click cancels drawing without spending a virtual arrow. Draw expires after ten
  seconds. Expired rounds forfeit unused arrows; accepted arrows can finish their flight
  after the deadline. Leave, disconnect, death, combat, position,
  map and instance changes release the native player hold. World movement, attacks and
  lobby travel are blocked during active training.
* HUD contains round, score, arrows, distance, wind, charge, countdown, active target,
  zoom and last-shot/Bullseye feedback. Right click cycles 1×/2×/4× while preserving the
  world aim under the pointer. Audio and further playtesting remain required.

### First editor views

Weapon Mastery Editor: categories, cap, curve, damage tuning, checked list of existing
weapon items, talent/prerequisite editing and five native rank-spell pickers, plus a visual
existing-weapon/projectile compatibility picker. Archery Mini-Game Editor: ranges, native map preview, click to place the
shooting tile, enabled state and wind. Its staged range-rules dialog provides round/target
settings, draggable perspective target placement and animated motion preview. Both views share one live draft so switching editor
menus cannot overwrite the other view's unsaved edits. Configuration saves are validated
on the server and use a temporary file followed by atomic replacement. Property-grid fields
are technical authoring controls; a talent node-graph layout and scenery-layer authoring remain future work.

## Run on a development copy

1. Build/deploy matching **client, server and editor** from this branch. Do not mix old and
   new network builds. Keep `main` and production data untouched until reviewed.
2. Back up the development server's full `resources` directory. Include
   `royal-combat.json` and `weapon-masteries.db` in future server backups. Stop the server
   before copying the database; do not delete it to solve a read error.
3. In Game Editor > Content Editors > Weapon Mastery Editor, select existing bow/sword/etc.
   items for the correct category, then save. The server checks that mapped IDs resolve to
   equipment in the weapon slot and that an item belongs to only one category.
4. Open the intended **existing** safe map. Mini-Games > Archery Mini-Game Editor > New range
   on current map. Click a passable shooting tile, set `Enabled = true`, choose wind and save.
   Use Range rules and targets for rounds, target placement and motion. No range is enabled
   by default because the repository supplies no authoritative world map.
5. On an existing Master Archer NPC event, add Mini-Games > Start Royal Archery Training.
   Optionally precede it with a native Cinematic Director scene. Finish the event after the
   open command so its own movement hold is released. There is no new cinematic interpreter.
6. Log in, walk to the configured tile, interact with the NPC, then choose Start training.
   Move the mouse to aim above the highlighted target, hold left mouse to draw and release
   to fire. Right click cycles zoom without moving the aim under the pointer.
   Character > Royal Archery Training is an additional entry point for development checks.
7. Check all fifteen arrows, misses, bullseyes, cancel/close, death/disconnect and return to
   world movement. Verify inventory quantities before and after. Use mapped weapons against
   eligible real NPCs and check that mastery XP persists after reconnect/restart.

The XP curve and damage percentage need balance review before live enabling. There is no
Premium multiplier or paid unlock. Rolling back binaries leaves the separate mastery database
intact; older clients do not understand the appended mini-game type, so also restore authored
events from backup when rolling back.

## Verification

```powershell
git submodule update --init --recursive
dotnet run --project Utilities/RoyalCombatTests/RoyalCombatTests.csproj -c Debug
dotnet test Intersect.Tests.Server/Intersect.Tests.Server.csproj -c Debug --filter "FullyQualifiedName~RoyalCombatTests|FullyQualifiedName~JoustWorldTests|FullyQualifiedName~Ammo"
dotnet run --project Utilities/PokerUiTests/Intersect.PokerUiTests.csproj -c Debug
dotnet run --project Utilities/InterfaceEditorTests/Intersect.InterfaceEditorTests.csproj -c Debug -p:RuntimeIdentifiers=win-x64 -- --royal-combat-only
dotnet build Intersect.Client/Intersect.Client.csproj -c Debug
dotnet build Intersect.Server/Intersect.Server.csproj -c Debug
dotnet build Intersect.Editor/Intersect.Editor.csproj -c Debug
```

The independent rules harness covers timing, finite bounds, replay rejection, 150-point
matches, collision/flight/wind, expiry and XP/configuration limits. Server tests enter the
real player session handler, check interrupted sessions and packet serialization, reopen
SQLite progression, separate categories/characters and verify source-weapon attribution.
Client tests exercise the actual Gwen viewport through the existing headless metrics renderer
at 1280×720, 1366×768 and 1920×1080, plus late snapshots after close and FR/ES catalog resolution.
**Headless metrics rendering is not a screenshot of a running production client.**

Local sixth-slice validation: 152 rules checks and all 499 executed server tests passed;
the full existing client UI runner passed including archery, ammo compatibility, talent
confirmation, custom moving-target rounds, zoom, sixteen-entry competition/ranking boards and
late-refresh disposal checks; focused
real editor controls passed, including target dragging and 760×520/1100×650 rendering.
All eight existing mini-game editor test groups also passed in the second slice. Client, server and
editor compile in Debug. The Windows editor runner needs `RuntimeIdentifiers=win-x64`, as
in existing CI. Existing compiler/package warnings are not resolved by this feature.

## Artwork inventory and missing delivery

**New graphic files: zero.** The current target, terrain, string, bow and projectile are
code-native diagnostic shapes. They must not be advertised as Admurin production artwork.
There is no delivered PNG pack, atlas, source layer file or production screenshot in this slice.
No existing artwork has been recreated or blindly extracted from a poster.

After the real assets and mockup are available, produce and validate:

| Production group | Required separate assets / metadata |
| --- | --- |
| Perspective scenery | Sky, mountains, castle, trees, ground, fences, foreground; day/evening layers with independent parallax |
| Native map terrain | Range tiles matched to actual editor tile size (16×16 only if confirmed), stands, hay, flags, tents, podium, Duke's dais |
| Targets | Standard/royal/training/moving faces and separate supports; target-centre pivot, radius and hit-test hotspot; no baked arrows |
| Foreground action | Bow/hands idle/draw/release, mapped to equipped appearance where possible; flight and planted arrows, impact frames |
| UI/VFX | Reticle, charge/wind adornments, bullseye effect, confetti, ceremony; text remains localized UI, never baked into sprites |
| Manifest | Per asset: path, width/height, alpha, pivot, frame rectangles, duration, atlas, physical scale and resource destination |

Dimensions, palette, pixel density and animation conventions must follow the supplied game's
actual resources. Use integer/nearest-neighbour presentation and transparent PNGs where useful.
Do not substitute generic art to claim completion. Production art needs its own loader/manifest
validation and actual-client QA; the current code-native viewport does not yet load such a pack.

## Remaining mandate / ordered continuation

1. **Ammunition hardening:** broader trade/bank/crafting/crash-recovery and real-world load
   testing, native cannon progression integration and presentation polish. The base category,
   compatible selection, existing-projectile mapping, cross-stack consumption, equipment
   serialization and native naval entry point are implemented; naval storage is preserved.
2. **Mastery abilities/UI:** author actual rank spell mappings for Piercing Arrow, Multi Shot
   and Rain of Arrows using the connected game's existing spell data; no fictional spell IDs
   are pre-enabled. Points, ranks, prerequisites, passives and native spell unlocks are implemented.
   Online Character Information and configurable paid talent resets are implemented.
   Other authored weapon trees, Cannoneering, notifications, NPC tutorials,
   milestone quests 10/20/30, level-30 titles
   (Royal Eagle Eye, Royal Blademaster, Master of the Royal Artillery), achievements/Steam.
3. **Production archery presentation:** approved separated art above, layer rendering and
   parallax, optional third person, bow animation, audio and further aim calibration.
   Zoom, countdown, target placement, periodic target motion and round settings are implemented.
4. **Competition:** local-range registration for 2–16, common rules/start, server scores,
   withdrawals, live/final standings, persistent day/week/season boards and normalized
   equipment/mastery/skills are implemented. Production capacity tests, broader matchmaking,
   dedicated instances, rounds/finals, reconnect, spectator scoreboard and Aureons/cosmetic
   rewards remain future work.
   Named modes: Royal Training Grounds, Royal Archery Championship, Marksman's Challenge,
   Archery Survival, Archer Duel, Grand Duke Ansfroi's Tournament, Guild Tournament; future
   Castle Defense/invasions. The delivered competition is a simultaneous score card;
   the named advanced modes and their tournament structure remain unfinished.
5. **Native cinematic sequences:** introduction, view transition, finals, decisive shot,
   feasible slow motion, podium/Duke ceremony via existing Director and Events/Time tools.
   This slice only provides a native event entry point, not those finished sequences.
6. **Advanced editors:** scenery-layer placement, arbitrary target paths, talent graph,
   prerequisites/effects, rewards, scheduling, leaderboards/cinematic hooks, authored playable
   defaults on real game maps and verified layout at small resolutions.
7. **Release QA/docs:** world combat/spells/crafting/naval/quest/dungeon/invasion regression,
   Party/Guild/Steam support where available, player/developer FR/EN wiki (and ES player
   captions), account/item migration tests if required later, load/performance tests and real
   client screenshots/videos using the actual world data. No production-world E2E run or
   multiplayer performance claim is made here.
