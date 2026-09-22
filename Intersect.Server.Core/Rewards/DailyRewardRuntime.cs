#nullable enable
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.MiniGames;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.MiniGames;
using Intersect.Server.Networking;

namespace Intersect.Server.Rewards;

internal static class DailyRewardRuntime
{
    private static readonly DailyRewardStore Store = new();

    private static int DateKey(DateTime utc) => utc.Year * 10000 + utc.Month * 100 + utc.Day;

    internal static DailyRewardStatePacket State(Player player, string message = "")
    {
        if (player.User == null) return new DailyRewardStatePacket { Message = "Account unavailable." };

        var config = RewardConfigurationRuntime.Current;
        var saved = Store.Load(player.User.Id);
        var now = DateTime.UtcNow;
        var today = DateKey(now);
        var canClaim = saved.LastClaimDate != today;
        var day = canClaim
            ? saved.CycleDay % config.DailyCycleDays + 1
            : Math.Clamp(saved.CycleDay, 1, config.DailyCycleDays);

        var next = new DateTimeOffset(now.Date.AddDays(1), TimeSpan.Zero).ToUnixTimeMilliseconds();

        return new DailyRewardStatePacket
        {
            CycleDays = config.DailyCycleDays,
            CurrentDay = day,
            CanClaim = canClaim,
            ClaimedToday = !canClaim,
            NextClaimUnixMs = next,
            Rewards = config.DailyRewards
                .OrderBy(reward => reward.Day)
                .ThenBy(reward => reward.ItemId)
                .Select(reward => new DailyRewardPacketEntry
                {
                    Day = reward.Day,
                    ItemId = reward.ItemId,
                    Quantity = reward.Quantity,
                }).ToArray(),
            Message = message,
        };
    }

    internal static DailyRewardStatePacket Claim(Player player)
    {
        if (player.User == null) return State(player, "Account unavailable.");

        var config = RewardConfigurationRuntime.Current;
        var before = Store.Load(player.User.Id);
        var now = DateTime.UtcNow;
        var today = DateKey(now);
        if (before.LastClaimDate == today)
        {
            return State(player, "Today's reward has already been claimed.");
        }

        var day = before.CycleDay % config.DailyCycleDays + 1;
        var rewards = config.DailyRewards.Where(reward => reward.Day == day).ToArray();

        if (!Store.TryClaim(player.User.Id, today, day, out _))
        {
            return State(player, "Today's reward has already been claimed.");
        }

        var delivered = new List<string>();
        foreach (var reward in rewards)
        {
            if (ItemDescriptor.Get(reward.ItemId) is not { } item) continue;
            if (player.TryGiveItem(reward.ItemId, reward.Quantity, ItemHandling.Normal, bankOverflow: true))
            {
                delivered.Add($"{reward.Quantity:N0} x {item.Name}");
            }
            else
            {
                PacketSender.SendChatMsg(player,
                    $"[Daily Reward] {reward.Quantity:N0} x {item.Name} could not be delivered. Contact an administrator.",
                    ChatMessageType.Error, Color.White);
            }
        }

        var message = delivered.Count == 0
            ? $"Day {day} claimed."
            : $"Day {day} claimed: {string.Join(", ", delivered)}";
        PacketSender.SendChatMsg(player, "[Daily Reward] " + message, ChatMessageType.Inventory, Color.White);
        return State(player, message);
    }
}
