#nullable enable
using Intersect.Server.Database;
using Intersect.Server.Database.PlayerData;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Intersect.Server.MiniGames.Currency;

/// <summary>Uses the real player model but never attaches the live player's entity graph.</summary>
public static class PokerInventoryWriter
{
    public static void Write(PlayerContext context, SqliteConnection connection, SqliteTransaction transaction,
        Guid player, IReadOnlyDictionary<Guid, Item> values)
    {
        if (player == Guid.Empty || context.IsReadOnly) throw new MoneyRuleException("Invalid inventory write context.");
        context.Database.SetDbConnection(connection, contextOwnsConnection: false);
        using var enlistment = context.Database.UseTransaction(transaction);
        foreach (var change in values)
        {
            var persisted = context.Player_Items.IgnoreAutoIncludes().AsTracking()
                .SingleOrDefault(s => s.Id == change.Key && s.PlayerId == player)
                ?? throw new MoneyRuleException("An inventory slot is not saved yet; retry after the next save.");
            persisted.Set(change.Value);
        }
        // Engine contexts normally disable automatic detection. Never rely on SaveChanges
        // discovering these changes implicitly: an escrow must not commit without its debit.
        context.ChangeTracker.DetectChanges();
        context.SaveChanges();
        // The ledger, not this context, owns the outer transaction and commit.
    }
}
