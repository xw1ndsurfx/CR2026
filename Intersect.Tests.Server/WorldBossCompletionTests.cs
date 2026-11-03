using System.Collections;
using System.Reflection;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.WorldEvents.WorldBosses;
using Intersect.Server.Entities;
using Intersect.Server.WorldEvents.WorldBosses;
using NUnit.Framework;

namespace Intersect.Tests.Server;

[TestFixture, NonParallelizable]
public sealed class WorldBossCompletionTests
{
    [Test]
    public void DeathEventSurvivesNpcStateChangesAndIsConsumedOnce()
    {
        Options.EnsureCreated();
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var runtime = typeof(WorldBossRuntime);
        var sessionType = runtime.GetNestedType("ActiveBoss", BindingFlags.NonPublic)!;
        var session = Activator.CreateInstance(sessionType)!;
        var boss = new WorldBossDefinition { Id = Guid.NewGuid() };
        var npc = new Npc(new NPCDescriptor(Guid.NewGuid())); GC.SuppressFinalize(npc);
        sessionType.GetProperty("Definition")!.SetValue(session, boss);
        sessionType.GetProperty("Npc")!.SetValue(session, npc);
        sessionType.GetProperty("ExpiresAtMs")!.SetValue(session, long.MaxValue);
        var active = (IDictionary)runtime.GetField("Active", flags)!.GetValue(null)!;
        var byNpc = runtime.GetField("ActiveByNpcId", flags)!.GetValue(null)!;
        var nextCheck = runtime.GetField("_nextCheckAt", flags)!; var previousCheck = nextCheck.GetValue(null);
        active.Add(boss.Id, session);
        byNpc.GetType().GetMethod("TryAdd")!.Invoke(byNpc, [npc.Id, session]);
        nextCheck.SetValue(null, long.MaxValue);
        try
        {
            WorldBossRuntime.OnNpcDied(npc);
            WorldBossRuntime.OnNpcDied(npc);
            Assert.That(sessionType.GetField("Defeated")!.GetValue(session), Is.EqualTo(1));
            Assert.That(npc.IsDead, Is.False, "The death event is independent of subsequently mutable NPC state.");
            WorldBossRuntime.Update(0);
            Assert.That(active.Contains(boss.Id), Is.False);
            Assert.That((bool)byNpc.GetType().GetMethod("ContainsKey")!.Invoke(byNpc, [npc.Id])!, Is.False);
            WorldBossRuntime.Update(0);
            Assert.That(active.Contains(boss.Id), Is.False, "The terminal encounter cannot finish twice.");
        }
        finally
        {
            active.Remove(boss.Id); nextCheck.SetValue(null, previousCheck);
            byNpc.GetType().GetMethod("TryRemove", [typeof(Guid), sessionType.MakeByRefType()])!.Invoke(byNpc, [npc.Id, null]);
        }
    }
}
