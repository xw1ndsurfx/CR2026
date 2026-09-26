#nullable enable
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.MiniGames;
using Intersect.Server.Networking;

namespace Intersect.Server.Rewards;

internal static class DailyRewardRuntime
{
    private static readonly DailyRewardStore Store = new();

    private static int DateKey(DateTime utc) => utc.Year * 10000 + utc.Month * 100 + utc.Day;

    internal static DailyRewardStatePacket State(Player player, string message = "", bool autoOpen = false)
    {
        if (player.User == null) return new DailyRewardStatePacket { Message = "Account unavailable." };

        var config = RewardConfigurationRuntime.Current;
        if (!config.DailyRewardsEnabled)
        {
            return new DailyRewardStatePacket
            {
                Enabled = false,
                CycleDays = config.DailyCycleDays,
                CurrentDay = 1,
                CanClaim = false,
                Message = string.IsNullOrWhiteSpace(message) ? "Daily rewards are disabled." : message,
            };
        }

        var saved = Store.Load(player.User.Id);
        var now = DateTime.UtcNow;
        var today = DateKey(now);
        var canClaim = saved.LastClaimDate != today;
        var day = canClaim
            ? saved.CycleDay % config.DailyCycleDays + 1
            : Math.Clamp(saved.CycleDay, 1, config.DailyCycleDays);

        return new DailyRewardStatePacket
        {
            Enabled = true,
            CycleDays = config.DailyCycleDays,
            CurrentDay = day,
            LastClaimedDay = saved.CycleDay,
            CanClaim = canClaim,
            ClaimedToday = !canClaim,
            NextClaimUnixMs = new DateTimeOffset(now.Date.AddDays(1), TimeSpan.Zero).ToUnixTimeMilliseconds(),
            AutoOpen = autoOpen && config.ShowDailyRewardsOnLogin && canClaim,
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

        lock (player.User.PokerSaveGate)
        {
            var config = RewardConfigurationRuntime.Current;
            if (!config.DailyRewardsEnabled) return State(player, "Daily rewards are disabled.");

            var before = Store.Load(player.User.Id);
            var now = DateTime.UtcNow;
            var today = DateKey(now);
            if (before.LastClaimDate == today) return State(player, "Today's reward has already been claimed.");

            var day = before.CycleDay % config.DailyCycleDays + 1;
            var rewards = config.DailyRewards.Where(reward => reward.Day == day).ToArray();
            if (rewards.Length == 0) return State(player, $"Day {day} has no configured reward.");

            var delivered = new List<string>();
            foreach (var reward in rewards)
            {
                if (ItemDescriptor.Get(reward.ItemId) is not { } item ||
                    !player.TryGiveItem(reward.ItemId, reward.Quantity, ItemHandling.Normal, bankOverflow: true))
                {
                    PacketSender.SendChatMsg(
                        player,
                        $"[Daily Reward] Day {day} could not be fully delivered. Free inventory/bank space and try again.",
                        ChatMessageType.Error,
                        Color.White
                    );
                    return State(player, "Reward delivery failed; the day was not marked as claimed.");
                }
                delivered.Add($"{reward.Quantity:N0} x {item.Name}");
            }

            if (!Store.TryClaim(player.User.Id, today, day, out _))
                return State(player, "Today's reward has already been claimed.");

            var message = $"Day {day} claimed: {string.Join(", ", delivered)}";
            PacketSender.SendChatMsg(player, "[Daily Reward] " + message, ChatMessageType.Inventory, Color.White);
            return State(player, message);
        }
    }
}
