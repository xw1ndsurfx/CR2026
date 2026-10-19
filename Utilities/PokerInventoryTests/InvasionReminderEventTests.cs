using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.WorldEvents.Invasions;
using Intersect.Server.Entities;

internal static class InvasionReminderEventTests
{
    [ModuleInitializer] internal static void Run()
    {
        var previous = Options.Instance;
        try
        {
            typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, new Options());
            Verify();
        }
        catch (Exception e) { System.Console.Error.WriteLine(e); Environment.Exit(1); }
        finally { typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, previous); }
    }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static void Verify()
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var runtime = typeof(Player).Assembly.GetType("Intersect.Server.WorldEvents.Invasions.InvasionRuntime", true)!;
        var checkReminder = runtime.GetMethod("CheckReminder", flags)!;
        var online = (HashSet<Player>)typeof(Player).GetField("_onlinePlayers", flags)!.GetValue(null)!;
        var keys = (IDictionary)runtime.GetField("LastReminderKeys", flags)!.GetValue(null)!;
        var players = new[] { new Player { InGame = true }, new Player { InGame = true } };
        foreach (var player in players) { GC.SuppressFinalize(player); online.Add(player); }
        ConcurrentQueue<Player.StartCommonEventMetadata> Queue(Player player) =>
            (ConcurrentQueue<Player.StartCommonEventMetadata>)typeof(Player).GetField("_queueStartCommonEvent", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(player)!;
        var minutes = new[] { 60, 30, 15, 5 };
        var events = minutes.Select(_ => new EventDescriptor(Guid.NewGuid(), true) { Pages = [new()] }).ToArray();
        var mapEvent = new EventDescriptor(Guid.NewGuid(), false);
        foreach (var evt in events.Append(mapEvent)) EventDescriptor.Lookup[evt.Id] = evt;
        var definition = new InvasionDefinition { TargetMapId = Guid.NewGuid(), StartHour = 12, StartMinute = 0 };
        var start = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.FromHours(-4));
        try
        {
            for (var i = 0; i < minutes.Length; i++)
            {
                typeof(InvasionDefinition).GetProperty($"Reminder{minutes[i]}CommonEventId")!.SetValue(definition, events[i].Id);
                var now = start.AddMinutes(-minutes[i]);
                checkReminder.Invoke(null, [definition, now.AddSeconds(-1), minutes[i], true, "Warning", ""]);
                checkReminder.Invoke(null, [definition, now, minutes[i], false, "Warning", ""]);
                Check(players.All(p => Queue(p).Count == i), "Early and disabled alerts do not launch events");
                checkReminder.Invoke(null, [definition, now, minutes[i], true, "Warning", ""]);
                checkReminder.Invoke(null, [definition, now.AddSeconds(1), minutes[i], true, "Warning", ""]);
                Check(players.All(p => Queue(p).Count == i + 1 && Queue(p).Last().EventDescriptor.Id == events[i].Id),
                    "Each alert queues its own Common Event once per connected player, even without sound");
            }
            checkReminder.Invoke(null, [definition, start.AddDays(1).AddMinutes(-60), 60, true, "Warning", ""]);
            Check(players.All(p => Queue(p).Count == 5), "The next scheduled occurrence can launch its event again");
            var run = runtime.GetMethod("RunReminderCommonEvent", flags)!;
            foreach (var invalid in new[] { Guid.Empty, Guid.NewGuid(), mapEvent.Id })
            {
                definition.Reminder60CommonEventId = invalid;
                run.Invoke(null, [definition, 60]);
                Check(players.All(p => Queue(p).Count == 5), "Unset, missing and map-only events cannot be executed as alert Common Events");
            }
        }
        finally
        {
            foreach (var player in players) online.Remove(player);
            foreach (var evt in events.Append(mapEvent)) EventDescriptor.Lookup.DeleteAt(evt.Id);
            foreach (var minute in minutes) keys.Remove((definition.Id, minute));
        }
        System.Console.WriteLine("PASS actual invasion alerts: independent Common Events, online player queues, no sound, timing, disabled/duplicate alerts and next occurrence");
    }
}
