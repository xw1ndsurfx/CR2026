#nullable enable
using System;
using Intersect.Server.Database.PlayerData;

namespace Intersect.Server.MiniGames.Currency;

/// <summary>
/// Flush the account's existing unsaved state BEFORE an inventory/escrow transaction.
/// The caller holds EntityLock and the real User.Save gate through checkpoint, transfer
/// and live-slot synchronization. The transfer still owns its independent atomic receipt.
/// </summary>
public static class PokerInventoryCheckpoint
{
    public static T Run<T>(Func<UserSaveResult> saveAccount, Func<T> transfer)
    {
        ArgumentNullException.ThrowIfNull(saveAccount);
        ArgumentNullException.ThrowIfNull(transfer);
        if (saveAccount() != UserSaveResult.Completed)
            throw new MoneyRuleException("Account save did not complete. No poker transfer was started; pending refunds are retained.");
        return transfer();
    }
}
