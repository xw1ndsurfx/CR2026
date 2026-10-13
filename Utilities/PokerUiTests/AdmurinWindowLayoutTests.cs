using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using ControlBase = Intersect.Client.Framework.Gwen.Control.Base;

internal static class AdmurinWindowLayoutTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        _ = new TestContentManager();
        var assembly = typeof(GameInterface).Assembly;
        var layout = typeof(ControlBase).GetMethod("RecurseLayout", BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (var name in new[] { "DailyRewardWindow", "DungeonPanelWindow", "UserListWindow" })
        {
            using var renderer = new MetricsRenderer();
            using var skin = new TestSkin(renderer);
            skin.Colors.Button.Normal = skin.Colors.Button.Disabled =
                skin.Colors.Button.Active = skin.Colors.Button.Hover = Color.White;
            using var canvas = new Canvas(skin) { Size = new Point(1920, 1080) };
            var type = assembly.GetType("Intersect.Client.Interface.Game." + name, true)!;
            var args = name == "UserListWindow" ? new object[] { canvas, new Action(() => { }) } : new object[] { canvas };
            var window = (WindowControl)Activator.CreateInstance(type, args)!;
            window.IsHidden = false;
            typeof(Intersect.Client.Interface.Window).GetMethod("OnPreDraw", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(window, new object[] { skin });
            for (var pass = 0; pass < 4; pass++)
            {
                layout.Invoke(window, new object[] { skin });
                layout.Invoke(canvas, new object[] { skin });
            }
            var origin = window.ToCanvas(0, 0);
            var controls = Descendants(window).ToArray();
            var targets = name switch
            {
                "DailyRewardWindow" => new[] { "Claim", "Status", "Cards" },
                "DungeonPanelWindow" => new[] { "Heading", "Subtitle", "MostClears", "Fastest", "DungeonPageNext" },
                _ => new[] { "UserListTabFriends", "UserListTabParty", "UserListBody", "UserListActions" },
            };
            foreach (var target in targets)
            {
                var control = controls.Single(child => child.Name == target);
                var position = control.ToCanvas(0, 0) - origin;
                if (position.X < 0 || position.Y < window.Titlebar.Height ||
                    position.X + control.Width > window.Width || position.Y + control.Height > window.Height - 4)
                    throw new InvalidOperationException($"{name}/{target} escapes the native window after layout: pos={position.X},{position.Y}, size={control.Width},{control.Height}, window={window.Width},{window.Height}, title={window.Titlebar.Height}");
            }
            if (controls.Any(child => child.Name is "AdmurinUserListFrame" or "AdmurinDungeonFrame" or "DungeonFallbackFrame"))
                throw new InvalidOperationException(name + " has a second outer frame");
            System.Console.WriteLine("PASS Admurin native window bounds: " + name);
        }
    }

    private static IEnumerable<ControlBase> Descendants(ControlBase parent)
    {
        foreach (var child in parent.Children)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
