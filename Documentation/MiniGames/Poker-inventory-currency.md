# Poker with inventory currency

This replaces the configuration-only currency milestone. The event's selected item
now backs human buy-ins and cash-outs. Keep the pull request in draft until the
manual acceptance checklist below is complete on a development copy.

## Event settings

Use the same client, server and editor version. In **Start Mini-Game -> Poker**:

- `Table currency / Monnaie`: select the existing Aureons item (or another compatible
  stackable item). The stable item GUID is used; no name or icon is hard-coded.
- `Buy-in (inventory item units)`: for example **100**. This fixed amount is charged
  once when a character takes a seat. The object's Price is irrelevant: one unit
  in inventory equals one unit at the table. Reopening the same active seat does not
  charge again. Insufficient funds prevent admission.
- Small / big blind: for example **5 / 10**.
- `Initial NPC reserve (one-time seed)`: for example **10000** when using the dealer
  and two guests. This is the total treasury for that logical table, not each NPC.
  The game operator is explicitly authorizing creation of that initial house budget.
  Each NPC borrows one buy-in from the treasury and returns its remaining balance.
- Existing automatic hands, card backs, dealing/victory animations and optional
  global human-win announcements remain supported.

Show a normal event question **before** Start Mini-Game, such as:
`Join the poker table for 100 Aureons? Yes / No` and run the command only on Yes.
The command uses the configured fixed buy-in; there is not yet an additional
player-side amount picker or confirmation dialog. Do not use an autorun loop.

Example: a character with 350 Aureons pays 100, leaving 250 in inventory and 100 at
the table. After a session ending at 125, leaving returns 125: final inventory 375.
The maximum possible session loss is the committed table bankroll, not all inventory.
To bring more money after busting, leave and rejoin; no free top-up is provided.

`None / Test chips` keeps the previous free test mode. Test bankrolls never become
inventory items, and a character cannot simultaneously hold a test and funded seat.
All events used to access one table must have identical currency, blinds and other
settings. Keep separate table identifiers for funded and test versions.

## Inventory and recovery

**This implementation supports SQLite player databases only.** MySQL is refused
before a buy-in. It never creates a replacement player database if the file is
missing. One game-server process must own that database; a held `.poker.lock` file
prevents another funded runtime from recovering live seats.

The selected units come from the main inventory only, not bags or bank accounts.
Debit and credit plans use actual InventorySlot rows, with explicit EF change
tracking. The player's EntityLock and the account's real autosave lock remain held
until the database result and live inventory are synchronized.

Buy-ins and cash-outs commit the inventory write and unique transfer receipt in
**one transaction in the same player database**. Every completed hand updates all
funded bankrolls, positive-net-win XP and its hand receipt in one transaction.
Replayed requests/receipts do not credit twice. Unmatched-bet refunds are not wins.

Leaving during a hand preserves the engine's existing rules: a departing seat
checks when free or folds when facing an unpaid bet, and an all-in seat retains
its eligibility. Its remaining bankroll becomes withdrawable only after the hand
settles. Other participants continue playing; closing the UI does not undo a bet.
A disconnected player's refund waits until that character is online again.

Refunds are retried while online, approximately every five seconds. If inventory
space or the configured item's stack capacity is insufficient, the entire claim
remains saved. Nothing is dropped on the ground or silently discarded. Make room
and wait for the refund message. Deleted/incompatible currency definitions also
leave a claim pending until the same item GUID is restored.

A server restart does **not** resume dealt cards or a partial hand. All players in
an unfinished hand recover their **last completed-hand bankrolls**, including their
initial buy-in when no hand completed. A finished, committed result remains final.
This all-participant void rule is deliberate; it is not selective reimbursement
of only a losing player. In-flight gameplay is not reconstructed from a journal.

Storage failures pause affected tables and prevent unconfirmed results from being
announced as wins. If an inventory transaction's commit outcome cannot be verified
at all, the server stops rather than letting autosave duplicate or erase funds.
Repair database access and restart; do not delete financial records to bypass this.

## NPC treasury and XP

A treasury is keyed by map ID + table identifier + currency GUID, **not** the map
instance ID. Different instances borrow from the same budget. The initial positive
seed is created once. Reopening a table, reconnecting, restarting, or raising the
configured seed afterwards does not refill an existing treasury. Zero leaves an
uninitialized treasury unfunded. A depleted treasury can lead to fewer/no NPCs;
two funded human players may still play, subject to the reserved dealer chair.

Players' losses to NPCs replenish their bankrolls; when those NPCs leave, their
remaining balances return to the house. There is no automatic unlimited issuance.
Changing a map/table identifier defines a NEW treasury, so only trusted editors
should configure funded tables. A treasury replenishment administration UI and
advanced anti-collusion/anti-farming policy are not included in this version.

Funded poker progress starts separately from free test progress. 25 XP is awarded
for a hand with positive net gain, once per character/hand. Levels 1-25 and B1-B6
unlocks are unchanged. Both funded modes using different item currencies share the
character's funded poker skill. Old test XP and test backs are not imported.

The green XP bar remains active; no new art is required for inventory accounting.

## Database and backups

Financial tables are stored in the **actual player database**, normally
`resources/playerdata.db`, honoring the configured resource directory:

`PokerMoneyHouses`, `PokerMoneySeats`, `PokerMoneyTransfers`, `PokerMoneyHands`,
`PokerMoneyProfiles` and their indexes are created automatically when needed.
The existing account/character/inventory schema is not replaced. Do not separately
copy or reset only these tables: inventory and financial records form one unit.

Stop the development server and back up its entire resources/data directory before
updating. Keep `minigames-test.db` too; it still owns the separate free-test profile.
Do not apply an older player-database backup without also considering all transfers
since that backup. Receipts are retained; archival tooling is not provided yet.

## Validation and manual acceptance

The automated suites exercise the actual poker engine with funded bankrolls,
SQLite transaction faults, repeated receipts, cross-currency/character rejection,
finite NPC loans, restarted checkpoints, deferred refunds, actual PlayerContext
and InventorySlot writes with auto-detection disabled, and funded network/UI fields.
They are not a substitute for a live session with the real game assets.

Before merging or enabling on the live game, use a BACKUP/COPY and verify:
1. 350 -> buy-in 100 -> inventory 250, reopening the same table does not charge twice.
2. Two humans finish hands and cash out; total money is conserved without NPCs.
3. Dealer/NPC stakes use the configured finite treasury and stop refilling when empty.
4. Full inventory, disconnect, reconnect and a server restart preserve refunds and
   completed-hand results; an unfinished hand follows the all-player void rule.
5. Global announcements name the selected currency and show NET winnings; only the
   winning human receives the selected victory animation.
6. Funded XP, B1-B6 permissions and the green bar persist independently of test XP.

## Update commands

Save local work; close server, clients and editor before replacing binaries.

```powershell
git fetch origin
git switch feature/poker-minigame-core
git pull --ff-only origin feature/poker-minigame-core
git submodule update --init --recursive

dotnet build Intersect.Server/Intersect.Server.csproj --configuration Debug
dotnet build Intersect.Client/Intersect.Client.csproj --configuration Debug
dotnet build Intersect.Editor/Intersect.Editor.csproj --configuration Debug
```

Use the corresponding Debug applications and ALL companion DLLs. These commands
do not replace a different Release/publish directory. Do not mix protocol versions.
