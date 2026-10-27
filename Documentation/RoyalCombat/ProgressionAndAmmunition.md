# Talent progression and ammunition selection

This continues the first archery slice on the same draft PR. It does not supply production
art, multiplayer competition, paid respec, NPC milestone quests, cosmetic titles or naval
weapon mastery. Existing game items, artwork, spell/projectile definitions and naval ammo
storage remain authoritative.

## Authoring and compatibility

1. Open Weapon Mastery Editor. Select a category, set `PointsPerLevel` (default 1) and map
   its existing weapon items. At cap 30, default progression yields 29 spendable points.
2. Open **Mastery talent tree**. Add/edit nodes or use **Archery talent templates** on an
   empty tree. Set stable keys, levels, rank limits 1–5, prerequisite key/rank and passive
   damage/speed/critical amounts. Invalid, cyclic or missing prerequisites are rejected.
3. For an active talent select an existing native spell for every rank. Partial mappings
   cannot save. All-empty rank mappings are valid for passives; a talent with no passive
   effect and no spell mapping cannot be purchased. Defaults do not fabricate spell GUIDs.
4. Save the child dialog and then the parent configuration. Cancel leaves the source draft
   intact. Server validation checks the actual spell resources. Child dialogs use the editor's
   normal theme and center on their owner.
5. Open **Ammunition compatibility**. Select an existing ranged weapon and check existing
   alternate projectile definitions. The original projectile stays available automatically.
   Each projectile supplies its native ammo item, amount, range and impact/spell effects.
   Two different candidate projectiles cannot share an ammo item ID for the same weapon,
   because selecting an item would otherwise be ambiguous. Use existing stackable ammo items.

Default archery templates: Steady Aim (+0.5% weapon base damage per rank), Quick Shot
(+0.5% attack speed per rank), Eagle Eye (+1 critical chance per rank), plus disabled active
templates Piercing Arrow, Multi Shot and Rain of Arrows awaiting real rank spells. These are
initial authoring defaults requiring balance review, not a claim that all those named skills
already exist in the game's content database. Existing saved configurations without talents
load with an empty tree; authors opt in via templates. No existing category is overwritten.

**Mapped active spells become mastery-gated.** Select dedicated abilities intentionally:
while mapped, even an already-known copy requires an unlocked rank and compatible equipped
weapon. Use native Spells Editor definitions appropriate to each rank. Do not repurpose an
ordinary class ability unless this restriction is intended. No second casting engine is added.

## Player behavior

Character > Weapon Masteries displays level, XP, percentage, remaining XP and points.
Selecting a talent first asks for confirmation; one confirmed rank costs one point. Level,
prerequisite, rank cap and budget are checked again by the server using persisted XP.
Purchasing during combat, death or archery training is rejected. There is no respec button
in this slice: do not describe it as an Aureons respec implementation.

Active ranks are taught into free native spellbook slots. If the next spell cannot fit,
the purchase is refused without consuming a point. Native spells use existing hotbars,
targets, casting times, cooldowns and projectile logic. Rank validation runs again at cast
completion, including after equipment changes. Unlocked spells missing after a restart are
reconciled on login and when the mastery panel opens. A full spellbook can require freeing a
slot and reopening that panel. Spells first learned through mastery are recorded durably;
removing/remapping a talent does not turn a stale learned spell into a free ordinary ability.

Inventory > Ammunition shows existing owned ammo plus compatible choices for the equipped
weapon. Incompatible or insufficient entries are disabled; the server independently verifies
the equipped weapon and ownership when a selection request arrives. The original and alternate
icons are taken from existing items. The **Cannon ammunition** action opens the existing naval
inventory and controls; cannonballs are not moved into a new parallel stockpile.

Selection persists per character and weapon. Equipment swaps resolve the new weapon's own
choice. Depleting a selected ammo type stops firing instead of silently consuming another type.
The server chooses the projectile and deducts the required quantity across stacks; failed
deductions do not fire. Normal attack, equipment/swap and cast execution are serialized through
the player's entity lock. Shots/casts are blocked during active shop, bank, crafting or trade
interactions; unsolicited party/friend invitations do not block combat. Explicit native spell
projectiles retain their authored ammunition requirements; selecting a weapon's fire arrow
does not silently replace a multi-projectile spell with a single-arrow projectile.

Official/training archery still uses only virtual arrows and normalized shared ballistics.
Mastery passives do not affect its score, charge or trajectory. Crafting, loot and shops continue
using existing ammo item IDs; this feature does not generate new ammo item variants or recipes.

## Persistence and checks

New additive tables in `resources/weapon-masteries.db`: `WeaponAmmoSelection`,
`MasteryTalentRanks`, `MasteryTalentPurchases`, `MasteryLearnedSpells`. Character/account/item
tables are unchanged. Rank writes and purchase receipts share one SQLite transaction; replaying
the same purchase request cannot charge a second point. Character, category and weapon keys
separate progress. Back up this database with the matching configuration and native character
database while the server is stopped. Keep category/talent keys stable after release.

Validated locally: 108 rules checks; all 431 server tests, including concurrent spending and
last-arrow deduction, insufficient multi-stack ammo, native spellbook limits, equipment gates,
historical spell mapping, profile persistence and packet serialization; full existing client UI
suite including confirmation/compatibility/disposal; focused native editor controls; all eight
existing mini-game editor currency/catalog groups. The old catalog assertion is now based on
the actual mini-game enum and includes the ninth game, Archery. Client, server and editor compile.
Player captions have English/French/Spanish entries and matching placeholders.

Still required before production: real-world multiplayer/trade/bank/crafting and crash-recovery
load tests, broader transaction auditing of pre-existing inventory writers, actual connected
game-data authoring, balancing and actual-client visual QA. The new tests exercise real server
methods and native UI controls, not a running production world. No production screenshot or
full anti-macro/exploit-proof claim is made. Production art, full cinematics, advanced target
graph editing and advanced multiplayer tournaments remain follow-up. Online public mastery
inspection and paid category resets are now implemented; see [mastery management](MasteryManagement.md).
A first local-range multiplayer score competition is available;
see [competition setup and limits](Competitions.md).
