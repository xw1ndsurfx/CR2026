using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Core;
using Intersect.Client.General;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Framework.Core.WorldEvents.Invasions;
using Intersect.Network.Packets.WorldEvents;
using MessagePack;
using ControlBase = Intersect.Client.Framework.Gwen.Control.Base;
internal static class InvasionAlertTests
{
    [ModuleInitializer] internal static void Run() { try { Verify(); } catch(Exception e) { System.Console.Error.WriteLine(e); Environment.Exit(1); } }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static object Field(object obj, string name) => obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(obj)!;
    private static void Verify()
    {
        var date = new DateTimeOffset(2026, 10, 20, 0, 30, 0, TimeSpan.FromHours(-4));
        var definition = new InvasionDefinition { Enabled = true, StartHour = 0, StartMinute = 30, ScheduleDays = InvasionScheduleDays.Tuesday, EnvironmentLeadSeconds = 60 };
        Check(!definition.TryGetPreludeOccurrence(date.AddMinutes(-61).AddSeconds(-1), out _), "Prelude must not start before its configured lead");
        Check(definition.TryGetPreludeOccurrence(date.AddMinutes(-61), out var occurrence) && occurrence == date, "Environment begins before the one-hour warning across midnight");
        Check(definition.TryGetPreludeOccurrence(date.AddMinutes(-60), out _), "Environment is already active at the first alert");
        definition.EnvironmentLeadSeconds = 0;
        Check(!definition.TryGetPreludeOccurrence(date.AddMinutes(-60).AddSeconds(-1), out _) && definition.TryGetPreludeOccurrence(date.AddMinutes(-60), out _), "Zero lead starts ambience exactly at the first alert");
        definition.EnvironmentLeadSeconds = 60;
        Check(!definition.TryGetPreludeOccurrence(date, out _), "Prelude ends at the invasion start boundary");
        definition.Reminder60Enabled = false; definition.Reminder30Enabled = false; definition.Reminder15Enabled = false;
        Check(definition.FirstReminderMinutes == 5 && definition.TryGetPreludeOccurrence(date.AddMinutes(-6), out _), "Lead follows the first enabled warning");
        definition.Enabled = false; Check(!definition.TryGetPreludeOccurrence(date.AddMinutes(-6), out _), "Disabled invasions do not apply ambience");
        var status = MessagePackSerializer.Deserialize<InvasionStatusPacket>(MessagePackSerializer.Serialize(new InvasionStatusPacket { Active = true, Prelude = true, Music = "royal.ogg" }));
        Check(status.Prelude && status.Music == "royal.ogg", "Prelude flag and music survive packet transport");
        var oldContext = ApplicationContext.Context.Value; var oldDatabase = Globals.Database;
        ApplicationContext.Context.Value = DispatchProxy.Create<IApplicationContext, ActivitiesLogProxy>(); Globals.Database = new InterfaceRefreshTests.MemoryPreferences();
        try
        {
            foreach (var size in new[] { new Point(640,480), new Point(1280,720), new Point(1920,1080) })
            {
                _ = new TestContentManager(); using var renderer = new MetricsRenderer(); using var skin = new TestSkin(renderer); using var canvas = new Canvas(skin) { Size = size };
                var statusType = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.InvasionStatusWindow", true)!;
                var statusPanel = (ControlBase)Activator.CreateInstance(statusType, [canvas])!;
                var targetState = new InvasionStatusPacket { Active = true, ObjectiveHealth = 3200, ObjectiveMaxHealth = 3200,
                    Targets = Enumerable.Range(1,32).Select(i => new InvasionTargetStatus { Id = Guid.NewGuid(), Name = "Gate " + i, Health = 100, MaxHealth = 100 }).ToArray() };
                targetState = MessagePackSerializer.Deserialize<InvasionStatusPacket>(MessagePackSerializer.Serialize(targetState));
                statusType.GetMethod("Apply")!.Invoke(statusPanel, [targetState]);
                var details = (RichLabel)Field(statusPanel, "_targetText"); var targetScroll = (ScrollControl)Field(statusPanel, "_targets");
                Check(details.Text.Contains("Gate 32") && details.Height > targetScroll.Height, "Every objective survives transport and remains readable in the scrolling HUD");
                var disclosure = Field(statusPanel, "_disclosure");
                var collapse = disclosure.GetType().GetMethod("SetCollapsed", BindingFlags.Instance | BindingFlags.NonPublic)!;
                collapse.Invoke(disclosure, [true]); collapse.Invoke(disclosure, [false]);
                statusType.GetMethod("Update")!.Invoke(statusPanel, null);
                Check(!targetScroll.IsHidden, "Expanding the HUD restores multiple objectives");
                var type = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.InvasionAlertWindow", true)!;
                var panel = (ControlBase)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, [canvas], null)!;
                var message = "**[ROYAL ALERT]**\n" + string.Join("\n\n", Enumerable.Repeat("Hear me, brave adventurers! **Azrakel the Fallen** is approaching {island}. Gather your companions and prepare for battle!", 30)) + "\n**FOR POLONYA!**";
                var packet = new InvasionAlertPacket { InvasionId = Guid.NewGuid(), Name = "Royal invasion", Message = message, Island = "Polonya", MinutesBefore = 60 };
                packet = MessagePackSerializer.Deserialize<InvasionAlertPacket>(MessagePackSerializer.Serialize(packet));
                type.GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(panel, [packet]);
                var gameUi = (GameInterface)RuntimeHelpers.GetUninitializedObject(typeof(GameInterface));
                typeof(GameInterface).GetField("_invasionAlertWindow", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(gameUi, panel);
                Check(gameUi.SuppressStatusHuds, "Status HUDs yield to a royal alert so its paragraphs remain readable");
                var body = (RichLabel)Field(panel, "_body"); var scroll = (ScrollControl)Field(panel, "_scroll");
                Check(body.Text.Contains("FOR POLONYA!") && !body.Text.Contains("**") && body.Text.Contains("Polonya"), "Complete authored message and closing lines are retained with markdown markers removed");
                Check(body.Height > scroll.Height && body.FormattedLabels.Any(l => ReferenceEquals(l.Font, Intersect.Client.Framework.File_Management.GameContentManager.Current.GetFont("sourcesansproblack"))), "Long alerts scroll and bold passages use the emphasis font");
                scroll.UpdateScrollBars(); typeof(ScrollControl).GetMethod("OnMouseWheeled", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(scroll, [-120]);
                Check(scroll.VerticalScrollBar.ScrollAmount > 0, "Players can scroll to the final paragraphs");
                Check(panel.Width <= size.X && panel.Height <= size.Y, "Royal alert fits the viewport");
                type.GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(panel, [new InvasionAlertPacket { Name = "Second", Message = "Second warning" }]);
                Check(body.Text.Contains("FOR POLONYA!"), "Simultaneous alerts queue instead of overwriting unread messages");
                type.GetMethod("Next", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(panel, null);
                Check(body.Text.Contains("Second warning"), "Continue opens the next queued alert");
                type.GetMethod("Next", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(panel, null);
                Check(!gameUi.SuppressStatusHuds, "Status HUDs return once the last alert is closed");
            }
        }
        finally { ApplicationContext.Context.Value = oldContext; Globals.Database = oldDatabase; }
        System.Console.WriteLine("PASS invasion prelude boundaries/disabled warnings/midnight and long royal alerts, bold, scrolling, queue and packets");
    }
}
