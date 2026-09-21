# Blackjack for CR2026

Blackjack is added alongside Poker in `Start Mini-Game`. Its dedicated development
branch is `feature/blackjack-part6-sync`, rebuilt on the completed Poker Part 11/11 baseline.
It does not replace Poker and is not an automatic deployment to a live server.

## Event configuration

Create an interaction event, show a Yes/No buy-in confirmation, and put
`Mini-Games -> Start Mini-Game` in the Yes branch. Example:

| Setting | Example |
| --- | --- |
| Mini-game | Blackjack - versus dealer |
| Table ID | blackjack-aureons-1 |
| Maximum seats (includes dealer) | 6 |
| Table currency / Monnaie | existing Aureons item |
| Buy-in (inventory item units) | 100 |
| Blackjack minimum bet | 10 |
| Blackjack maximum bet | 100 |
| Betting / turn timeout | 30 seconds |
| Other NPC opponents | 2 |
| Automatic rounds | enabled |
| Initial house / NPC reserve | 50000 |
| Hit soft 17 | unchecked (S17) |

The dealer is always present in Blackjack. Six seats means the dealer plus up to
five gamblers (humans and guests combined), with at least one place reserved for
a human. The Poker blind/dealer settings are disabled for Blackjack and do not
change its rules. Identical game, map instance and table ID share a table; all
access events must use identical settings. Poker and Blackjack with the same
textual table ID remain separate games and separate house reserves.

The buy-in is a fixed amount configured in the event, not the initial wager of
each round. The player selects an even wager inside the displayed limit during
the betting phase and presses Place bet. Players who do not bet sit out that
round. No human wager means the round is cancelled without NPC-only XP farming.
New human participants can sit out a round already in progress and join the
next. A guest can free a full table's seat between rounds, never mid-hand.

Use a confirmation such as `Join Blackjack with 100 Aureons?`. There is no second
native buy-in confirmation or variable deposit slider. Do not trigger admission
in an endless autorun event. `Leave Mini-Game` also works for Blackjack.

## Rules chosen for this implementation

These are CR2026 rules, not a claim to reproduce every TLOPO rule:

- Six physical decks, shuffled by the server for each new round. This is not a
  persistent multi-round shoe. Equal card images can occur legitimately.
- Every player and NPC guest competes independently against the same dealer,
  not against the other gamblers for a shared poker pot. Player cards are face-up.
- Natural blackjack (ace plus a ten-valued card) pays 3:2 net. Other wins pay 1:1.
  An equal score pushes. A bust loses even if the dealer also busts.
- Initial wagers must be even integers so a 3:2 payoff never creates fractional
  inventory items. Doubles and split bets are paid from the table balance.
- The dealer has one up-card and one hidden hole card, checked for a natural
  before player decisions. No hidden card or hidden-derived total is broadcast.
- Hit and Stand; Double on the first two cards, including after a split; one
  split into two hands for the same rank. Different ten-valued ranks do not split.
- Split aces receive one card per hand and stop. A split-hand 21 pays 1:1, not 3:2.
- S17 by default, configurable H17. No insurance, surrender, re-splitting, or side
  bets are included.
- A timed-out or departing player stands on unfinished dealt hands. Leaving
  does not selectively cancel a losing hand. An unstarted betting-phase wager
  can be cancelled. Remaining outcomes settle normally.

The server reserves up to four times each initial wager from its dealer bankroll
before accepting it, covering one split and a double on both hands. This is
conservative: a low dealer bankroll can lower the displayed maximum bet even
when the event allows a higher limit.

## Inventory-backed play and finite house budget

The same transactional inventory bridge and ONE player SQLite database ledger
are shared by Poker and Blackjack. There is no second database lease or second
recovery owner. MySQL is not enabled. One character cannot simultaneously own
Poker and Blackjack seats, in test mode or funded mode.

Example: inventory 350 -> buy-in 100 -> inventory 250, table balance 100.
A natural on a 10-unit wager earns 15 net: table balance 115. Leaving returns 115
and the inventory becomes 365. This is an illustrative outcome, not a forced deal.
Item Price is irrelevant: one item unit equals one table unit. Bank/bag items
are not debited. Reopening the same active seat does not charge another buy-in.

The initial positive house reserve is a designer-authorized creation of budget,
seeded once per `blackjack + map + table ID + currency`, shared across instances.
The example 50000 leaves enough reserve for both the dealer bankroll and guests.
The dealer initially borrows up to `max(10000, maxBet * 4 * playerCapacity)` from
that reserve, limited to available funds. Guest stakes also borrow from the
remaining reserve. With a very small initial reserve the dealer may take all of
it and no guest can join. Solo play still works against a funded dealer.

Reopening/restarting does not reseed an existing reserve, nor does increasing the
configured seed later. NPCs return their balance on leaving. The dealer's seated
bankroll is not topped up automatically: when it is too low, stop the session
and reopen to borrow any remaining available house funds. An exhausted reserve
cannot create new funds. An administrative replenishment UI is not included.
Only trusted designers should create/edit event IDs and seeded budgets.

## Commit, refund and crash semantics

Before each buy-in or pending refund the existing account save checkpoints prior
live inventory moves under EntityLock and the account save gate. The transfer
and its escrow receipt then commit in the same SQLite transaction. Blackjack
uses the same tested writer and receipt replay handling as Poker.

At round completion, the dealer, guests, and human balances, the unique round
receipt and profitable humans' Blackjack XP commit together. While storage is
unavailable, the affected table stops accepting decisions/next rounds and retries.
A retry does not grant duplicate XP or change an already committed result.

Leaving a dealt round waits for settlement. A disconnected character's pending
refund is retried after they are online. The existing sweep retries about every
five seconds. Full inventory or a missing currency definition retains the claim;
nothing is discarded on the ground. Recovery checks both runtime memberships
before treating an active escrow as orphaned.

A server restart voids a wholly unfinished round for all participants and returns
the last committed table balance (or the original buy-in before the first round).
It does not reconstruct and resume a partially dealt round. Closing a client is
not a server restart and cannot use this rule to erase an individual loss.

Financial data use the existing `PokerMoney*` tables in the real player database.
Do not delete only these tables to reset Blackjack: they back real debited items.
Back up the entire player database with the server stopped. Run one server
process per player database; the shared file lease enforces one ledger owner.

## Progression, announcements and art

Blackjack has its own character/game progression: 25 XP per completed round with
positive combined net result across the player's one or two hands. A split win
and loss which net to zero gives no victory XP. Levels 1-25 and the green bar use
the same curve as Poker, but earning Blackjack XP never changes Poker or RPG XP.

B1-B6 unlock at levels 1/5/10/15/20/25 in Blackjack independently. Selected backs
persist, and changes during a round apply next round. Test Blackjack uses the
`blackjack` key in minigames-test.db; funded Blackjack uses Game='blackjack' in
the player database. Test progress is not imported into the funded game.

Reuse existing `resources/misc/AC.png`, `TD.png`, etc., and `B1.png` through
`B6.png`. No new mandatory images are required. Optional existing Poker portraits
represent Marlow, the named guests, and a generic human. These are still static
portraits, not map-seated NPC entities or personal player paperdolls.

The borderless green-felt scene keeps the local human at the bottom. It shows
the dealer's visible total, turn indicator, decisions, split hands, wagers,
results, bankroll and green XP progress. The global chat optionally announces
positive NET human wins using the selected item name; NPC results stay local.
Existing event animations still support the configured deal and winner effects.
Blackjack also uses the Poker Part 11/11 procedural motion settings: Off, Fast,
Normal or Cinematic, with authoritative transitions for initial dealing, Hit,
Double/Split chip movement, dealer reveal/draw, payouts, shuffle and the local
winner pulse. The first snapshot after opening or reconnecting always snaps to
the authoritative final positions, and Refresh never replays historical motion.
The winner's scene must still be open to show local visual effects.

## Checks and manual acceptance

Automated coverage includes deterministic rules and randomized conservation;
shared real SQLite escrow with synthetic inventory callbacks; protocol/envelope
round trips and hole-card privacy; independent profiles and receipts; actual
Windows event dialog; actual Gwen controls with synthetic font/drawing metrics.
The inherited real PlayerContext/InventorySlot suite remains enabled, as do the
Poker regressions. Synthetic drawing checks are not a GPU screenshot or a real
logged-in multiplayer acceptance session. No production deployment is implied.

After backing up a development copy, test with matching server/client/editor:

1. Choose Blackjack with Aureons, buy-in 100, even minimum 10, reserve 50000.
2. Confirm inventory debit, play alone with Marlow and guests, then cash out.
3. Join two human clients and verify the same dealer, independent wagers/turns,
   Hit/Stand/Double/Split availability, and one shared final dealer hand.
4. Check positive-net XP and global announcement, green progress, B1 accessible
   and higher backs locked; verify Poker XP is unchanged.
5. Quit/disconnect during a hand; do not allow an immediate refund of a dealt
   wager. Check delayed refund, full inventory claim, and restart on a COPY.
6. Reconnect and verify saved funded/test Blackjack progression stays separate.

Useful commands from the repository root:

```powershell
git fetch origin
git switch feature/blackjack-part6-sync
git pull --ff-only origin feature/blackjack-part6-sync
git submodule update --init --recursive

dotnet build Intersect.Server/Intersect.Server.csproj --configuration Debug
dotnet build Intersect.Client/Intersect.Client.csproj --configuration Debug
dotnet build Intersect.Editor/Intersect.Editor.csproj --configuration Debug

dotnet run --project Utilities/BlackjackTests/Intersect.BlackjackTests.csproj --configuration Release
dotnet run --project Utilities/BlackjackIntegrationTests/Intersect.BlackjackIntegrationTests.csproj --configuration Release
dotnet run --project Utilities/BlackjackEditorTests/Intersect.BlackjackEditorTests.csproj --configuration Release
```

Use these Debug applications and all their matching DLLs, not old executables
from a Release/publish folder. The branch includes the previous Poker changes;
no merge of either draft PR into main is required for a development test.


## Part 8/8 release gate

Blackjack is ready to merge only when the dedicated `Blackjack production validation`
workflow is green. That gate requires:

- Blackjack engine rules tests.
- Blackjack ledger, wire protocol and editor integration tests.
- Real Gwen/UI regression coverage, including reconnect/Refresh motion behavior.
- Real Server Core, Server, Client and Editor builds.
- Poker editor, inventory and progression regression suites.

Before merging to `main`, also perform the manual acceptance steps above on matching
Debug builds from the same commit. Use a COPY of the player database for restart and
disconnect testing. Do not treat a green automated gate as proof of a real two-client
multiplayer session; the manual two-client check remains required.

Recommended final sequence:

```powershell
git fetch origin
git switch feature/blackjack-part8-release-gate
git pull --ff-only origin feature/blackjack-part8-release-gate
git submodule update --init --recursive

dotnet build Intersect.Server/Intersect.Server.csproj --configuration Debug
dotnet build Intersect.Client/Intersect.Client.csproj --configuration Debug
dotnet build Intersect.Editor/Intersect.Editor.csproj --configuration Debug

dotnet run --project Utilities/BlackjackTests/Intersect.BlackjackTests.csproj --configuration Release
dotnet run --project Utilities/BlackjackIntegrationTests/Intersect.BlackjackIntegrationTests.csproj --configuration Release
dotnet run --project Utilities/BlackjackEditorTests/Intersect.BlackjackEditorTests.csproj --configuration Release
dotnet run --project Utilities/PokerUiTests/Intersect.PokerUiTests.csproj --configuration Release
```
