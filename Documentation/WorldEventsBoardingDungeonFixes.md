# World events, boarding combat and dungeon exit fixes

World boss statuses now identify the spawned NPC. NPC death records a terminal encounter signal without acquiring the scheduler gate and immediately broadcasts closure. The client also closes the exact boss HUD on its entity death packet and suppresses delayed active snapshots for that NPC; a new spawn remains eligible. Rewards still use the scheduler's once-only finalization.

Scheduled invasion music, fog, tint and night lighting begin five minutes before the scheduled start, independently of earlier reminders and cinematic lead times. Earlier warning messages and their sounds/Common Events remain scheduled normally. The obsolete editable offset before the first warning is removed from the editor. Existing configuration files remain readable. Manually started cinematic countdowns longer than five minutes also defer ambience until their final five minutes.

The accessible-house directory supplies explicit fonts and sizes for all controls and a localized owner caption in English, French and Spanish.

Boarding NPCs have encounter-local hostile behavior, full-deck detection and combat movement even when their source NPC is passive, static or configured to hold position. Leash/combat-time resets do not interrupt this fight. Authored NPC descriptors and pets remain unchanged; the override is removed after completion, failure or the final abandonment. New spawns on the boarding instance receive the same behavior.

Dungeon abandonment now uses its own explicitly laid out modal confirmation rather than shared InputBox JSON. Confirming sends the existing run-specific request and the server retains participant, run and map checks. Cancellation and stale run changes close the dialog safely; gameplay input is blocked while it is open.

Validation covers the full twelve-sailor deck pursuing and dealing real melee damage, passive/static source overrides and restoration, server abandonment validation, terminal world boss processing, late client snapshots, invasion timing including midnight, EN/FR/ES text, native editor controls and software-rendered window previews. Software previews use test fonts/skin and do not replace a live multiplayer playtest with the game's actual assets.

## Follow-up: sea boarding, reconnect and whip mastery

Unowned boarding enemies now receive the hostile movement override even when their NPC template is pet-capable. Actual summoned companions are excluded by their owner, including companions assigned during the spawn callback.

Disconnecting from an active dungeon preserves run membership instead of applying the shared-instance party kick. Login restores the original instance identity, its controller registration and the remaining eligible party members. Explicit departures and unrelated new parties are respected. Active dungeon maps retain their existing enemies and damaged health while everyone is disconnected, until the run finishes or expires. Boarding crew members retain eligibility while their captain is temporarily offline. This recovery covers reconnects to the same running server; it does not persist active runs across a server restart.

A normal weapon projectile can deal its lethal damage through an attached combat effect. That effect now carries the originating weapon into mastery hit attribution. Separately cast spells do not gain weapon mastery merely because a whip is equipped.

Validation: 347 server tests passed across naval encounters, dungeon exit/reconnect, Royal Combat, invasion simulation and pet spell requirements. Reconnect tests replace the player object, restore party/controller membership, abandon the original run, and check idle enemy preservation. Whip tests kill real NPCs through melee, projectile and attached-effect paths, and exclude ordinary spell casts. No live multiplayer session with the player's assets was available.
