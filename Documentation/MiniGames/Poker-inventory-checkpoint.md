# Poker inventory checkpoints

This note supplements `Poker-inventory-currency.md`. Inventory-backed poker uses
an account checkpoint before every new buy-in or pending cash-out attempt.

## Why the checkpoint matters

An online player can move or split a currency stack between regular autosaves.
Saving only the slot changed by poker would then be insufficient. For example:

- Saved inventory: slot A has 350 Aureons, slot B is empty.
- Live inventory after a move: A is empty, B has 350.
- A 100-unit buy-in must not save B=250 while leaving the old saved A=350.

Under the player's EntityLock and the actual User.Save lock, the bridge first
calls `User.Save(force: true)` to persist the account's existing live state.
Only `UserSaveResult.Completed` permits the poker transfer. Skipped, failed or
throwing saves stop admission or retain the cash-out claim.

The subsequent debit/credit and its escrow receipt still commit in one SQLite
transaction. A crash between the account checkpoint and that transaction simply
leaves the latest normal inventory save and no new poker transfer. A transaction
rollback does not undo the earlier legitimate stack move.

Both locks stay held until the transaction result has been resolved and the live
slots have been synchronized. A sweep with no pending refund does not force an
account save. This uses the existing engine account-save behavior; it does not
redesign unrelated cross-account trading or guild-bank transaction semantics.

## Regression coverage

The real PlayerContext/InventorySlot fixture now covers moved stacks before
buy-in and before cash-out, a failed debit after a successful checkpoint,
checkpoint exceptions, every non-success UserSaveResult, and refund retention
when the checkpoint cannot be completed. It reopens the SQLite ledger and checks
inventory plus pending claims, not only the balance of the destination slot.

The fixture omits authentication and saves its mapped inventory snapshot in the
checkpoint callback. The production callback is the real account Save method,
whose account graph is broader than the fixture. Manual acceptance must still
include a normal logged-in character moving stacks before joining and leaving.

Run with the other funded tests:

```powershell
dotnet run --project Utilities/PokerInventoryTests/Intersect.PokerInventoryTests.csproj --configuration Release
```

All financial data and inventory remain in the same player database. Stop the
server and back up that entire database and resources before the first funded
play test. SQLite is supported; MySQL is not enabled by this implementation.
