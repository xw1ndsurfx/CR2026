using Intersect.Core;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.MiniGames;
using Intersect.Server.Entities;
using Intersect.Server.Networking;

namespace Intersect.Server.MiniGames;

internal static class PokerLevelRewardRuntime
{
    internal static bool DefinitionsExist(IEnumerable<PokerLevelReward>? rewards)
    {
        foreach (var reward in rewards ?? [])
            if (!reward.IsValid || ItemDescriptor.Get(reward.ItemId) == null) return false;
        return true;
    }

    internal static void Grant(Player player, int level, IReadOnlyList<PokerLevelReward> rewards)
    {
        foreach (var reward in rewards)
        {
            if (!reward.IsValid || ItemDescriptor.Get(reward.ItemId) is not { } item)
            {
                PacketSender.SendChatMsg(player, $"[Poker] Level {level} reward is unavailable because its item no longer exists.",
                    ChatMessageType.Error, Color.White);
                continue;
            }

            // Bank overflow makes level rewards resilient to a full backpack without dropping items on the map.
            if (player.TryGiveItem(reward.ItemId, reward.Quantity, ItemHandling.Normal, bankOverflow: true))
            {
                PacketSender.SendChatMsg(player,
                    $"[Poker] Level {level} reward: {reward.Quantity:N0} x {item.Name}.",
                    ChatMessageType.Inventory, Color.White);
            }
            else
            {
                PacketSender.SendChatMsg(player,
                    $"[Poker] Level {level} reward could not be delivered. Free inventory/bank space and contact an administrator.",
                    ChatMessageType.Error, Color.White);
            }
        }
    }
}
