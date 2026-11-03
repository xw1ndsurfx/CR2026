using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Input;
using Intersect.Client.General;
using Intersect.Client.Interface.Game;
using Intersect.Network.Packets.Server;
using Intersect.Network.Packets.WorldEvents;
using ControlBase = Intersect.Client.Framework.Gwen.Control.Base;

internal static class StatusHudControlsTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    [ModuleInitializer] internal static void Run()
    {
        try { Verify(); } catch (Exception error) { System.Console.Error.WriteLine(error); Environment.Exit(1); }
    }

    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Verify()
    {
        _ = new TestContentManager();
        var previous = Globals.Database;
        var previousContext = Intersect.Core.ApplicationContext.Context.Value;
        Intersect.Core.ApplicationContext.Context.Value = DispatchProxy.Create<Intersect.Core.IApplicationContext, ActivitiesLogProxy>();
        try
        {
            var database = new InterfaceRefreshTests.MemoryPreferences(); Globals.Database = database;
            using var renderer = new MetricsRenderer(); using var skin = new TestSkin(renderer);
            using var canvas = new Canvas(skin) { Size = new Point(1280, 720) };
            foreach (var name in new[] { "DungeonRunHudWindow", "WorldBossStatusWindow", "InvasionStatusWindow" })
            {
                var type = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game." + name, true)!;
                var hud = (ControlBase)Activator.CreateInstance(type, [canvas])!;
                object packet = name switch
                {
                    "DungeonRunHudWindow" => new DungeonRunStatePacket { Status = DungeonRunStatus.Active, RunId = Guid.NewGuid() },
                    "WorldBossStatusWindow" => new WorldBossStatusPacket { Active = true, ExpiresAtUnixMilliseconds = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds() },
                    _ => new InvasionStatusPacket { Active = true, Targets = [] },
                };
                void Apply() => type.GetMethod("Apply")!.Invoke(hud, name == "WorldBossStatusWindow" ? [packet, 1] : [packet]);
                void Update() => type.GetMethod("Update")!.Invoke(hud, null);
                Apply();
                var handle = hud.Children.Single(c => c.Name == "HudDragHandle");
                var toggle = hud.Children.OfType<Button>().Single(c => c.Name == "HudCollapse");
                handle.GetType().GetMethod("OnMouseDown", Hidden)!.Invoke(handle, [MouseButton.Left, new Point(hud.X + 5, hud.Y + 5), false]);
                handle.IsActive = true; // InputHandler sets this before dispatching native mouse movement.
                handle.GetType().GetMethod("OnMouseMoved", Hidden)!.Invoke(handle, [45, 65, 0, 0]);
                Update();
                Check(hud.X == 40 && hud.Y == 60, name + " must retain drag coordinates during frame updates");
                handle.GetType().GetMethod("OnMouseUp", Hidden)!.Invoke(handle, [MouseButton.Left, new Point(45, 65), false]);
                handle.IsActive = false;
                Apply();
                Check(hud.X == 40 && hud.Y == 60, name + " must not recenter on incoming packets");
                void Toggle() => typeof(Button).GetMethod("OnKeyReturn", Hidden)!.Invoke(toggle, [false]);
                Toggle(); Apply();
                Check(hud.Height == 36 && !handle.IsHidden && !toggle.IsHidden, name + " stays collapsed and draggable after a packet");
                if (packet is DungeonRunStatePacket dungeon)
                {
                    dungeon.Status = DungeonRunStatus.Completed; Apply(); Toggle();
                    Check(hud.Height == 126 && hud.Children.Single(c => c.Name == "DungeonRunAbandon").IsHidden,
                        "Expanding a dungeon result must never restore the abandon button");
                }
                else Toggle();
                var key = name switch { "DungeonRunHudWindow" => "ShowDungeonHud", "WorldBossStatusWindow" => "ShowWorldBossHud", _ => "ShowInvasionHud" };
                typeof(Intersect.Client.Framework.Database.GameDatabase).GetProperty(key)!.SetValue(database, false);
                Apply(); Check(hud.IsHidden, name + " packets must respect the hidden preference immediately");
                typeof(Intersect.Client.Framework.Database.GameDatabase).GetProperty(key)!.SetValue(database, true);
                Update(); Check(!hud.IsHidden, name + " can be shown again from options");
                canvas.Size = new Point(320, 200); Update();
                Check(hud.X >= 0 && hud.Y >= 0, name + " must stay accessible after a resolution change");
                canvas.Size = new Point(1280, 720);
                var recreated = (ControlBase)Activator.CreateInstance(type, [canvas])!;
                type.GetMethod("Apply")!.Invoke(recreated, name == "WorldBossStatusWindow" ? [packet, 1] : [packet]);
                Check(recreated.X == 40 && recreated.Y == 60, name + " saved position survives window reconstruction");
            }
            database.ShowDungeonHud = false; database.DungeonHudCollapsed = true; database.SavePreferences();
            var restored = new InterfaceRefreshTests.MemoryPreferences(database.Values); restored.LoadPreferences();
            Check(!restored.ShowDungeonHud && restored.DungeonHudCollapsed, "Dungeon visibility and collapse settings survive reload");
            System.Console.WriteLine("PASS status HUD controls: actual dragging, packet updates, collapse/results, visibility, screen bounds and persistence");
        }
        finally { Globals.Database = previous; Intersect.Core.ApplicationContext.Context.Value = previousContext; }
    }
}
