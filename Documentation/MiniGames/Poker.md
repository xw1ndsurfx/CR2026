# Poker mini-game - milestone 2: event commands and shared server lobbies

## Status

Development branch only. The poker core, event command editor and shared lobby registry
are implemented. **There is still no playable poker window or client action packet.**
Activating an event joins/rejoins a server lobby and displays a development chat message.
It does NOT start a hand, debit currency, freeze movement or wait for a client response.
Do not merge/deploy this milestone as a complete player-facing mini-game.

## Configure an event

Build the editor and server from the same feature branch. In the event command selector:

1. Open `Mini-Games > Start Mini-Game...`.
2. Select Poker and enter a case-sensitive Table ID, for example `tavern-1`.
3. Choose 2-6 seats, starting test chips, small/big blinds and a 5-300 second turn timeout.
4. Save the command and the event. Cancel does not insert or modify a command.
5. Use an interaction/action-button trigger, not an autorun loop.

Players using the same Table ID on the SAME map and map instance share a table, including
when different event objects refer to that ID. Separate maps/instances never share it.
Use different Table IDs for separate tables on one map. All events for one table must use
identical settings: a conflict is rejected rather than reconfiguring a running table.
Table IDs accept 1-64 ASCII letters, digits, hyphens and underscores, without spaces.

`Mini-Games > Leave Mini-Game` leaves the current lobby. Commands are intentionally
non-blocking, with no success/cancel branches in this milestone. They must not be wrapped
in Hold Player without an explicit release. The current editor labels/status messages are
English development text; integration with the localization catalog remains to be done.

The serialized command enum uses reserved values 1000/1001. Existing values are unchanged.
DefaultValue annotations preserve nonzero defaults under IgnoreAndPopulate serialization.
The event editor, printer and server dispatcher are extended through separate partial files.

## Core and registry

Core files now live in `Intersect.Server.Core/MiniGames/Poker` (same namespace as milestone 1).
They moved from the executable project because event execution lives in Server.Core;
this avoids a circular project reference. The core evaluator/table logic itself is unchanged.

Core rules: 2-6-seat no-limit Texas hold'em; cryptographic shuffle; best-five-of-seven hands;
heads-up blind order; check/call/raise-to/fold; all-ins, side pots, uncalled refunds, ties and
clockwise odd-chip allocation. Private cards remain recipient-specific until a contested
showdown. A fold win does not reveal the winner's cards.

`PokerTableRegistry` provides:

- One seat per player, authenticated login-session tokens and idempotent event activation.
- Shared tables keyed by map ID, map instance ID and Table ID, with a fresh table-instance
  GUID on recreation to reject delayed actions targeting an old table.
- Validated settings, fixed table capacity, and rejection of conflicting event settings.
- Session/location/table/hand/revision checks on reads and actions.
- Recipient-specific detached updates, never a broadcast containing every private hand.
- Automatic leave on logout, replacement login, map/instance change, death or disposal.
- Retention of departing active-hand seats until settlement, including all-in eligibility;
  late arrivals wait for the next hand and can leave immediately.
- A bounded registry (1024 tables by default), empty-table cleanup and regular server ticks.

Presence callbacks execute outside the registry lock. The subsequent sweep checks membership
object identity, so a stale observation cannot evict a newly joined replacement membership.
The runtime adapter reads online Player identity and LoginTime under EntityLock. Its timer
runs at one-second intervals after first use, never performs network sends while locked,
and catches/logs sweep errors without terminating the process.

All balances are temporary TEST CHIPS. Leaving finally discards that seat's balance;
rejoining can intentionally grant a fresh test stack. Restarting loses all tables.
No Aureons, Royal Diamonds, inventory items, real money or rewards are involved.
Persistent stakes require a transactional ledger and crash recovery before they are enabled.
This is original code, not copied TLOPO game code/assets or a verified reproduction of its
specific variant, presentation, progression or payouts.

## Validation

With the .NET 8 SDK, from the repository root:

```sh
git submodule update --init --recursive
dotnet run --project Utilities/PokerSmokeTests/Intersect.PokerSmokeTests.csproj --configuration Release
```

The standalone executable links the actual evaluator, table and registry sources. It has
17 original core test groups (including 250 simulated hands) plus 18 registry groups.
Registry checks cover table identity, session isolation, capacity, repeated/concurrent joins,
private snapshots, stale requests, late joins, departures, cleanup and sweep/join races.
A nonzero exit code is failure. No game database or external test framework is required.

On Windows, build the real engine projects with dependencies:

```sh
dotnet build Intersect.Server.Core/Intersect.Server.Core.csproj --configuration Debug
dotnet build Intersect.Editor/Intersect.Editor.csproj --configuration Debug
```

The `Poker core smoke tests` and `Poker event integration builds` workflows run these checks.
Added tests are not evidence of success: consult the workflow result for the exact commit.
A standalone success is not a full engine build. Compilation is not a two-client playtest.

## Next acceptance gates

1. Network: action/state/close packets, per-recipient transmission, resync/reconnect,
   rate limits, and session/location validation immediately before accepting an action.
   Keep table settings and login tokens server-owned. Never serialize PokerTable/deck.
2. Client: a localized window with seats, own cards, board, pot and legal actions, plus
   explicit Start Hand and Leave. Closing/disconnect must release the UI and request leave.
3. Gameplay policy: table interaction range, combat restrictions, event continuation,
   localization and optional event outcome branches. Do not freeze a player without cleanup.
4. Full validation: compile client/server/editor together, then use two REAL clients for
   join, full hand, simultaneous actions, timeout, warp, disconnect and leave/rejoin.
5. Presentation/economy: agree the desired TLOPO-inspired variant, create original artwork,
   and implement transactional debit/refund/settlement and crash recovery before real stakes.

API notes: StartHand is explicit and revision-checked; joins never start/reset a hand.
RaiseTo is the TOTAL street wager. Use Call for an all-in call. On stale state, send a fresh
snapshot instead of replaying the old action. Payouts distinguish won pots from refunds;
settlement already updates stacks and clears Pot. The deterministic deck constructor and
custom-stack Join overload remain internal test seams, not network options.
