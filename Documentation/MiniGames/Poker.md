# Poker mini-game - milestone 1: server core

## Status

This is a development milestone, NOT a playable client feature. No existing event,
network handler, inventory, currency, database schema, or client window is changed.
There is no Start Mini-Game command in the editor yet.

Implemented in `Intersect.Server/MiniGames/Poker`:

- A lock-protected 2-6-seat no-limit Texas hold'em state machine.
- Cryptographic deck shuffle; best-five-of-seven evaluation, wheel straights and kickers.
- Heads-up blind/action order, four betting rounds and the big-blind option.
- Fold, check, call and raise-to, including all-ins and short-raise reopening rules.
- Main/side pots, unmatched-chip refunds, ties and clockwise odd-chip allocation.
- Per-recipient defensive snapshots; opponents' hole cards stay out of snapshots until
  a contested showdown. A fold win does not reveal the winner's cards.
- Hand/revision validation, action deadlines, late joining, and departure auto-check/fold.

All chips are temporary TEST CHIPS. Leaving removes that seat's balance; restarting the
server would lose all tables. There is no conversion to Aureons, Royal Diamonds, items,
real money, or rewards. Do not connect the current implementation to a persistent economy.
This is original code, not copied game code/assets or a verified reproduction of TLOPO's
specific poker variant, payouts, presentation, or progression.

## Run the independent checks

From the repository root, with the .NET 8 SDK:

```sh
dotnet run --project Utilities/PokerSmokeTests/Intersect.PokerSmokeTests.csproj --configuration Release
```

The executable links the actual core source files and has no external NuGet test packages,
server database, game assets, Windows editor, or network-key dependency. Its local
`Directory.Build.props` deliberately isolates it from the engine's build customizations.
A nonzero exit code means failure. The 17 named test groups include 250 deterministic
simulated hands checking termination, chip conservation and card privacy.

The `Poker core smoke tests` workflow runs the same command for this branch and relevant PRs.
The authoring environment had no .NET compiler: added tests are not by themselves evidence
of passing results. Read the workflow result, or run the command above, before merging.
A successful standalone check is NOT a full-engine build or a live multiplayer test.

## API contract for the next milestone

Create ONE `PokerTable` per table identity on the SERVER, not one table per player:

```csharp
var table = new PokerTable(new PokerRules(MaxPlayers: 6));
table.Join(authenticatedPlayerId, playerName);
var snapshot = table.Snapshot(authenticatedPlayerId);
// Broadcast a SEPARATE snapshot to each seated recipient.
```

`StartHand` is a separate, explicit operation available to a seated player when at least two
funded players are present. No hand starts or resets just because a player joins.
`RaiseTo` specifies the TOTAL street wager, not the added amount. An all-in call uses `Call`;
an all-in raise uses `RaiseTo` with `MaximumRaiseTo`. `CanRaise` is recipient-specific;
`MinimumRaiseTo` can exceed a short stack's maximum, in which case only that all-in raise
is legal. Fold/check/call ignore the amount parameter.

`Act` must receive the hand ID and revision from the recipient's latest snapshot. On a
stale/invalid action, send a fresh snapshot rather than retrying the old command blindly.
A timeout encountered during `Act` may advance the table and return `StaleState`.

Call `Tick(serverTime)` regularly, including when no packets arrive. Use only server-owned
time and authenticated player identity; neither may come from client-supplied payloads.
No user-facing endpoint is provided by this milestone.

`Leave` during a hand retains a participant's seat until settlement, and auto-checks or
folds when action reaches them. An all-in player remains eligible. Keep the membership
reserved during that period, then remove it after settlement. New arrivals wait for the
next hand. A zero-stack seat can explicitly leave and rejoin for fresh TEST chips.

Snapshots contain only detached arrays/records. The table object, deck, deterministic
constructor, and custom-stack Join overload must never be exposed to clients or serialized.
The latter two are internal test seams only. `Payouts` lists each pot/refund separately;
`IsRefund` distinguishes returned uncalled chips from a won pot. Sum entries as needed.
At settlement, `Pot` becomes zero and stacks already include the payouts.

## Remaining milestones / acceptance gates

1. Event + server integration: append a serialized `StartMiniGame` command without changing
   existing enum values; add its editor configuration, type mapping and execution handler.
   Add an authenticated session registry keyed by (map ID, instance ID, table key), enforce
   one membership per player, map/range/combat rules, disconnect/warp cleanup and regular
   ticks. Define event blocking/release and success/cancel branches explicitly.
2. Network + client: register action and recipient-state packets, handle resync/reconnect,
   rate-limit actions, add a localized Poker window with seats, board, own cards and controls.
   Never send a shared snapshot containing everybody's private cards.
3. Full validation: build client/server/editor together and run two REAL clients through
   joining, a full hand, simultaneous actions, timeout, disconnect and leave/rejoin.
4. Presentation and economy: agree on the desired TLOPO-inspired variant and original
   artwork/animations. Persistent stakes need transactional debit/refund/settlement,
   crash recovery, replay-safe ledger entries and abuse tests before any game currency.

Do not merge/deploy this milestone as if the complete mini-game were available to players.
