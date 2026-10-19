using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Framework.Core.Achievements;
using Intersect.Framework.Core.GameObjects.Conditions;
using Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;
using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.Framework.Core.WorldEvents.Invasions;
using Intersect.GameObjects;
using Intersect.Server.Entities;
using Intersect.Server.Entities.Events;
using PlayerQuest = Intersect.Server.Database.PlayerData.Players.Quest;

internal static class InvasionObjectivesTests
{
    [ModuleInitializer] internal static void Run()
    {
        var old = Options.Instance;
        try { typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, new Options()); Verify(); }
        catch(Exception e) { System.Console.Error.WriteLine(e); Environment.Exit(1); }
        finally { typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, old); }
    }
    private static void Check(bool ok, string message) { if(!ok) throw new Exception(message); }
    private static void Verify()
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var assembly = typeof(Player).Assembly;
        var runtime = assembly.GetType("Intersect.Server.WorldEvents.Invasions.InvasionRuntime", true)!;
        var counters = assembly.GetType("Intersect.Server.WorldEvents.Invasions.InvasionProgressRuntime", true)!;
        var achievementRuntime = assembly.GetType("Intersect.Server.Achievements.AchievementRuntime", true)!;
        var cache = assembly.GetType("Intersect.Server.Achievements.AchievementConfigurationRuntime", true)!.GetField("_current", flags)!;
        var oldConfig = cache.GetValue(null);
        var invasionId = Guid.NewGuid(); var other = Guid.NewGuid();
        var player = new Player { Id = Guid.NewGuid() }; GC.SuppressFinalize(player);
        var achievement = new AchievementDefinition { ObjectiveType = AchievementObjectiveType.InvasionCompletions, TargetId = invasionId, TargetAmount = 2 };
        var generic = new AchievementDefinition { ObjectiveType = AchievementObjectiveType.InvasionCompletions, TargetAmount = 3 };
        var wrong = new AchievementDefinition { ObjectiveType = AchievementObjectiveType.InvasionCompletions, TargetId = other, TargetAmount = 2 };
        cache.SetValue(null, new AchievementConfiguration { Achievements = [achievement, generic, wrong] });
        var task = new QuestTaskDescriptor(Guid.NewGuid()) { Objective = QuestObjective.WinInvasions, Quantity = 2, TargetId = invasionId };
        var quest = new QuestDescriptor(Guid.NewGuid()) { Tasks = [task] }; QuestDescriptor.Lookup[quest.Id] = quest;
        var progress = new PlayerQuest(quest.Id) { TaskId = task.Id }; player.Quests.Add(progress);
        void Record(InvasionProgressMetric metric, Guid? id = null) => counters.GetMethod("Record", flags)!.Invoke(null,[player,id ?? invasionId,metric]);
        long Count(InvasionProgressMetric metric, Guid id) => (long)counters.GetMethod("Get", flags)!.Invoke(null,[player,id,metric])!;
        long Ach(Guid id) => (long)achievementRuntime.GetMethod("GetProgress", flags)!.Invoke(null,[player,id])!;
        bool Meets(InvasionConditionMode mode, int amount = 1, Guid? id = null, bool negated = false) => Conditions.MeetsCondition((Condition)new InvasionCondition { InvasionId = id ?? invasionId, Mode = mode, Amount = amount, Negated = negated },player,null!,null!);
        var sessions = (IDictionary)runtime.GetField("SessionsById",flags)!.GetValue(null)!;
        var preparing = (IDictionary)runtime.GetField("PreparingInvasions",flags)!.GetValue(null)!;
        var online = (IDictionary)typeof(Player).GetField("OnlinePlayersById",flags)!.GetValue(null)!;
        var waveTask = new QuestTaskDescriptor(Guid.NewGuid()) { Objective = QuestObjective.ClearInvasionWaves, Quantity = 1, TargetId = invasionId };
        var waveQuest = new QuestDescriptor(Guid.NewGuid()) { Tasks = [waveTask] }; QuestDescriptor.Lookup[waveQuest.Id] = waveQuest;
        var sessionType = runtime.GetNestedType("Session",BindingFlags.NonPublic)!;
        var session = Activator.CreateInstance(sessionType,true)!;
        var sessionId = (Guid)sessionType.GetProperty("SessionId")!.GetValue(session)!;
        try
        {
            Record(InvasionProgressMetric.Defeats); Record(InvasionProgressMetric.WavesCleared);
            Check(progress.TaskProgress == 0 && Meets(InvasionConditionMode.DefeatsAtLeast) && Meets(InvasionConditionMode.WavesClearedAtLeast), "Losses/waves feed independent history without awarding victory quests");
            Record(InvasionProgressMetric.Victories, other);
            Check(progress.TaskProgress == 0 && Count(InvasionProgressMetric.Victories,invasionId) == 0 && Count(InvasionProgressMetric.Victories,Guid.Empty) == 1, "Invasion-specific quests/history exclude other invasions; global history aggregates");
            Record(InvasionProgressMetric.Victories);
            Check(progress.TaskProgress == 1 && Ach(achievement.Id) == 1 && Ach(generic.Id) == 2 && Ach(wrong.Id) == 1, "Actual victory advances matching and wildcard achievements plus the active quest");
            Record(InvasionProgressMetric.Victories);
            Check(progress.Completed && (bool)achievementRuntime.GetMethod("IsCompleted",flags)!.Invoke(null,[player,achievement.Id])! && Meets(InvasionConditionMode.VictoriesAtLeast,2), "Thresholds complete actual quests and achievements and satisfy event conditions");
            Record(InvasionProgressMetric.Participations);
            Check(Meets(InvasionConditionMode.ParticipationsAtLeast) && !Meets(InvasionConditionMode.VictoriesAtLeast,3) && Meets(InvasionConditionMode.VictoriesAtLeast,3,negated:true), "Condition registry dispatch and negation use authoritative persisted counts");
            var condition = new InvasionCondition { InvasionId = invasionId, Mode = InvasionConditionMode.WaveAtLeast, Amount = 3, Negated = true };
            var copy = new ConditionList(new ConditionList { Conditions = [condition] }.Data());
            Check(copy.Conditions.Single() is InvasionCondition c && c.InvasionId == invasionId && c.Amount == 3 && c.Negated, "New condition round trips with its stable serialized type");
            sessionType.GetProperty("Definition")!.SetValue(session,new InvasionDefinition { Id = invasionId });
            sessionType.GetProperty("WaveIndex")!.SetValue(session,2); sessions.Add(sessionId,session);
            ((IDictionary)sessionType.GetProperty("Participants")!.GetValue(session)!).Add(player.Id,(byte)0);
            preparing.Add(invasionId,(byte)0);
            Check(Meets(InvasionConditionMode.Active) && Meets(InvasionConditionMode.Preparing) && Meets(InvasionConditionMode.PlayerParticipating) && Meets(InvasionConditionMode.WaveAtLeast,3) && !Meets(InvasionConditionMode.Active,id:other), "Live conditions scope active/pre-alert/participation/wave state to the selected invasion");
            player.Quests.Add(new PlayerQuest(waveQuest.Id) { TaskId = waveTask.Id });
            online.Add(player.Id,player);
            var definition = (InvasionDefinition)sessionType.GetProperty("Definition")!.GetValue(session)!;
            definition.Waves = [new() { DelaySeconds = 2 }, new() { DelaySeconds = 4 }];
            sessionType.GetProperty("WaveIndex")!.SetValue(session,0);
            sessionType.GetProperty("NextWaveAtMs")!.SetValue(session,long.MaxValue);
            runtime.GetMethod("ScheduleNextWave",flags)!.Invoke(null,[session,10000L]);
            runtime.GetMethod("ScheduleNextWave",flags)!.Invoke(null,[session,10001L]);
            Check(Count(InvasionProgressMetric.WavesCleared,invasionId) == 2 && player.Quests.Single(q => q.QuestId == waveQuest.Id).Completed,
                "Actual wave transition credits participants and completes wave quests exactly once");
            sessionType.GetProperty("Completed")!.SetValue(session,true);
            Check(!Meets(InvasionConditionMode.Active) && !Meets(InvasionConditionMode.PlayerParticipating), "Ended sessions do not remain active in event conditions");
        }
        finally { cache.SetValue(null,oldConfig); QuestDescriptor.Lookup.DeleteAt(quest.Id); QuestDescriptor.Lookup.DeleteAt(waveQuest.Id); online.Remove(player.Id); sessions.Remove(sessionId); preparing.Remove(invasionId); }
        System.Console.WriteLine("PASS invasion objective pipeline: persisted global/specific counts, real quest/achievement completion, condition registry/negation/serialization and live states");
    }
}
