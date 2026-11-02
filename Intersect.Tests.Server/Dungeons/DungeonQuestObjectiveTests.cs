using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Framework.Core.Dungeons;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Server.Dungeons;
using Intersect.Server.Entities;
using NUnit.Framework;

namespace Intersect.Tests.Server.Dungeons;

[TestFixture, NonParallelizable]
public sealed class DungeonQuestObjectiveTests
{
    [Test]
    public void QuestObjectiveRoundTripsAndRequiresAQuestWithoutChangingLegacyDefaults()
    {
        var dungeon=new DungeonDefinition { CompletionRequirements=DungeonCompletionRequirement.CompleteQuest };
        Assert.That(dungeon.IsStructurallyValid,Is.False);
        dungeon.CompletionQuestId=Guid.NewGuid();
        var config=new DungeonConfiguration { Dungeons=[dungeon] };
        var copy=DungeonConfiguration.FromJson(config.ToJson()).Dungeons.Single();
        Assert.That(copy.CompletionQuestId,Is.EqualTo(dungeon.CompletionQuestId));
        Assert.That(copy.CompletionRequirements,Is.EqualTo(DungeonCompletionRequirement.CompleteQuest));
        Assert.That(new DungeonDefinition().CompletionRequirements,Is.EqualTo(DungeonCompletionRequirement.DefeatFinalBoss));
        Assert.That(new OpenStableCommand().Type,Is.EqualTo(Intersect.Framework.Core.GameObjects.Events.EventCommandType.OpenStable));
        var json=new OpenStableCommand().GetCopyData([],[]);
        var command=Newtonsoft.Json.JsonConvert.DeserializeObject<EventCommand>(json,new Newtonsoft.Json.JsonSerializerSettings
        {
            TypeNameHandling=Newtonsoft.Json.TypeNameHandling.Auto,
            SerializationBinder=new Intersect.Framework.Core.Serialization.IntersectTypeSerializationBinder(),
        });
        Assert.That(command,Is.TypeOf<OpenStableCommand>(),"Stable commands must survive event copy and persistence");
    }

    [Test]
    public void OnlyAParticipantsMatchingQuestCompletedInsideTheDungeonCounts()
    {
        var type=typeof(DungeonRunRuntime).GetNestedType("Run",BindingFlags.NonPublic)!;
        var runs=(IDictionary)typeof(DungeonRunRuntime).GetField("RunsByInstance",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
        var dungeon=new DungeonDefinition { MapIds=[Guid.NewGuid()], CompletionQuestId=Guid.NewGuid(),
            CompletionRequirements=DungeonCompletionRequirement.CompleteQuest|DungeonCompletionRequirement.DefeatFinalBoss };
        var instance=Guid.NewGuid();
        var run=Activator.CreateInstance(type,true)!;
        type.GetProperty("Dungeon")!.SetValue(run,dungeon);
        type.GetProperty("MapInstanceId")!.SetValue(run,instance);
        var participants=(ConcurrentDictionary<Guid,byte>)type.GetProperty("Participants")!.GetValue(run)!;
        var player=(Player)RuntimeHelpers.GetUninitializedObject(typeof(Player)); GC.SuppressFinalize(player);
        player.Id=Guid.NewGuid();player.MapInstanceId=instance;player.MapId=dungeon.MapIds[0];
        bool Done()=>(bool)type.GetProperty("QuestCompleted")!.GetValue(run)!;
        runs.Add(instance,run);
        try
        {
            DungeonRunRuntime.OnQuestCompleted(player,dungeon.CompletionQuestId);Assert.That(Done(),Is.False,"Outsider");
            participants[player.Id]=0;
            DungeonRunRuntime.OnQuestCompleted(player,Guid.NewGuid());Assert.That(Done(),Is.False,"Other quest");
            player.MapId=Guid.NewGuid();
            DungeonRunRuntime.OnQuestCompleted(player,dungeon.CompletionQuestId);Assert.That(Done(),Is.False,"Outside dungeon maps");
            player.MapId=dungeon.MapIds[0];player.MapInstanceId=Guid.NewGuid();
            DungeonRunRuntime.OnQuestCompleted(player,dungeon.CompletionQuestId);Assert.That(Done(),Is.False,"Other instance");
            player.MapInstanceId=instance;
            DungeonRunRuntime.OnQuestCompleted(player,dungeon.CompletionQuestId);Assert.That(Done(),Is.True);
            typeof(DungeonRunRuntime).GetMethod("EvaluateCompletion",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[run]);
            Assert.That(type.GetField("CompletionStarted")!.GetValue(run),Is.False,"Boss and quest objectives must both pass");
            type.GetProperty("QuestCompleted")!.SetValue(run,false);
            type.GetProperty("Finished")!.SetValue(run,true);
            DungeonRunRuntime.OnQuestCompleted(player,dungeon.CompletionQuestId);Assert.That(Done(),Is.False,"Finished run");
        }
        finally { runs.Remove(instance); }
    }
}
