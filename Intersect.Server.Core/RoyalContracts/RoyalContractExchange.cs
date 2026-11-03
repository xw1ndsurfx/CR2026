using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.RoyalContracts;
using Intersect.Server.Database;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.Entities;

namespace Intersect.Server.RoyalContracts;

internal enum RoyalContractExchangeResult { Success, InventoryFull, SaveFailed }

internal static class RoyalContractExchange
{
    // Caller owns EntityLock and the account's save gate. Inventory, receipt, seals
    // and community contribution are committed in the same player-database save.
    internal static RoyalContractExchangeResult Commit(Player player, RoyalContract contract, string profileBefore,
        Guid itemId, int delta, Func<bool> save)
    {
        var plan = new List<(InventorySlot Slot, Item Before, Item After)>();
        var variableId = RoyalContractRules.ContributionId(contract.Id);
        var existing = player.GetVariable(variableId);
        var contributionBefore = existing?.Value.Integer ?? 0;
        var left = Math.Abs(delta);
        if (left > 0)
        {
            if (ItemDescriptor.Get(itemId) is not { IsStackable: true, MaxInventoryStack: > 0 } descriptor) return Rollback(RoyalContractExchangeResult.SaveFailed);
            // Native inventories are sparse: enumeration alone omits unused slots.
            // Resolve the legal empty slots before constructing a reward plan.
            if (delta > 0) _ = player.FindOpenInventorySlots();
            foreach (var slot in player.Items.OrderBy(s => s.ItemId == itemId ? 0 : 1))
            {
                if (left == 0) break;
                if (slot.BagId.GetValueOrDefault() != Guid.Empty || slot.Quantity < 0 ||
                    (delta < 0 ? slot.ItemId != itemId : slot.ItemId != Guid.Empty && slot.ItemId != itemId)) continue;
                var quantity = Math.Min(left, delta < 0 ? slot.Quantity : descriptor.MaxInventoryStack - (slot.ItemId == Guid.Empty ? 0 : slot.Quantity));
                if (quantity <= 0) continue;
                var after = slot.ItemId == Guid.Empty ? new Item(itemId, quantity) : slot.Clone();
                if (slot.ItemId != Guid.Empty) after.Quantity += delta < 0 ? -quantity : quantity;
                if (after.Quantity == 0) after = Item.None;
                plan.Add((slot, slot.Clone(), after)); left -= quantity;
            }
            if (left != 0) return Rollback(RoyalContractExchangeResult.InventoryFull);
        }
        foreach (var change in plan) change.Slot.Set(change.After);
        if (contract.Template.Collective)
            RoyalContractsRuntime.SetContribution(player, contract.Id, player.PetCollection.RoyalContracts.Contracts[contract.Id].Quantity);
        try
        {
            if (save()) return RoyalContractExchangeResult.Success;
            return Rollback(RoyalContractExchangeResult.SaveFailed);
        }
        catch { Rollback(RoyalContractExchangeResult.SaveFailed); throw; }

        RoyalContractExchangeResult Rollback(RoyalContractExchangeResult result)
        {
            player.PetCollection.RoyalContracts = RoyalContractRules.Read<RoyalContractProfile>(profileBefore);
            foreach (var change in plan) change.Slot.Set(change.Before);
            var variable = player.GetVariable(variableId);
            if (existing == null && variable != null) player.Variables.Remove(variable);
            else if (existing != null) existing.Value.Integer = contributionBefore;
            return result;
        }
    }
}
