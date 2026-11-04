using Intersect.Framework.Core.Achievements;
using Intersect.Framework.Core.Professions;
using Intersect.Server.Achievements;
using Intersect.Server.Entities;

namespace Intersect.Server.Professions;

internal static partial class ProfessionRuntime
{
    // Only publishes progression saved in the player's existing account
    // transaction. Currency, quest state and XP are not changed here.
    internal static void PublishExportationAward(Player player, ProfessionDefinition definition,
        int previousLevel, long gainedExperience)
    {
        SendHarvestProgress(player, definition, Math.Max(0, gainedExperience), previousLevel);
        AchievementRuntime.SetAbsoluteProgress(player, AchievementObjectiveType.ProfessionLevel,
            GetLevel(player, definition.Id), definition.Id);
    }
}
