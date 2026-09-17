# Event-launched mini-games

## Delivery status: foundation only

This first increment adds the serializable `StartMiniGameCommand` contract,
a stable `MiniGameType.Poker` identifier, poker table options, validation methods,
and regression tests. It is NOT a playable poker implementation.

The command is not yet connected to the editor command picker, the server event
processor, network packets, or a client window. Do not manually insert this
command into production event data until those integrations are delivered.
Validation methods exist, but do not enforce anything at runtime until callers
are wired to them. No running tables, cards, betting, or balances are created.

## Configuration contract

`StartMiniGameCommand` has a game type, an explicit nonempty `TableId`, and
separate `PokerTableOptions`. New mini-games should have their own option types
and explicitly assigned `MiniGameType` values; do not reinterpret poker fields.

Initial play-chip defaults (development choices, not final economy rules):

| Option | Default | Foundation validation |
| --- | ---: | --- |
| MaxPlayers | 6 | 2 through 6 |
| StartingChips | 1000 | 1 through 1,000,000 and at least BigBlind |
| SmallBlind | 10 | Positive and below BigBlind |
| BigBlind | 20 | Above SmallBlind and no more than StartingChips |
| TurnTimeoutSeconds | 30 | 5 through 300 |

These are play chips only. Nothing reads or changes Aureons, Royal Diamonds,
Corpsy Coins, items, purchases, or any persistent balance.

Generate the table ID explicitly in the future editor form. Preserve that ID
when saving, loading, or copying a command; never generate it during loading.
The planned server lookup key is `(map-instance identity, TableId)`, so matching
events can share a table without merging different map instances. Reject
inconsistent options for an existing table instead of changing a live table.

## Compatibility and tests

`StartMiniGame` is appended to `EventCommandType`; all existing numeric values
remain unchanged. Regression test cases cover each existing command value.

Nonzero defaults use `DefaultValue` attributes for event-copy JSON settings.
An omitted poker-options object keeps the initialized defaults; an explicit
null options object fails validation. Tests cover default/custom event copies,
invalid options, boundary values, and an existing shop command round trip.

Build and NUnit execution have NOT been run in the authoring environment:
no .NET SDK is installed there. Static source checks are not a substitute for
compilation or the actual NUnit tests. Keep the pull request in draft until
verification succeeds.

From a development checkout, with the repository's .NET 8 SDK requirements
and dependencies installed:

```powershell
git submodule update --init --recursive
dotnet test Intersect.Tests/Intersect.Tests.csproj -c Debug --filter FullyQualifiedName~StartMiniGameCommandTests
dotnet build Intersect.sln -c Debug
```

For the full solution, use the Windows development environment required by
the editor. Keep production server data outside the test checkout.

## Next increments and acceptance gates

1. **Editor and server event integration.** Add the Start Mini-Game picker and
   form, validation feedback, command descriptions, and a server dispatcher.
   Save, close, reopen, and copy an event without losing its options. Reject
   invalid/unsupported commands without crashing or blocking the event.
2. **Shared table lobby.** Add server-owned membership and client open/state/leave
   packets with a basic window. Two clients in the same instance and table join
   one lobby; different tables/instances stay separate. Enforce capacity, clean
   disconnect/map-change handling, and reject unauthenticated or nonmember actions.
3. **Playable poker.** Add server-owned deck, private hands, community cards,
   turn order, Check/Call/Raise/Fold/Leave, blinds, pots, all-ins, side pots, ties,
   payout, and turn timeouts. Do not expose opponents' private cards. Validate
   chip conservation, invalid/replayed actions, and multiplayer disconnect cases.
4. **Polish and optional economy.** Improve the visual table and localization.
   Any persistent currency or inventory integration needs an explicit design,
   atomic debit/refund rules, and separate tests before enabling it.

Each increment should be reviewable and tested before merging into `main`.
