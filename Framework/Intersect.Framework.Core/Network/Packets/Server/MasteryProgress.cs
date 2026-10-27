using Intersect.Framework.Core.Archery;

namespace Intersect.Network.Packets.Server;

// Reuse the existing timed activity-progress HUD and its native packet.
public static class MasteryProgress
{
    public static ProfessionProgressPacket Create(MasteryCategory category, long before, long after)
    {
        before = Math.Clamp(before, 0, category.Maximum);
        after = Math.Clamp(after, before, category.Maximum);
        var level = category.Level(after);
        var maximum = level >= category.Cap;
        var into = maximum ? 0 : category.WithinLevel(after);
        var required = maximum ? 0 : category.Required(level);
        return new ProfessionProgressPacket
        {
            ProfessionName = category.Name, Level = level, MaximumLevel = category.Cap,
            ExperienceGained = after - before, ExperienceIntoLevel = into,
            ExperienceRequiredForLevel = required, ExperienceToNextLevel = maximum ? -1 : required - into,
            Percentage = maximum ? 100 : (int)(100L * into / required),
            LeveledUp = level > category.Level(before), MaximumLevelReached = maximum,
        };
    }
}
