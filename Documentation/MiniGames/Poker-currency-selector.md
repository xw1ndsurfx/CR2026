# Poker: event currency selection and green experience bar

## Status: configuration milestone, NOT inventory accounting

This change adds the requested item selector to `Start Mini-Game -> Poker` and
publishes the earlier green XP-bar patch. It does **not** yet implement Aureons
buy-ins, payouts or refunds. No inventory or player-database balances are changed.

Selecting an item saves the event configuration, but the server refuses that
event with an explicit message instead of opening an unfunded table under the
name of a real currency. Select **None / Test chips (no inventory)** to keep
playing the existing test version. This remains the default for old events.

## Selection

`Table currency / Monnaie` lists created item definitions compatible with
`ItemDescriptor.IsStackable`: Currency items (including those whose Stackable
checkbox is disabled/unchecked) and other explicitly stackable items. Equipment,
bags and items with an invalid inventory stack limit are excluded.

The dropdown includes the folder, item name and a short identifier. The complete
ID is shown below it. Use the dropdown's type-ahead to navigate the list.

The event stores `CurrencyItemId`, a GUID, not the name, list index, image name or
folder. Renaming or reordering objects does not change which item was selected.
Missing or retyped items stay visibly selected as invalid references; saving is
blocked until the author chooses a valid item or explicitly chooses test chips.
Cancel leaves the original event unchanged.

For example, select the existing `[CURRENCY] / Aureons` object. There is no
hard-coded Aureons name or GUID. The same mechanism can configure another table
for another compatible currency object. Configure all access events to a table
consistently.

The starting amount is labelled `Planned buy-in (item units)` when an object is
selected. It is only a stored setting at this milestone, not a charge. The
summary in the event command list also identifies the selected object and marks
inventory payments as unavailable.

The dialog is scrollable and resizable, and its Save/Cancel buttons remain in a
fixed footer instead of scrolling out of view.

## Green XP bar

The poker scene now uses a green fill and highlight on a dark track with a
visible border. The track is 18 design pixels high and scales with the scene.
At zero progress the track remains visible; the fill corresponds to progress
within the current poker level. XP formulas, saved progression and B1-B6 unlocks
are unchanged. No new images or fonts are required.

This is the same bar change previously supplied as `poker-exp-verte.patch`, now
in the repository. Do not apply that patch a second time.

## Remaining accounting work

Before enabling inventory-backed play, implement and validate:

- Atomic inventory-to-table buy-in, bound to the configured object ID and an
  authenticated player; never grant the test starting balance as spendable items.
- Persistent escrow and settlement receipts in the same transactional boundary
  as inventory changes; safe recovery, full-inventory refunds, disconnects and
  restart behavior without lost items or duplicate credits.
- Explicit, durable NPC funding limits; the current refillable test NPC stacks
  must not become an unlimited source of inventory currency.
- Separation of funded and test tables, XP policy, authoritative currency labels,
  and rejection of conflicting settings for the same table.

Those operations are intentionally not simulated by renaming test chips. Keep
this branch in draft and use a development server.

## Validation

`CurrencySelectionTests` adds regression checks for old event defaults, GUID
serialization, Currency items with Stackable unchecked, eligible and ineligible
item types, duplicate names, renaming, retained missing IDs and betting limits.
`GreenExperienceBarTests` checks the actual scene's draw calls at 0%, 50% and 100%
progress at two canvas sizes. Existing engine, networking, UI and persistence
checks remain enabled. These are not a manual in-game or real-inventory test.
