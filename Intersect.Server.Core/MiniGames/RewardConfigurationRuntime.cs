using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.MiniGames;
using Newtonsoft.Json;

namespace Intersect.Server.MiniGames;

internal static class RewardConfigurationRuntime
{
    private static readonly object Gate = new();
    private static readonly string PathName = Path.Combine("resources", "reward-configuration.json");
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
        configuration.DailyRewards.All(reward => ItemDescriptor.Get(reward.ItemId) != null);

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
