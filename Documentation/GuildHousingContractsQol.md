# Guild, housing and Royal Contracts usability

This update adds the following player and editor paths. New interface strings are supplied in English, French and Spanish.

## Guild emblems and overhead labels

Open **Guild emblem** from the guild member window. Submission requires a staff account with Moderator/Game Master or Administrator rights, active Premium and a guild. Guild leadership alone does not grant submission access. Only an Administrator can approve, reject or remove an emblem.

The in-game file browser accepts an RGB/RGBA PNG up to 128 by 128 pixels and 64 KiB. The server checks the PNG structure, CRC, dimensions and bounded decompressed size, and removes ancillary metadata. Pending images are visible only to administrators and the submitting guild's eligible staff. A revision check prevents an administrator from approving a replaced submission. The previous approved emblem stays public while a replacement awaits approval.

Records are saved atomically in `resources/guild-emblems.json`. Approved images appear beside the guild name. Public clients never receive pending images through the overhead-label lookup. The guild label uses smaller, warm lettering; AFK is the highest row above guild/title/name labels. Floating action text, including EXP, follows the character label zoom scale.

## Housing services

* **Open furniture shop** (event command 1092) opens the furniture catalogue separately from My House. Put this command on a merchant NPC. Purchases require the event session, the same map instance and a position within five tiles of the merchant. The session expires after fifteen minutes.
* **Open house directory** (event command 1093) opens a searchable, paginated list of public houses, including offline owners, plus the player's own house. Visiting still uses the normal server privacy and travel checks.
* My House retains storage, placement, pets, privacy and saved layouts, without a purchase tab.

## Lobby Editor and shops

**Lobby Editor** is available in the editor hub/content editors. Set an image filename from the distributed client's GUI assets and a maximum party size for each activity. Settings are saved in `resources/lobbies.json`. Effective capacity also respects the server party limit, dungeon capacity and ship seats. Existing party invitations respect a listed lobby's capacity.

Both normal Shop Editor offer lists have search fields, horizontal scrolling and full-caption tooltips. The sprite selector's OK/Cancel actions now close and dispose the actual parented selector; OK commits the selected sprite and Cancel preserves the previous graphic.

## Grog and pet icons

Choose **Grog (green screen and vomiting)** as a consumable effect in Item Editor. Value is duration in seconds, bounded to 5–120 (a zero value from authored data uses 30 seconds). The player receives a fading green overlay and recurring animated vomiting. The item's configured animation is also replayed for vomiting when present. Nearby players within two tiles on the same map instance receive a brief 3.5-second green splash; a splash does not trigger further vomiting. Effects do not change movement or combat rules.

An NPC marked as a pet and linked through its summon item automatically supplies the item's portrait. The client crops the facing-down sprite frame to its visible bounds and caches a square portrait for inventory, bank, bag, hotbar, shops, rewards and other item displays. Item Editor also previews the linked pet instead of requiring a separate item icon.

## Royal Contracts

The selected daily/weekly tab has the gold border. Reward images appear in the contract offer/details and accepted-contract journal details. Reward names use the item name directly, without the old unknown-name prefix.

The editor supports ships sunk, dungeon completions, quest completions, invasion victories, successful enemy-ship boardings and qualified world-boss victories, alongside the existing kills, deliveries and exploration. Quest objectives can select a specific quest. The other new activity objectives currently cover any activity of that kind. Templates continue to apply at the next rotation.

Progress comes from authoritative server completion hooks, only after acceptance. Naval boarding progress is queued out of the naval registry lock before touching player progress. Accepted, unclaimed contracts are listed in Quest Logs; selecting one also tracks its objective and count on the HUD. Journal refresh is read-only and available away from the board; acceptance, delivery and reward claims still require the board session/proximity checks.

A newly completed contract queues a gold completion notification. Repeated state packets do not repeat it, and login snapshots do not replay previously completed contracts.

## Validation

* Royal Contracts/server rules: 125 checks, including new objective boundaries, PNG validation, packet round trips and lobby configuration bounds.
* Royal Contracts UI: 413 checks, EN/FR/ES layouts and rendered previews, reward art and selected-tab behavior.
* General client UI suite: all eight main scene groups and module regression checks, including separate furniture purchases, guild permission states, directory pagination, AFK ordering and completion-notification deduplication.
* Editor service checks: sprite OK/Cancel lifecycle, lobby fields/translations, Shop Editor search, housing graphic picker and existing naval-shop layout.

These automated checks use native controls and synthetic rendering fixtures. Multiplayer appearance, supplied animations and final art should also be exercised in the configured game world before release.
