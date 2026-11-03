using System.Reflection;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Achievements;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Server.Achievements;
using Intersect.Server.Entities;
using NUnit.Framework;

namespace Intersect.Tests.Server.Naval;

public sealed partial class NavalTests
{
    [TestCase("")]
    [TestCase("leftover custom counter key")]
    [TestCase("50")]
    public void MonthlyAnyNpcMissionCreditsTwoDifferentNpcKills(string targetKey)
    {
        using var world = new EnemyTestWorld();
        var cache = typeof(AchievementConfigurationRuntime).GetField("_current", BindingFlags.Static | BindingFlags.NonPublic)!;
        var previous = cache.GetValue(null);
        var mission = new AchievementDefinition { Monthly = true, ObjectiveType = AchievementObjectiveType.NpcKills, TargetAmount = 3, TargetKey = targetKey };
        var ordinary = new AchievementDefinition { ObjectiveType = AchievementObjectiveType.NpcKills, TargetAmount = 4 };
        var firstNpcId = Guid.NewGuid();
        var specific = new AchievementDefinition { Monthly = true, ObjectiveType = AchievementObjectiveType.NpcKills, TargetId = firstNpcId, TargetAmount = 5 };
        var custom = new AchievementDefinition { Monthly = true, ObjectiveType = AchievementObjectiveType.CustomCounter, TargetKey = "code", TargetAmount = 5 };
        cache.SetValue(null, new AchievementConfiguration { Achievements = [mission, ordinary, specific, custom] });
        var player = new Player { Id = Guid.NewGuid(), MapId = world.Map.Id, Level = Options.Instance.Player.MaxLevel };
        GC.SuppressFinalize(player);
        try
        {
            var setRaw = typeof(AchievementRuntime).GetMethod("SetRaw", BindingFlags.Static | BindingFlags.NonPublic)!;
            setRaw.Invoke(null, [player, mission.Id, (byte)0x31, 3L, DateTimeOffset.UtcNow.AddMonths(-1)]);
            setRaw.Invoke(null, [player, mission.Id, (byte)0x57, 1L, DateTimeOffset.UtcNow.AddMonths(-1)]);
            for (var i = 0; i < 2; i++)
            {
                var npc = new Npc(new NPCDescriptor(i == 0 ? firstNpcId : Guid.NewGuid()) { Experience = 0 }) { MapId = world.Map.Id };
                GC.SuppressFinalize(npc);
                player.KilledEntity(npc);
            }
            Assert.That(AchievementRuntime.GetProgress(player, mission.Id), Is.EqualTo(2));
            Assert.That(AchievementRuntime.GetProgress(player, ordinary.Id), Is.EqualTo(2));
            Assert.That(AchievementRuntime.BuildState(player).Single(entry => entry.AchievementId == mission.Id).Progress, Is.EqualTo(2));
            Assert.That(AchievementRuntime.GetProgress(player, specific.Id), Is.EqualTo(1), "A selected NPC still filters by descriptor ID.");
            AchievementRuntime.AddProgress(player, AchievementObjectiveType.CustomCounter, targetKey: "other");
            Assert.That(AchievementRuntime.GetProgress(player, custom.Id), Is.Zero, "Custom counters still require their configured code.");
            AchievementRuntime.AddProgress(player, AchievementObjectiveType.CustomCounter, targetKey: "code");
            Assert.That(AchievementRuntime.GetProgress(player, custom.Id), Is.EqualTo(1));
        }
        finally { cache.SetValue(null, previous); }
    }
}
