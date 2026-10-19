# EXP potions, loot events, buyback prices and item descriptions

Deploy matching client, server and editor builds. The additive SQLite/MySQL ShopBuybackDiscount migration preserves existing shops with a zero reduction. No character migration is required: potion percentage and UTC expiry use persisted character variables.

## EXP potions

In Item Editor, choose Consumable > EXP boost potion. The first value is duration in seconds (1-604800); the percentage is extra EXP (1-1000). For example, 1800 seconds and 50 gives +50% EXP for thirty minutes. The tooltip and top-left HUD show the effect and remaining time. The clock continues while disconnected and changes of map do not remove the boost.

An identical percentage extends the duration, capped at seven days from now. A weaker potion is refused without consumption. A stronger potion replaces the previous percentage and duration. A full-duration boost cannot be extended by consuming another identical potion. Invalid settings are rejected in the editor and server. Item cooldowns and use events retain their existing behavior.

Personal and scheduled EXP percentages add. With +50% from a potion and +50% from an event, a base reward of 100 grants 200 before other existing bonuses. Character NPC reward messages show a separate POTION line. Mini-game/profession rewards use the combined percentage and display a general bonus label. Character, profession and mini-game level caps and receipt protection remain in force. Pet EXP rules are unchanged.

## Loot Booster Events

Events > Loot Booster Events has an independent schedule with selected weekdays, From/Until times and a bonus percentage. Server-local time, overnight windows and strongest-overlap rules match EXP events. The top-left HUD displays the bonus and time remaining; login snapshots are silent, while starts/ends are announced.

The bonus multiplies drop probability, not quantity or rarity. +50% changes a 20% chance to 30%; the final chance is capped at 100%, and zero-chance drops remain impossible. NPC loot rolls use the active event at death, including individualized loot and existing equipment luck. Harvesting rolls resource loot when harvested, so existing resources benefit immediately when an event begins. Guaranteed drops keep their configured quantity. Player death drops, purchases and crafted outputs are not boosted.

## Shop buyback reduction

Shop Editor > General > Buyback reduction (%) accepts 0-99. It reduces the amount paid by the shop when a player sells an item. It applies to explicit whitelist offers and to the item's base-price offer in blacklist mode. Buying from the shop is unchanged. Prices round down per unit, with a minimum of one currency unit for a positive original price. Client quotes and server payouts use the same helper. Zero preserves previous prices.

## Auto Creator descriptions

The optional flavor-description checkbox replaces the description with object-specific prose. It writes no Auto Creator marker, stats, level, rarity or price summary. Weapon/equipment names select blade, axe, bow, magical focus, shield, boots, helm and ornament descriptions; bags, currency and potions have their own text. The templates ship in English/French/Spanish, while custom authored text continues through the content translation catalog. Existing descriptions change only when applying the checked option; uncheck it to preserve handwritten text. Existing EXP boost potion settings are preserved by automatic stat generation.

## Premium ideas (proposals, not implemented here)

Prioritize saved housing layouts, saved cosmetic outfits, decorative pet beds/toys, and favorite recipes with material checklists. Further options include saved lobby profiles, cosmetic profile frames/trophy showcases, configurable house ambience, inventory tags/wishlists, a screenshot album, and saved House Party invitations. These complement the previously implemented Premium bank tab/sorting/appearance. Preserve acquired objects, decorations and saved creations after subscription expiry; limit new edits or extra slots instead. Keep PvP grades, matchmaking and combat statistics earned in play.

## Validation

Tests cover potion consumption, extension, weaker replacement refusal, reloaded variables and expiry; additive character/profession/mini-game EXP and repeated receipts; overnight loot schedules, capped probabilities and actual resource harvest generation; unchanged legacy shops and real SQLite migration; localized, stacked bonus HUDs on narrow canvases; matching client shop quotes; independent editor schedules and readable controls; narrative descriptions and all locale placeholders. Verify final audio/visual behavior and authored item/shop configurations in game after deployment.
