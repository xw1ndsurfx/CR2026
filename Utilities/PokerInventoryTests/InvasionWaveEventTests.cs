using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.WorldEvents.Invasions;
using Intersect.Server.Entities;

internal static class InvasionWaveEventTests
{
    [ModuleInitializer] internal static void Run()
    {
        var previous = Options.Instance;
        try { typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, new Options()); Verify(); }
        catch (Exception e) { System.Console.Error.WriteLine(e); Environment.Exit(1); }
        finally { typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, previous); }
    }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static void Verify()
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var runtime = typeof(Player).Assembly.GetType("Intersect.Server.WorldEvents.Invasions.InvasionRuntime", true)!;
        var sessionType = runtime.GetNestedType("Session", BindingFlags.NonPublic)!;
        var update = runtime.GetMethod("UpdateSession", flags)!;
        var schedule = runtime.GetMethod("ScheduleNextWave", flags)!;
        var online = (HashSet<Player>)typeof(Player).GetField("_onlinePlayers", flags)!.GetValue(null)!;
        var players = new[] { new Player { InGame = true }, new Player { InGame = true } };
        foreach (var player in players) { GC.SuppressFinalize(player); online.Add(player); }
        ConcurrentQueue<Player.StartCommonEventMetadata> Queue(Player p) => (ConcurrentQueue<Player.StartCommonEventMetadata>)typeof(Player).GetField("_queueStartCommonEvent", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(p)!;
        var events = Enumerable.Range(0,3).Select(_ => new EventDescriptor(Guid.NewGuid(), true) { Pages = [new()] }).ToArray();
        var mapEvent = new EventDescriptor(Guid.NewGuid(), false);
        foreach (var evt in events.Append(mapEvent)) EventDescriptor.Lookup[evt.Id] = evt;
        var definition = new InvasionDefinition { Waves = events.Select((evt,i) => new InvasionWaveDefinition { AfterClearCommonEventId = evt.Id, DelaySeconds = 3 + i }).ToArray() };
        var session = Activator.CreateInstance(sessionType, true)!;
        void Set(string name, object value) => sessionType.GetProperty(name)!.SetValue(session, value);
        long Deadline() => (long)sessionType.GetProperty("NextWaveAtMs")!.GetValue(session)!;
        Set("Definition", definition);
        Set("Objectives", Activator.CreateInstance(sessionType.GetProperty("Objectives")!.PropertyType)!);
        Set("NextWaveAtMs", long.MaxValue);
        try
        {
            schedule.Invoke(null, [session, 10000L]);
            Check(players.All(p => Queue(p).Count == 0), "No between-wave event before the first wave");
            Set("WaveIndex", 0); Set("LastStatusAtMs", 10000L);
            var npc = new Npc(new NPCDescriptor(Guid.NewGuid())); GC.SuppressFinalize(npc);
            var active = (IDictionary)sessionType.GetProperty("ActiveNpcs")!.GetValue(session)!;
            active.Add(npc.Id, npc);
            update.Invoke(null, [session, 10000L]);
            Check(players.All(p => Queue(p).Count == 0), "Living invaders prevent the transition event");
            npc.IsDead = true;
            update.Invoke(null, [session, 10001L]);
            update.Invoke(null, [session, 10002L]);
            Check(Deadline() == 14001 && players.All(p => Queue(p).Count == 1 && Queue(p).Last().EventDescriptor.Id == events[0].Id),
                "Clearing the last NPC launches the completed wave's event once and starts the next wave's configured delay");
            Set("WaveIndex", 1); Set("NextWaveAtMs", long.MaxValue); Set("LastStatusAtMs", 20000L);
            update.Invoke(null, [session, 20000L]);
            Check(Deadline() == 25000 && players.All(p => Queue(p).Count == 2 && Queue(p).Last().EventDescriptor.Id == events[1].Id),
                "Each completed wave can launch a different Common Event");
            Set("WaveIndex", 2); Set("NextWaveAtMs", long.MaxValue);
            schedule.Invoke(null, [session, 30000L]);
            Set("WaveIndex", 0); Set("Completed", true);
            schedule.Invoke(null, [session, 30000L]);
            Check(players.All(p => Queue(p).Count == 2), "No between-wave event after the final wave or after the invasion ends");
            Set("Completed", false);
            foreach (var id in new[] { Guid.Empty, Guid.NewGuid(), mapEvent.Id })
            {
                definition.Waves[0].AfterClearCommonEventId = id; Set("NextWaveAtMs", long.MaxValue);
                schedule.Invoke(null, [session, 40000L]);
                Check(Deadline() == 44000 && players.All(p => Queue(p).Count == 2), "Unset or deleted/non-common events do not execute or stall progression");
            }
        }
        finally
        {
            foreach (var player in players) online.Remove(player);
            foreach (var evt in events.Append(mapEvent)) EventDescriptor.Lookup.DeleteAt(evt.Id);
        }
        System.Console.WriteLine("PASS invasion wave transitions: last NPC, one event per connected player, independent waves, delay, no replay/first/final/ended invasion and invalid-event fallback");
    }
}
