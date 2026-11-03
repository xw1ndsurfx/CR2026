# World events, boarding combat and dungeon exit fixes

World boss statuses now identify the spawned NPC. NPC death records a terminal encounter signal without acquiring the scheduler gate and immediately broadcasts closure. The client also closes the exact boss HUD on its entity death packet and suppresses delayed active snapshots for that NPC; a new spawn remains eligible. Rewards still use the scheduler's once-only finalization.

Scheduled invasion music, fog, tint and night lighting begin five minutes before the scheduled start, independently of earlier reminders and cinematic lead times. Earlier warning messages and their sounds/Common Events remain scheduled normally. The obsolete editable offset before the first warning is removed from the editor. Existing configuration files remain readable. Manually started cinematic countdowns longer than five minutes also defer ambience until their final five minutes.

The accessible-house directory supplies explicit fonts and sizes for all controls and a localized owner caption in English, French and Spanish.

Boarding NPCs have encounter-local hostile behavior, full-deck detection and combat movement even when their source NPC is passive, static or configured to hold position. Leash/combat-time resets do not interrupt this fight. Authored NPC descriptors and pets remain unchanged; the override is removed after completion, failure or the final abandonment. New spawns on the boarding instance receive the same behavior.

Dungeon abandonment now uses its own explicitly laid out modal confirmation rather than shared InputBox JSON. Confirming sends the existing run-specific request and the server retains participant, run and map checks. Cancellation and stale run changes close the dialog safely; gameplay input is blocked while it is open.

Validation covers the full twelve-sailor deck pursuing and dealing real melee damage, passive/static source overrides and restoration, server abandonment validation, terminal world boss processing, late client snapshots, invasion timing including midnight, EN/FR/ES text, native editor controls and software-rendered window previews. Software previews use test fonts/skin and do not replace a live multiplayer playtest with the game's actual assets.
