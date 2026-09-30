using Intersect.Framework.Core.Achievements;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Server.Professions;

namespace Intersect.Server.Achievements;

internal static class AchievementConfigurationRuntime
{
    private static readonly object Gate = new();
    private static readonly string PathName = Path.Combine("resources", "achievements.json");
    private static AchievementConfiguration? _current;

    internal static AchievementConfiguration Current
    {
        get
        {
            lock (Gate)
                return _current ??= LoadCore();
        }
    }

    internal static string Json
    {
        get
        {
            lock (Gate)
                return Current.ToJson();
        }
    }

    internal static bool IsValid(AchievementConfiguration configuration)
    {
        if (!configuration.IsStructurallyValid)
            return false;

        foreach (var achievement in configuration.Achievements)
        {
            if (achievement.ObjectiveType == AchievementObjectiveType.ProfessionLevel &&
                ProfessionConfigurationRuntime.Current.Find(achievement.TargetId) == null)
                return false;

            var reward = achievement.Reward;
            if (reward.ItemId != Guid.Empty && ItemDescriptor.Get(reward.ItemId) == null)
                return false;

            if (reward.CurrencyItemId != Guid.Empty && ItemDescriptor.Get(reward.CurrencyItemId) == null)
                return false;

            if (reward.CommonEventId != Guid.Empty &&
                EventDescriptor.Get(reward.CommonEventId) is not { CommonEvent: true })
                return false;
        }

        return true;
    }

    internal static void Save(string json)
    {
        var configuration = AchievementConfiguration.FromJson(json);
        if (!IsValid(configuration))
            throw new InvalidDataException(
                "Achievement configuration references a missing profession, item, currency item, or common event."
            );

        lock (Gate)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(PathName))!);
            var temp = PathName + ".tmp";
            File.WriteAllText(temp, configuration.ToJson());
            File.Move(temp, PathName, overwrite: true);
            _current = configuration;
            AchievementConfiguration.Load(configuration.ToJson());
        }
    }

    private static AchievementConfiguration LoadCore()
    {
        try
        {
            var configuration = File.Exists(PathName)
                ? AchievementConfiguration.FromJson(File.ReadAllText(PathName))
                : new AchievementConfiguration();

            if (!IsValid(configuration))
                configuration = new AchievementConfiguration();

            AchievementConfiguration.Load(configuration.ToJson());
            return configuration;
        }
        catch
        {
            var empty = new AchievementConfiguration();
            AchievementConfiguration.Load(empty.ToJson());
            return empty;
        }
    }
}
