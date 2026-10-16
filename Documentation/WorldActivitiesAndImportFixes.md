# World activities, bank access and character imports

Rebuild the client, server and editor together. These changes include an additive nullable `BankAccount` player column for SQLite and MySQL; no existing item rows are removed.

## Combat music and scheduled experience

Open **System Editor > Combat Music & Experience Events**. Select a track from `resources/music` and an after-combat grace period (default 8 seconds, in addition to the existing server combat timeout). The track continues across map changes and nearby fights, then the current map music returns. Invasion music always has priority. Re-requesting an already playing track keeps its playback position. A blank combat track disables the override.

Experience events have an enabled flag, name, selected weekdays, start time, duration and additional percentage. Schedules use the server-local clock, like World Bosses, and may cross midnight. The strongest currently active percentage applies when schedules overlap. The bonus is additive on base **character** experience; profession/pet/mini-game progression is unchanged. Starts/ends use localized announcements. Configuration is saved atomically in server `resources/world-activities.json` and persists across restarts.

## Personal bank

The same System Editor panel configures the minimum level, opening fee and currency. Defaults are level **14** and **3 Auragems**, identical for free/Premium players. An empty currency selection resolves the existing Auragems/Auragem item by name; explicitly select the project's currency to avoid ambiguity. Existing stored goods are grandfathered for the opening fee; the level rule still applies. The opening fee and account flag save together; failures roll back both.

Free storage keeps its existing base capacity. Premium adds one equal-capacity tab, server-authorized automatic sorting and gold window borders. Expired Premium storage remains visible and withdrawable; deposits, internal moves into that tab, and auto-sorting are rejected server-side. Stored goods are never deleted or hidden because a subscription expired. Guild banks keep their existing permissions and capacity.

## Character Import / Generator

ZIP imports accept known category folders with or without a `Paperdolls` ancestor, and infer a category from artist filenames when possible. Female/Male folders and animation subfolders are preserved. Existing artwork is skipped, not overwritten; unsafe paths and invalid PNGs are rejected. All Body families except the `Ape` body layer are treated as Base, including previously imported assets.

**Add to Character Creation** and **Export Paperdoll + Item** resolve the matching opposite-sex family automatically. If absent, a Yes/No/Cancel prompt offers copying the selected art to the other sex, exporting only the selected sex, or cancelling. Distinct `_male` / `_female` sheets and their animation overrides are retained; generated items fill both gender fields appropriately. Appearance previews, character selection and in-game base layers resolve the selected gender variant. Gender-only styles are filtered when switching sex.

## Interface fixes

The persistent phase indicator uses an explicit readable font, fixed top-left bounds, a translucent background and front ordering. Quest journal and quest offer descriptions accept vertical scrolling and grow to their text height. Quest Shop uses bronze cards, status colors and translated headings. Potion recipe choices show the output icon and avoid unsupported bullet glyphs. Completion rewards display Change Item cards; positive rare-or-higher gains use the same animated rarity presentation as rare loot.

The Premium-condition editor crash was a missing `GetEventConditionalDesc(PremiumStatusCondition)` overload. A specific formatter plus a generic condition fallback now supports dynamic requirements and event condition descriptions.

## Validation

Targeted automated tests cover real ZIP import/reimport and male/female sheet export, Premium dynamic description dispatch, schedule boundaries and overlap, music priority/grace, long-text wheel scrolling, locale coverage, rarity gain presentation, and real bank deposit/withdraw restrictions plus opening-fee rollback. Live audio transitions, content-specific appearance placement and multiplayer play remain in-game checks after rebuilding.
