using System.Security.Cryptography;
using System.Text;
using Intersect.Framework.Core.Achievements;
using Intersect.Framework.Core.WorldEvents.Invasions;
using Intersect.Server.Achievements;
using Intersect.Server.Entities;

namespace Intersect.Server.WorldEvents.Invasions;

internal static class InvasionProgressRuntime
{
    private static Guid StateId(Guid invasionId, InvasionProgressMetric metric) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"cr2026:invasion:{invasionId:N}:{(int)metric}"))[..16]);

    internal static long Get(Player player, Guid invasionId, InvasionProgressMetric metric) =>
        Math.Max(0L, player.GetVariable(StateId(invasionId, metric))?.Value?.Integer ?? 0L);

    internal static void Record(Player player, Guid invasionId, InvasionProgressMetric metric)
    {
        if (invasionId == Guid.Empty || player.IsDisposed) return;
        lock (player.EntityLock)
        {
            foreach (var id in new[] { Guid.Empty, invasionId })
            {
                var value = Get(player, id, metric);
                player.SetVariableValue(StateId(id, metric), value == long.MaxValue ? value : value + 1);
            }
            var objective = metric switch
            {
                InvasionProgressMetric.Victories => AchievementObjectiveType.InvasionCompletions,
                InvasionProgressMetric.Participations => AchievementObjectiveType.InvasionParticipations,
                InvasionProgressMetric.WavesCleared => AchievementObjectiveType.InvasionWavesCleared,
                _ => AchievementObjectiveType.InvasionDefeats,
            };
            AchievementRuntime.AddProgress(player, objective, 1, invasionId);
            player.UpdateInvasionQuestTasks(invasionId, metric);
        }
    }
}
