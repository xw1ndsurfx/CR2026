# Combat music, PvP, menu animations and blacksmith workshops

Rebuild and deploy the client, server and editor together from the actual CR2026 checkout. Do not copy test-build binaries: each checkout can have different handshake keys. Startup applies the additive NPC ExperienceMaximum migration on SQLite or MySQL. Existing NPC rewards and explicit individualized-loot settings remain unchanged.

## Combat playlists

System Editor > Combat Music > Map combat playlists selects multiple music files and a group of maps. Each map belongs to at most one playlist. The client chooses one track per fight, keeps it through the configured grace period and across maps sharing the playlist, and changes to the relevant playlist on a boss map. The global combat track is the fallback. Invasion music retains priority. Place the referenced audio files in resources/music on every client.

## Ranked PvP and Capture the Chest

System Editor > PvP configures the enabled flag, exactly ten names and increasing point thresholds, ranked maps, kill/death points, win/defeat points, repeated-opponent cooldown, minimum match duration, maximum level difference and same-IP protection. Defaults are 25 points per kill, 5 per death, 100 per win, 25 per defeat, a ten-minute opponent cooldown and sixty seconds minimum match duration. Character variables persist ranking points and cooldowns; the top 100 includes offline characters. Party > PvP opens the arena directory and ranking. Grades appear beside names, on the local character panel and in profiles; the display option can hide grades beside names.

Create a dedicated Arena-zone map, then add it under Capture the Chest. Configure both base tiles, 1-8 players per team, minimum level, winning score and duration (ten minutes by default). An optional item supplies the chest icon; otherwise localized red/blue labels mark the chests. Keep base tiles reachable and clear of blocking resources/events. Arenas start automatically when both teams are full, balance levels by alternating the sorted roster, and use separate shared instances. No private-instance or dungeon travel is available through the queue.

Walk onto the enemy chest and bring it back while your own chest is at base. Touch your dropped chest to return it. Dropped chests return automatically after thirty seconds. Death drops the chest, hides the character for five seconds, and respawns at the team base with three seconds of protection; ordinary item/EXP death penalties do not run. The score target or timer ends the match; tied scores draw. Leaving or disconnecting can forfeit a team. Rewards exclude repeated opponents, accounts, same-IP players when enabled, and excessive level differences. The original map, position and vitals are restored on exit. An interrupted login returns to the last overworld position.

## Main-menu animations

System Editor > Main Menu Animations chooses existing animations, screen coordinates in percent, scale, transparency, looping and foreground/background placement. Saving copies both animation-layer definitions into this installation's resources/config.json. Distribute that client configuration and referenced animation textures; no login or server game-data download is needed to play them. These are snapshots: save again after changing the source animation. Animation panels pass clicks through to menu controls.

## NPC experience

NPC Editor exposes Min EXP and Max EXP. A kill rolls one inclusive reward shared by its existing party/pet/equipment/title/event reward calculation. Old records with maximum zero keep their fixed reward. Auto NPC Creator generates a range from 80% to 120% of its calculated base and enables Spawn loot for all attackers. Newly created NPC definitions also default to individualized loot.

## Royal Foundry and Royal Forge

Mini-Game > Royal Foundry / Royal Forge opens the recipe workspace. Add real ore/ingot/weapon items, select ingredients and quantities, output item/quantity, optional Blacksmith profession, required level, profession EXP, required quality and sounds from resources/sounds. Enable finished recipes. Disabled drafts remain editable. Add Start Mini-Game > Smelting or Smithing to a station's event; this authorizes the nearby player to open that workshop. There are no fabricated default recipes or item IDs.

The foundry heats material with the bellows, then pours three moulds by timing the moving marker. The forge heats, hammers four times, then quenches at the right temperature. Animated fire, molten metal, hammer strikes and sparks accompany actual ingredient/output icons. The server owns time, heat, strikes and quality; client requests cannot submit a score or reward. Active station sessions block walking and expire if the player leaves the location, logs out or stops refreshing.

Materials must be unequipped in the main inventory. A successful craft checks available space, consumes ingredients and inserts the output in a single account checkpoint. A failed save restores the inventory and permits Collect again; a paid session cannot issue the reward twice. Failed timing/quality attempts preserve materials. Profession EXP uses the existing profession/EXP-event system, and the output uses existing item-change notifications and rare-item effects. Configure a profession on the recipe to award profession EXP.

## Validation

Automated coverage includes playlist routing; grade boundaries and persisted anti-farming cooldowns; chest theft, return, capture, death/drop, expiry and forfeit; successful and timed-out foundry/forge play; inventory checkpoint failure rollback and committed quantities; NPC inclusive random bounds and migration preservation; packet serialization; en/fr/es coverage/placeholders; display preferences; stale workshop packets; offline menu animation scale/offset/opacity; small/large client layouts and actual WinForms editor construction. Multiplayer combat, authored map collision, station events and the final audio/visual experience still require in-game verification with matching deployed builds and real content.
