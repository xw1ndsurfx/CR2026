# Editor layouts and Royal Seals exchange

## Editor corrections

- Tileset content explicitly defines both scroll extents, including after a tileset changes size. Selection coordinates and drag scrolling continue to use the native scrolled PictureBox.
- Shop offer groups start at their new viewport origin when reparented. Both item lists keep independent scrolling and stay aligned when the editor is resized.
- Lobby Editor uses the shared searchable GUI image selector and preview. All activities, including Roll the Dice, have explicit rows linking their name, image and party capacity. Saved image references are retained when an asset is missing. Safe relative GUI asset paths are supported.
- NPC Auto Creator converts its respawn presets from seconds to the native millisecond field: for example, a normal level-20 NPC receives 40,000 ms and a level-20 boss receives 360,000 ms. Existing NPCs are updated when the creator is applied again.

## Client corrections

Guild Emblem text, buttons, lists and file selection controls explicitly resolve a font, avoiding zero-width/zero-height text measurement. The client's global font preference remains respected. Incorrectly encoded accents in its translations are repaired.

Spell shop icons fit within the item recess. Hovering a spell icon opens the standard spell description, including its combat statistics. An offer without a custom icon uses the spell's icon.

Weapon Masteries shows a visible EXP progress bar below the existing level and current/required EXP labels.

Beer/Grog applies a world-only blur using offscreen rendering, linear downsampling and a nine-sample reconstruction. Menus are drawn after this pass. Blur fades near expiration. A nearby vomit splash retains the brief green overlay; drinking initiates blur. Vomit particles start eight world pixels higher than before.

## Royal Seals shop

1. Create a shop in Shop Editor and choose **Royal Seals shop** as its configuration type.
2. Add products to **Items Sold** and enter each unit price in Royal Seals. Inventory currency selection and item buyback are disabled for this type.
3. Players open **Exchange Royal Seals** from Royal Contracts, choose a configured exchange, choose a quantity and buy. The price displayed on the row is the total for that quantity. A normal Open Shop event can also open a specific Royal Seals shop.

The server anchors access to the opening location and map instance for fifteen minutes, checks proximity and current product/price quotes, and limits purchase quantities. It rejects purchases after an offer is reordered or changed. Purchases save the seal balance and granted items together; insufficient seals, full inventory, save failure or an exception leave both unchanged. Equipment rewards use separate native item copies. Native item-currency purchase/sale packets cannot operate on a Royal Seals shop.

No player-database migration is required: the balance uses the existing Royal Contracts profile, and the shop configuration uses the existing shop JSON serialization. New enum and packet fields are appended.

## Validation

Automated checks cover both native tile scrollbars and scrolled coordinates; Shop Editor placement at 1100x760 and 800x600; all lobby image/capacity rows and locales; mastery progress; seal transactions and rollback; equipment copies; native persistence; packet quotes; configured reward icons and balances; and locale/token coverage. Native editor and software-rendered Gwen previews are generated for inspection. The configured game world's final blur, skin assets, spell hover and vomit placement should also receive a live playtest.
