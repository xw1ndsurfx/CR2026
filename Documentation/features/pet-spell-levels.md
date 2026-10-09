# Familiar spell unlock levels (NPC Editor)

This configuration **only affects companions**, not ordinary NPCs.

## Game Editor

1. Open **NPC Editor** and select a pet NPC (**Summonable companion** enabled).
2. Add the desired spells in the existing **Spells** section.
3. Select a specific spell entry in that list.
4. Scroll to **Player companion / Pet** and set **Required level for selected spell**. The list shows `(Lv. N)` next to each spell.
5. Repeat for other spells, then **Save**.

**Auto** restores the previous rule (first spell at level 1, then one every **Default spell interval (auto)** levels) for that slot. Existing pets preserve this rule automatically until an explicit override is assigned. Custom requirements may be non-consecutive or out of order (e.g. levels 5, 10, 25). Each entry in the spell list has its own requirement, even if two entries use the same spell.

## Runtime and compatibility

- The **NPC Editor** retains its original spell list for both NPC types.
- Only summoned player-owned companions filter spells according to the saved requirement; regular NPCs continue their original random spell selection.
- The pet state packet and the pet window use the exact same required-level calculation as the server AI.
- Adding/removing a spell in the editor also adds/removes its pet-level entry at the same index. Replacing a spell in a slot preserves that slot's level.
- Older pet configurations with no explicit per-spell levels continue to use the legacy default interval.
- Database migration `20261009060000_AddPetSpellRequiredLevels` adds a nullable companion-only column to the `Npcs` table; ordinary NPC rows are untouched in behavior.

## Validation

Verify on a staging copy of the game database: create a pet with spells configured for levels 3, 10 and 25; confirm that at level 3 only the first spell is eligible, at level 10 two are eligible, and at level 25 all are eligible. Configure out-of-order levels to verify eligible spell selection is not limited to the first N slots. Check that ordinary NPC spells are unchanged, that editing/reordering/removing entries does not shift another spell's required level unexpectedly, and that Save/reopen in Game Editor and restart the server preserve the settings.
