using Intersect.Core;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Variables;
using Intersect.Framework.Core.MiniGames;
using Intersect.Framework.Core.Professions;
using Intersect.Server.Entities;
using Intersect.Server.Professions;
using Intersect.Server.Networking;
using Newtonsoft.Json;

namespace Intersect.Server.MiniGames;

internal static class RewardConfigurationRuntime
{
    private static readonly object Gate = new();
    private static readonly string PathName = Path.Combine("resources", "reward-configuration.json");
    private static readonly LevelRewardClaimStore Claims = new();
    private static RewardConfiguration? _current;

    internal static RewardConfiguration Current
    {
        get
        {
            lock (Gate)
            {
                return _current ??= LoadCore();
            }
        }
    }

    internal static string Json
    {
        get
        {
            lock (Gate)
            {
                return Current.ToJson();
            }
        }
    }

    internal static bool LevelDefinitionsExist(IEnumerable<PokerLevelReward> rewards) =>
        PokerLevelRewardRuntime.DefinitionsExist(rewards);

    internal static bool IsValid(RewardConfiguration configuration) =>
        configuration.IsStructurallyValid &&
        LevelDefinitionsExist(configuration.PokerLevelRewards) &&
        LevelDefinitionsExist(configuration.BlackjackLevelRewards) &&
        configuration.DailyRewards.All(reward => ItemDescriptor.Get(reward.ItemId) != null) &&
        configuration.PotionRecipes.All(recipe =>
            ItemDescriptor.Get(recipe.OutputItemId) != null &&
            (recipe.UnlockPlayerVariableId == Guid.Empty ||
             PlayerVariableDescriptor.Get(recipe.UnlockPlayerVariableId) is { DataType: VariableDataType.Boolean })) &&
        configuration.CookingRecipes.All(recipe =>
            ProfessionConfigurationRuntime.Current.Find(recipe.ProfessionId) != null &&
            recipe.Ingredients.All(ingredient => ItemDescriptor.Get(ingredient.ItemId) != null) &&
            recipe.Outputs.All(output => ItemDescriptor.Get(output.ItemId) != null) &&
            (recipe.UnlockPlayerVariableId == Guid.Empty ||
             PlayerVariableDescriptor.Get(recipe.UnlockPlayerVariableId) is { DataType: VariableDataType.Boolean }));

    internal static void Save(string json)
    {
        var configuration = RewardConfiguration.FromJson(json);
        if (!IsValid(configuration))
        {
            throw new InvalidDataException("Reward configuration references missing or invalid items.");
        }

        lock (Gate)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(PathName))!);
            var temp = PathName + ".tmp";
            File.WriteAllText(temp, configuration.ToJson());
            File.Move(temp, PathName, overwrite: true);
            _current = configuration;
        }
    }

    internal static void GrantPendingLevelRewards(Player player, string gameKey, int currentLevel)
    {
        if (currentLevel < 2) return;

        var configuration = Current;
        var rewards = string.Equals(gameKey, MiniGameProgression.Blackjack, StringComparison.OrdinalIgnoreCase)
            ? configuration.BlackjackLevelRewards
            : configuration.PokerLevelRewards;
        var gameName = string.Equals(gameKey, MiniGameProgression.Blackjack, StringComparison.OrdinalIgnoreCase)
            ? "Blackjack"
            : "Poker";

        foreach (var group in (rewards ?? [])
                     .Where(reward => reward.Level <= currentLevel)
                     .GroupBy(reward => reward.Level)
                     .OrderBy(group => group.Key))
        {
            if (Claims.IsClaimed(player.Id, gameKey, group.Key)) continue;

            var delivered = true;
            var descriptions = new List<string>();
            foreach (var reward in group)
            {
                if (ItemDescriptor.Get(reward.ItemId) is not { } item ||
                    !player.TryGiveItem(reward.ItemId, reward.Quantity, ItemHandling.Normal, bankOverflow: true))
                {
                    delivered = false;
                    PacketSender.SendChatMsg(
                        player,
                        $"[{gameName}] Level {group.Key} reward could not be delivered. Free inventory/bank space and it will retry.",
                        ChatMessageType.Error,
                        Color.White
                    );
                    break;
                }

                descriptions.Add($"{reward.Quantity:N0} x {item.Name}");
            }

            if (!delivered || !Claims.TryMarkClaimed(player.Id, gameKey, group.Key)) continue;
            PacketSender.SendChatMsg(
                player,
                $"[{gameName}] Level {group.Key} reward: {string.Join(", ", descriptions)}",
                ChatMessageType.Inventory,
                Color.White
            );
        }
    }

    private static RewardConfiguration LoadCore()
    {
        if (!File.Exists(PathName))
        {
            return new RewardConfiguration();
        }

        try
        {
            var configuration = RewardConfiguration.FromJson(File.ReadAllText(PathName));
            return IsValid(configuration) ? configuration : new RewardConfiguration();
        }
        catch
        {
            // Never overwrite an unreadable configuration automatically.
            return new RewardConfiguration();
        }
    }
}
