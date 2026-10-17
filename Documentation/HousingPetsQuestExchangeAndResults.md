# House residents, quest exchange and event panels

## Pets at home

Sending a pet home now selects a nearby free tile instead of using the packet's default (0,0) coordinates while already inside. Placement avoids occupied resident/furniture anchor tiles and non-walkable map attributes. Multiple residents receive separate positions. House snapshots repair old corner/invalid/duplicate pet positions and refresh their sprite from the NPC descriptor while preserving names and progression. Residents render in the same elevation pass as the player so upper-level house decor does not hide them incorrectly.

The Pets window's home action now uses the same explicit font and framed caption as the other readable pet actions.

## World Boss and invasion panels

The World Boss HUD replaces unsupported bullet/em-dash separators with ASCII punctuation. Captions fit their allotted width and preserve the full text in tooltips; the contribution ranking wraps inside a scrollable area. The expanded panel fits the viewport and retains its minimize/restore control and player preference.

Invasion and World Boss result windows now share a responsive layout with a headline, a scrollable full summary, a distinct EXP reward ribbon and an accessible Continue button. Text wraps instead of being cut by a fixed-height multiline label. Damage and effective healing have separate lines. Layout reserves space for the native title bar and adapts down to a 640x480 viewport. All generated player-facing text has English, French and Spanish entries.

## Quest Shop redesign

The Quest Exchange reuses the actual Marketplace frame renderer and palette. The desktop layout has a category sidebar, selectable quest cards, and a story/prerequisite detail pane. On smaller screens, the detail pane replaces the list temporarily and a Back to quests button returns to the list. Existing quest-offer authorization is unchanged: View Quest requests the ordinary server offer; it does not accept quests automatically.

- Search matches quest names, descriptions, requirements and category names, including cached content translations.
- Status filters: all, available, in progress, completed, locked, and repeatable.
- Categories come from the existing quest folders in Quest Editor. Quests without a folder appear under General. No separate shop category configuration is required.
- Category/status/search filters combine. Same-shop updates preserve the search; Reset filters restores the full catalog.
- The result count shows matching / total quests. Full selected quest descriptions and prerequisites wrap and scroll.

Quest category and repeatability fields are appended to the existing packet keys. Rebuild and restart client, server and editor together. No database migration is required.

## Verification

Regression coverage exercises real Gwen windows at 1920x1080, 1280x720 and 640x480 in all three languages, long result summaries with wheel scrolling, viewport/button bounds, long boss names and damage rankings, quest search/category/status/repeatability combinations, packet round-trips, home-action text metrics, free-tile placement and old resident sprite/position repair. UI fixtures use synthetic rendering metrics; final artwork and live gameplay still need checking in the rebuilt game.

## Premium furniture

The Housing catalog has a Premium required checkbox per furniture definition. Existing definitions default to false. The server checks the account's active Premium status before charging a purchase and before placing or moving furniture, using the current catalog requirement when the owned snapshot is older. A retired definition retains the requirement saved with ownership.

Expiry preserves quantities and placed decoration. Owners can still store their furniture; buying, placing and moving restricted furniture require renewal. Client catalog/storage actions display the restriction in English, French and Spanish. The owner view carries the server's current Premium status for presentation; the server remains authoritative. No database migration is needed.
