# Events, shop prices and Death Editor

## Event animations

The event page layer (below / same as / above player) now also controls its attached animations. Both animation layers render in the event's sorted slot instead of the global above-player pass. Movement-route layer changes apply as well.

Event Editor has **In-game animation offset (pixels)** X/Y controls. Positive X moves right; positive Y moves down. These offsets do not move the event's graphic, collision or interaction tile. **World Map marker offset** remains separate.

## Shop price suggestions

Shop Editor provides **Suggest price** beside each cost. Hover to see the amount and calculation, click to apply it, then add/update the shop entry and save normally. Existing prices are never changed automatically.

The suggestion uses `Item.Price / Currency.Price`, rounded up for items sold by the shop. Shop buyback uses 50% of that converted value, rounded down, with a minimum of 1. Both Item Editor values must be positive. No suggestion is made for an unknown item/currency or exchanging an item for itself. This is an editable starting point for balancing.

## System Editor > Death Editor

Add a named rule, check its affected maps, choose an optional common event and death animation, then select the respawn map, zero-based tile X/Y and facing direction. A map belongs to one rule. Save waits for server confirmation; configuration is stored in the server's `resources/deaths.json`.

On death, normal death triggers, item drops and experience-loss settings still apply. A configured rule then:

1. Runs the selected common event (eligible page conditions still apply).
2. Waits for that event to finish. Dialogue responses are allowed; movement, attacks, new interactions, item use and spell use are blocked.
3. Hides the player, nameplate, health bar and attached effects, then plays the chosen animation at the original death tile for nearby players in the same instance.
4. Waits for the longer visible animation layer, including its repeats, then teleports to the configured location and restores vitals through normal respawn handling.

The common-event timeout is configurable from 1 to 600 seconds (default 120). It releases any event movement hold before continuing. A missing/removed animation is skipped. Animations longer than 60 seconds are rejected on save; if an asset later exceeds that limit, it is skipped. Logout completes the pending respawn before saving, avoiding an invisible/dead character on reconnect. Invalid optional configuration is logged and falls back to ordinary respawn without overwriting the damaged file.

Active dungeon runs keep their existing lives/retry/exit handling. Maps with no matching rule keep their existing respawn. Client, server and Game Editor must all be rebuilt and deployed from the same source version.
