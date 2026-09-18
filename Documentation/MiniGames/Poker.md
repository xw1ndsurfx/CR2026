# Poker mini-game - milestone 3: network and client window

## Status and limits

Development branch only. The event command now opens a Poker window backed by the shared
server table. The implementation includes Start Hand, Fold, Check, Call, Raise/Bet, All-in,
Refresh and Leave, six seat displays, private cards, community cards, pot, stacks and results.
The UI is an original, asset-free prototype using the existing Gwen controls and ASCII card
labels (AC, TD, KH, etc.). It is NOT the finished TLOPO presentation or a verified reproduction
of that game's rules. No TLOPO assets/code have been copied.

All balances are temporary TEST CHIPS. No Aureons, Royal Diamonds, inventory items, real
money or rewards are involved. Leaving finally discards that seat's balance; rejoining can
intentionally grant a fresh test stack. Restarting loses every table. Never attach these
balances to the persistent economy without transactions, replay-safe accounting and recovery.

Compilation/tests must be checked for the exact commit. No visual/manual two-client playtest
has been performed in the authoring environment. This PR remains a draft; do not deploy to
production or claim live multiplayer validation based only on the automated checks.

## Configure and try on a development server

Build the CLIENT, SERVER AND EDITOR from the SAME branch/commit. New built-in packets are
registered by the engine; old binaries must not be mixed with this protocol version.
Back up game data before connecting a development editor to a test server.

1. In an event, select `Mini-Games > Start Mini-Game...`.
2. Select Poker, Table ID `tavern-1`, 2-6 seats, 1000 chips, blinds 5/10, turn timeout 30 seconds.
3. Save the command/event. Use an interaction/action-button trigger, NOT an autorun loop.
4. Start two updated clients with different characters; interact with the event on the same
   map and instance. Both should see the same seats/pot and different private cards.
5. A funded seated player presses Start hand. Joining alone does not start/reset a hand.
6. Raise amount means the TOTAL wager on the current street, not the additional amount.
   Minimum selects the minimum legal raise or the short stack's all-in amount.
7. Leave table, the window's X, or Escape closes the local window and requests departure.
   `Mini-Games > Leave Mini-Game` also closes it from an event.

Same map + same map instance + same case-sensitive Table ID = same table, even through
separate event objects. Different maps/instances are isolated. All events referring to one
table must have identical settings. IDs accept 1-64 ASCII letters, digits, hyphens/underscores.
Every event activation rotates the UI view token but never duplicates a seat or resets chips.
Use a fresh activation after reconnect; a departing active-hand seat remains reserved until
settlement. A zero-stack player may leave/rejoin for a fresh TEST stack.

Commands remain non-blocking and have no outcome branches. They neither freeze movement nor
wait for the Poker window to close. Do not add Hold Player without explicit release logic.
This milestone enforces map/instance presence, not a physical table radius or combat policy.
Place test tables in a safe area. Those gameplay restrictions are remaining acceptance work.

## Networking and privacy

The packet types live in `Framework/Intersect.Framework.Core/Network/Packets`:

- `Client/PokerRequestPacket`: view ID, table-instance ID, request ID, hand/revision, action
  and optional total wager. No player identity, table settings, balances or client time.
- `Server/PokerStatePacket`: recipient identity, view ID, monotonic delivery sequence,
  request acknowledgement, server time, detached table state or a closed marker.
- `MiniGames/PokerProtocol`: explicit wire enum values and bounded DTO shapes.

Only an authenticated event can create/join a table. Requests derive identity from the
connection's Player and validate login, location, table, view token, request ID, revision and
turn. A per-view guard permits at most eight accepted requests per second and rejects repeats.
An old Leave packet cannot close a replacement window. Poker rejection returns fresh state;
malformed, unknown-view and throttled packets are dropped without executing an action.

`PokerRuntime` broadcasts a SEPARATE projection per recipient after table changes. Only that
recipient's private cards are present. Opponents' cards are empty until a contested showdown;
a fold winner does not reveal. The PokerTable object, deck and internal deterministic test
seams are never serialized. Transport projection copies arrays rather than exposing state.

Presence/timer sweeps run every 250 ms after first use. Disconnect, replacement login,
map/instance changes, death and disposal trigger leave; active stakes remain until settlement,
including all-in eligibility. Empty tables and their view contexts are removed. Sends occur
outside runtime/registry locks to the captured connection, not a newly resolved login.

The client enqueues packets in a bounded 64-entry inbox and only updates Gwen on the UI
thread. Delivery sequence checks prevent visual rollback. Late acknowledgements may release
a matching pending request without replacing a newer snapshot. A pending action blocks double
clicks. Missing replies cause a Refresh after five seconds, NEVER automatic retransmission
of a bet. Idle windows refresh after ten seconds. Countdown display uses server time plus
monotonic elapsed time; the server remains authoritative on expiry.

## Window and localization

`PokerWindow` requires no new graphics files. Its contents scroll at smaller resolutions.
It shows empty seats, dealer/acting markers, stacks and street bets, board, own cards, available
actions and grouped winnings. Card labels use ranks 2-9/T/J/Q/K/A and suits C/D/H/S. Refunds
are not labeled as winnings. Longer names/result summaries are shortened to fit the prototype.

The `Poker` section of `client_strings.json` is added through the existing localization
loader; default labels are English. A French section is supplied in `poker-client-fr.json`:
merge that section into an existing client strings file, never replace the entire file with it.
The event-editor labels and join-failure chat messages remain English development text.

## Automated checks

With .NET 8, from the repository root:

```sh
git submodule update --init --recursive
dotnet run --project Utilities/PokerSmokeTests/Intersect.PokerSmokeTests.csproj --configuration Release
dotnet run --project Utilities/PokerNetworkTests/Intersect.PokerNetworkTests.csproj --configuration Release
```

The original suite has 17 core groups (250 simulated hands) and 18 registry groups. The new
suite has 14 protocol/client-model groups: discovery, MessagePack round trips, privacy, DTO
validation, projection copies, request/view/rate guards, reordered acknowledgements, dismissal,
refresh recovery, clocks, and a full hand through two serialized client models. It references
the actual framework DTOs and links the actual core/registry/transport/client-model sources.
This is an in-process simulation, NOT two running game clients or a socket/visual test.

On Windows:

```sh
dotnet build Intersect.Server/Intersect.Server.csproj --configuration Debug
dotnet build Intersect.Client/Intersect.Client.csproj --configuration Debug
dotnet build Intersect.Editor/Intersect.Editor.csproj --configuration Debug
```

The integration workflow builds those real projects plus Server.Core and runs both suites.
It uses read-only repository permissions and does not deploy, publish or merge anything.
Read its result for the exact commit: added tests alone are not evidence of success.

## Remaining acceptance gates

Before any merge or production release, perform a REAL two-client test of opening the event,
private cards, a full hand, simultaneous/stale clicks, raises and all-ins, side pots, timeout,
late joining, leave/X/Escape, disconnection, map warp, death, reconnect, small resolutions,
keyboard focus and client-string localization. Test server shutdown while a hand is active.

Then decide table radius/combat restrictions, optional event outcomes, persistence policy,
the exact TLOPO-inspired variant and original artwork/animations. Persistent stakes require a
transactional ledger with crash recovery and abuse tests, not just replacing the test balance.
