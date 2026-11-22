using Intersect.Framework.Core.GuildHeadquarters;
using Intersect.Server.Database.PlayerData.Players;
using Newtonsoft.Json;

namespace Intersect.Server.GuildHeadquarters;

internal static class HeadquartersPurchase
{
    internal static long Available(Guild guild, Guid item) => item == Guid.Empty ? 0 : guild.Bank
        .Where(slot => slot.ItemId == item && slot.BagId == null && slot.Quantity > 0).Sum(slot => (long)slot.Quantity);

    // Call under the guild's native bank lock. The save includes both the bank and the unlock in one database transaction.
    internal static void Buy(Guild guild, HeadquartersConfiguration config, Guid tierId, long revision, int rank, Action save)
    {
        lock (guild.Lock)
        {
            if (!config.Enabled || rank < 0 || rank > config.ManagingRank) throw new InvalidOperationException("You cannot manage this headquarters.");
            var profile = HeadquartersProfile.Read(guild.HeadquartersJson);
            var next = config.Tiers.ElementAtOrDefault(profile.TierIndex(config) + 1);
            if (next == null || next.Id != tierId || profile.Revision != revision)
                throw new InvalidOperationException("The headquarters changed. Review the updated details.");
            if (guild.Members.Count < next.MinimumMembers) throw new InvalidOperationException("Your guild needs more members for this upgrade.");
            if (Available(guild, next.CostItemId) < next.CostQuantity) throw new InvalidOperationException("Deposit the required resources in the guild bank first.");
            var before = guild.HeadquartersJson;
            var capacity = guild.Bank.Capacity;
            var count = guild.BankSlotsCount;
            var slots = guild.Bank.Where(slot => slot.ItemId == next.CostItemId && slot.BagId == null && slot.Quantity > 0)
                .Select(slot => (Slot: slot, Item: slot.Clone())).ToArray();
            try
            {
                var remaining = next.CostQuantity;
                foreach (var (slot, _) in slots)
                {
                    var taken = Math.Min(remaining, slot.Quantity);
                    slot.Quantity -= taken; remaining -= taken;
                    if (slot.Quantity == 0) slot.Set(Intersect.Server.Database.Item.None);
                    if (remaining == 0) break;
                }
                profile.Upgrades = [.. profile.Upgrades, next.Id];
                profile.Revision = checked(profile.Revision + 1);
                guild.HeadquartersJson = JsonConvert.SerializeObject(profile);
                guild.BankSlotsCount = Math.Max(count, next.BankSlots);
                guild.Bank.Capacity = Math.Max(capacity, guild.BankSlotsCount);
                save();
            }
            catch
            {
                guild.HeadquartersJson = before;
                guild.BankSlotsCount = count;
                guild.Bank.Capacity = capacity;
                foreach (var (slot, item) in slots) slot.Set(item);
                throw;
            }
        }
    }
}
