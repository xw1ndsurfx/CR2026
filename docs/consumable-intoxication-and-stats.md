# Consumable intoxication and timed stats (Corps Royaux)

## Grog progression

Successive Grog consumptions now accumulate up to five doses while the configured
Grog effect is active. First dose applies the original visual effect only; the
second to fifth doses enable increasing levels of sideways movement (12%, 24%,
36%, 48% per step). One dose decays every 15 seconds; a full expiration clears
the state. Vomiting begins at four or more doses. The client HUD displays a
small mug, the current 1-5 dose level, and the **actual time until sober**,
updated by the server when each dose wears off. It auto-hides at zero or
when large status overlays are suppressed.

The player client predicts the staggered step and transmits its actual chosen
direction through the existing movement pipeline. Normal collision checks and
server position verification remain in place. This is intended as a playful
effect; it is **not** an anti-cheat-enforced movement disability.

## Temporary stat bonuses and penalties

For consumable items, the existing `StatsGiven` and
`PercentageStatsGiven` arrays now apply as time-limited stat modifiers instead
of permanent equipment modifiers. In the item editor, open
**Timed stat effects** while editing a consumable to set flat and percent
modifiers for Attack, Ability Power, Defense, Magic Resist and Speed.

The new **Stat effect duration (seconds)** editor field allows a 1-3600 second
effect duration completely independent of the item's reuse cooldown.
A value of **0** preserves legacy behavior: duration uses the old item
cooldown (in milliseconds, capped at 30 minutes), or Grog duration if the item
has no cooldown, otherwise 30 seconds.

The new `ConsumableStatDurationSeconds` item property requires a simple
`Items` table migration on both MySQL and SQLite. Migrations add the integer
column with a default of 0 so existing items keep their old duration rules.

The server validates item consumption, applies stat modifiers, refreshes rather
than stacks a repeat use of the same item, caps total modifiers, and broadcasts
stat updates on activation and expiration. Active stat modifiers are session-only;
cooldowns continue to use the game's existing persistence.

## Manual QA checklist

- Drink Grog once: green overlay, mug HUD at level 1/5, countdown appears, no crooked walking.
- Drink Grog twice, three, four, five times: increasing sideways steps and
  vomiting only at four or more doses.
- Try moving into blocked tiles and map edges while drunk: no clipping.
- Wait at least 15 seconds without drinking: gait becomes progressively normal, level and countdown update and the HUD disappears at sobriety.
- Set both positive and negative stat percentages and a custom duration in the
  item editor; save, reopen, and verify values persist across editor/server restart.
- Confirm a 20-second effect with a 3-second cooldown expires at 20 seconds,
  not 3; confirm another item's duration=0 follows legacy rules.
- Verify English/French/Spanish labels and HUD position below the minimap.
- Apply the SQLite/MySQL migration against a backed-up staging database.
- Consume an item: stats update, then revert exactly at expiration.
- Repeatedly consume the same item: refresh instead of stacking that item's stats.
- Use Grog and a normal potion concurrently; verify mixed modifiers and cooldowns.
- Reconnect or die during effects; verify no stuck movement or permanent stats.
- Build client, server and editor, and test live two-client sync before merging.
- Check the HUD countdown when the final dose decays before the original Grog
  duration and after receiving a nearby player's vomit splash packet.
