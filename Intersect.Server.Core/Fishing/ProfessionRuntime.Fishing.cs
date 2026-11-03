using Intersect.Enums;
using Intersect.Framework.Core.Achievements;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.Professions;
using Intersect.Server.Achievements;
using Intersect.Server.Entities;

namespace Intersect.Server.Professions;

internal static partial class ProfessionRuntime
{
    /// <summary>The inventory and profession XP have already committed atomically; publish existing profession hooks.</summary>
    internal static void PublishFishingAward(Player player, ProfessionDefinition definition, int previousLevel, long experienceGained)
    {
        SendHarvestProgress(player, definition, experienceGained, previousLevel);
        var level = GetLevel(player, definition.Id);
        AchievementRuntime.SetAbsoluteProgress(player, AchievementObjectiveType.ProfessionLevel, level, definition.Id);
        player.StartCommonEventsWithTrigger(CommonEventTrigger.PlayerVariableChange, "", definition.Id.ToString());
        // Item level rewards are already in the transaction. Fire only the authored common-event rewards here.
        foreach (var reward in definition.LevelRewards.Where(r => r.Level > previousLevel && r.Level <= level))
            if (reward.EventId != Guid.Empty && EventDescriptor.Get(reward.EventId) is { CommonEvent: true } evt)
                player.EnqueueStartCommonEvent(evt);
    }
}
