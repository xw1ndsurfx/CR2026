# Player houses, furniture catalog and pet recovery

## Content setup

Rebuild the server, client and editor together. The server applies an additive `Housing` player column for SQLite/MySQL; existing characters start with a private, empty house.

Open **Game Editors > Housing & Furniture Catalog**. Housing starts disabled: create an isolated interior map without public exits or gameplay rewards, select it as the template, choose a valid entry tile and enable housing. Each owner receives a separate shared instance; approved visitors join that same instance. A saved return location brings players back when leaving or reconnecting after a restart.

Add catalog rows with a name, currency, price, tileset, source X/Y in pixels and width/height in tiles. `Months` accepts exact UTC months separated by commas, such as `2026-10,2026-12`. Empty months archive an offer. Keep furniture IDs stable. Purchased furniture keeps its appearance and remains owned after an offer expires or is removed. The catalog uses existing tileset artwork; it does not generate furniture assets.

Configuration is saved to server `resources/housing.json`. Authored furniture names enter the editable content translation workflow; the translation service must already be configured. New player controls have English, French and Spanish translations.

## Player controls

**My house** opens the monthly catalog and owned furniture. Enter the house, choose **Place** or **Move**, click the destination tile, and rotate with the mouse wheel. Right click cancels placement. **Store** returns placed furniture to storage. Furniture is decorative and does not alter map collision.

Houses are private by default. Owners can open or close public access. An online or offline friend's profile offers **Visit house** when public. Closing a house removes visitors. Dungeon participants must leave their run before visiting. **Leave house** restores the prior location; a later explicit game warp or death destination takes precedence.

Use **Send pet home** in the Pets window to display an owned pet in the house. **Collect pet** returns it to the companion collection. Stored pets animate but do not fight or collect loot; their level, experience and nickname remain intact.

Purchases, inventory debit and furniture ownership are checkpointed together. Decoration requests are checked server-side against ownership, stored quantity, house instance, coordinates and configured limits. Visitors cannot edit another house.

## Pet defeat and invasion loss

Server `config.json` > `Npc.PetRecoverySeconds` defaults to **300** seconds (accepted range 0–86400). Pet death stores an absolute UTC recovery time, so relogging, summon items and summon commands cannot bypass the delay. The Pets window displays the remaining time.

In Invasion Editor, configure the defeat message and two defeat sounds. Supported message substitutions are `{name}`, `{island}` and `{target}`. The client plays sound 2 after sound 1 finishes, skips a missing file and stops the sequence on logout. Normal configuration/start failures do not trigger defeat audio.

## Validation

Automated coverage checks UTC offer boundaries, furniture stock and placement validation, profile/packet persistence, the additive SQLite migration, real pet death recovery and both summon paths, house owner/visitor UI controls and layout, and actual playback-completion sequencing with a fake audio backend. The native editor tests cover the new catalog and sound controls. Live multiplayer decoration and audio require an in-game validation after configuring content.
