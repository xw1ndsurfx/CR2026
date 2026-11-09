using Intersect.Core;
using Intersect.Framework.Core.Fishing;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.MiniGames;
using Intersect.Framework.Core.Professions;
using Intersect.Framework.Core.WorldEvents;
using Intersect.Server.Database;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.Entities;
using Intersect.Server.Professions;
using Microsoft.Extensions.Logging;

namespace Intersect.Server.Fishing;

internal enum FishingRewardResult { Success, AlreadyCommitted, InventoryFull, MissingBait, SaveFailed }

internal sealed class FishingProgressAward
{
    internal ProfessionDefinition? Profession;
    internal Guid VariableId;
    internal long Before, After;
    internal int PreviousLevel, NewLevel;
    internal long RawAfter => Profession == null ? After : After + 1;
    internal long Gained => Math.Max(0, After - Before);
}

internal static class FishingRewards
{
    internal static FishingProgressAward Progress(Player player, FishingConfiguration config, int experience = 0)
    {
        var profession = FishingRuntime.Profession(config);
        var variableId = profession?.Id ?? FishingRules.FallbackExperienceId;
        var raw = Math.Max(0, player.GetVariable(variableId)?.Value.Integer ?? 0);
        var before = profession?.ClampExperience(Math.Max(0, raw - 1)) ?? Math.Clamp(raw, 0, MiniGameProgression.MaximumExperience);
        var bonus = WorldActivitiesConfiguration.AwardBonus(experience, player.ActivityExperienceBonus);
        var proposed = (long)Math.Min(long.MaxValue - 1, (decimal)before + Math.Max(0, experience) + bonus);
        var after = profession?.ClampExperience(proposed) ?? Math.Min(proposed, MiniGameProgression.MaximumExperience);
        return new()
        {
            Profession = profession, VariableId = variableId, Before = before, After = after,
            PreviousLevel = profession?.LevelForExperience(before) ?? MiniGameProgression.Level(before),
            NewLevel = profession?.LevelForExperience(after) ?? MiniGameProgression.Level(after),
        };
    }

    internal static FishingRewardResult ConsumeBait(Player player, Guid baitId, int quantity)
    {
        if (baitId == Guid.Empty) return FishingRewardResult.Success;
        if (player.User is not { } user) return FishingRewardResult.SaveFailed;
        lock (user.PokerSaveGate)
            return Commit(player, Guid.Empty, Guid.Empty, 0, null, baitId, quantity, () => user.Save(force: true));
    }

    internal static FishingRewardResult Reward(Player player, Guid receipt, Guid itemId, int quantity, FishingProgressAward award, bool bank = false, IReadOnlyList<FishingInventoryEntry>? bonusLoot = null)
    {
        if (player.User is not { } user) return FishingRewardResult.SaveFailed;
        lock (user.PokerSaveGate)
            return Commit(player, receipt, itemId, quantity, award, Guid.Empty, 0, () => user.Save(force: true), bank, bonusLoot);
    }

    /// <summary>Caller holds EntityLock and the account's save gate. No notification or item Event runs before commit.</summary>
    internal static FishingRewardResult Commit(Player player, Guid receipt, Guid itemId, int quantity,
        FishingProgressAward? award, Guid baitId, int baitQuantity, Func<UserSaveResult> checkpoint, bool bank = false, IReadOnlyList<FishingInventoryEntry>? bonusLoot = null)
    {
        if (receipt != Guid.Empty && player.GetVariable(FishingRules.LastRewardId)?.Value.String == receipt.ToString("N"))
            return FishingRewardResult.AlreadyCommitted;
        if (itemId != Guid.Empty && (receipt == Guid.Empty || quantity is < 1 or > 1000 || award == null))
            return FishingRewardResult.SaveFailed;
        if (bonusLoot != null && (receipt == Guid.Empty || itemId == Guid.Empty || award == null || bonusLoot.Count > 16 ||
            bonusLoot.Any(loot => loot == null || loot.ItemId == Guid.Empty || loot.Quantity is < 1 or > 1000)))
            return FishingRewardResult.SaveFailed;
        if (bank && (!player.HasBankPremium || !player.BankAccount.Opened || baitId != Guid.Empty))
            return FishingRewardResult.SaveFailed;
        // Sparse inventories must be expanded through the existing legal-slot resolver, never by adding slots past capacity.
        _ = player.FindOpenInventorySlots();
        if (bank) { player.EnsureBankCapacity(); player.Bank.FillToCapacity(); }
        var inventorySlots = player.Items.OrderBy(s => s.Slot).ToArray();
        var slots = bank ? player.Bank.Take(player.EnsureBankCapacity()).Cast<Item>().ToArray()
            : inventorySlots.Cast<Item>().ToArray();
        var before = slots.Select(s => s.Clone()).ToArray();
        var plan = before.Select(item => item.Clone()).ToArray();
        if (baitId != Guid.Empty)
        {
            var remaining = baitQuantity;
            for (var i = 0; i < plan.Length && remaining > 0; i++)
            {
                if (plan[i].ItemId != baitId || plan[i].BagId.GetValueOrDefault() != Guid.Empty || player.SlotIsEquipped(inventorySlots[i].Slot, out _)) continue;
                var take = Math.Min(remaining, Math.Max(0, plan[i].Quantity));
                plan[i].Quantity -= take;
                remaining -= take;
                if (plan[i].Quantity == 0) plan[i] = Item.None;
            }
            if (remaining != 0) return FishingRewardResult.MissingBait;
        }
        var grants = new List<(Guid ItemId, int Quantity)>();
        if (itemId != Guid.Empty) grants.Add((itemId, quantity));
        if (bonusLoot != null) grants.AddRange(bonusLoot.Select(loot => (loot.ItemId, loot.Quantity)));
        if (award?.Profession is { } profession)
            grants.AddRange(profession.LevelRewards.Where(r => r.Level > award.PreviousLevel && r.Level <= award.NewLevel && r.ItemId != Guid.Empty && r.Quantity > 0)
                .Select(r => (r.ItemId, r.Quantity)));
        foreach (var grant in grants)
        {
            if (ItemDescriptor.Get(grant.ItemId) is not { } descriptor) return FishingRewardResult.SaveFailed;
            if (bank && !descriptor.CanBank) return FishingRewardResult.InventoryFull;
            var maximum = descriptor.IsStackable ? (bank ? descriptor.MaxBankStack : descriptor.MaxInventoryStack) : 1;
            if (maximum <= 0) return FishingRewardResult.SaveFailed;
            var remaining = grant.Quantity;
            if (descriptor.IsStackable)
                for (var i = 0; i < plan.Length && remaining > 0; i++)
                {
                    if (plan[i].ItemId != grant.ItemId || plan[i].BagId.GetValueOrDefault() != Guid.Empty || plan[i].Quantity < 0) continue;
                    var add = Math.Min(remaining, Math.Max(0, maximum - plan[i].Quantity));
                    plan[i].Quantity += add;
                    remaining -= add;
                }
            for (var i = 0; i < plan.Length && remaining > 0; i++)
                if (plan[i].ItemId == Guid.Empty)
                {
                    var add = Math.Min(remaining, maximum);
                    plan[i] = new Item(grant.ItemId, add);
                    remaining -= add;
                }
            if (remaining != 0) return FishingRewardResult.InventoryFull;
        }

        var variables = new List<(PlayerVariable Variable, bool Created, Newtonsoft.Json.Linq.JObject Before)>();
        void Variable(Guid id, Action<PlayerVariable> update)
        {
            var original = player.GetVariable(id);
            var variable = original ?? new PlayerVariable(id);
            variables.Add((variable, original == null, variable.Value.Json));
            if (original == null) player.Variables.Add(variable);
            update(variable);
        }
        try
        {
            for (var i = 0; i < slots.Length; i++) slots[i].Set(plan[i]);
            if (award != null) Variable(award.VariableId, v => v.Value.Integer = award.RawAfter);
            if (receipt != Guid.Empty) Variable(FishingRules.LastRewardId, v => v.Value.String = receipt.ToString("N"));
            if (checkpoint() == UserSaveResult.Completed) return FishingRewardResult.Success;
        }
        catch (Exception error)
        {
            ApplicationContext.Context.Value?.Logger.LogError(error, "Fishing reward checkpoint failed for {CharacterId}", player.Id);
        }
        for (var i = 0; i < slots.Length; i++) slots[i].Set(before[i]);
        foreach (var saved in variables)
            if (saved.Created) player.Variables.Remove(saved.Variable);
            else saved.Variable.Value.Json = saved.Before;
        return FishingRewardResult.SaveFailed;
    }
}
