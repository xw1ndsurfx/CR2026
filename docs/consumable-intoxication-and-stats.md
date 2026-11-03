# Consumable intoxication and timed stats (Corps Royaux)

## Grog progression

Successive Grog consumptions now accumulate up to five doses while the configured
Grog effect is active. First dose applies the original visual effect only; the
second to fifth doses enable increasing levels of sideways movement (12%, 24%,
36%, 48% per step). One dose decays every 15 seconds; a full expiration clears
the state. Vomiting begins at four or more doses.

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

Effect duration uses the configured **item cooldown in milliseconds**, capped
at 30 minutes. For zero-cooldown Grog, duration equals its Grog effect duration;
for any other zero-cooldown consumable, duration is 30 seconds. This preserves
compatibility with existing item database tables (no migration). A subsequent
change could separate stat duration from reuse cooldown via a dedicated field.

The server validates item consumption, applies stat modifiers, refreshes rather
than stacks a repeat use of the same item, caps total modifiers, and broadcasts
stat updates on activation and expiration. Active stat modifiers are session-only;
cooldowns continue to use the game's existing persistence.

## Manual QA checklist

- Drink Grog once: green overlay, no crooked walking.
- Drink Grog twice, three, four, five times: increasing sideways steps and
  vomiting only at four or more doses.
- Try moving into blocked tiles and map edges while drunk: no clipping.
- Wait at least 15 seconds without drinking: gait becomes progressively normal.
- Set both positive and negative stat percentages in the item editor, save,
  reopen, and verify values persist across editor and server restart.
- Consume an item: stats update, then revert exactly at expiration.
- Repeatedly consume the same item: refresh instead of stacking that item's stats.
- Use Grog and a normal potion concurrently; verify mixed modifiers and cooldowns.
- Reconnect or die during effects; verify no stuck movement or permanent stats.
- Build client, server and editor, and test live two-client sync before merging.
