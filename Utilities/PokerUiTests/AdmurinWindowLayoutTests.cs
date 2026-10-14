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
        foreach (var name in new[] { "DailyRewardWindow", "DungeonPanelWindow", "DungeonConfirmationWindow", "LogiCoinShopWindow", "UserListWindow" })
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
                "DungeonConfirmationWindow" => new[] { "DungeonGateTitle", "DungeonGateNameValue", "EnterDungeon", "CancelDungeon" },
                "LogiCoinShopWindow" => new[] { "ShopCatalog", "BuyLogiCoins", "ShopBalance", "CatalogPrevious", "CatalogNext", "ShopStatus" },
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
            if (controls.Any(child => child.Name is "AdmurinUserListFrame" or "AdmurinDungeonFrame" or "DungeonFallbackFrame" or "DungeonGateFrame" or "AdmurinShopFrame"))
                throw new InvalidOperationException(name + " has a second outer frame");
            if (name == "UserListWindow")
            {
                typeof(Options).GetMethod("EnsureCreated", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null);
                var guildPage = Enum.Parse(type.GetNestedType("Page", BindingFlags.NonPublic)!, "Guild");
                type.GetMethod("OpenPage")!.Invoke(window, new[] { guildPage });
                if (!Descendants(window).Any(child => child.Name == "FindGuild" && !child.IsHidden))
                    throw new InvalidOperationException("Guild-less player cannot find a guild");

                var legacyType = assembly.GetType("Intersect.Client.Interface.Game.GuildWindow", true)!;
                var legacy = (WindowControl)Activator.CreateInstance(legacyType, new object[] { canvas })!;
                legacy.IsHidden = false;
                var menuType = assembly.GetType("Intersect.Client.Interface.Game.MenuContainer", true)!;
                var menu = RuntimeHelpers.GetUninitializedObject(menuType);
                menuType.GetField("_guildWindow", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(menu, legacy);
                menuType.GetField("_userListWindow", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(menu, window);
                // This is the exact cleanup called on every frame for a guild-less player.
                for (var update = 0; update < 5; update++) menuType.GetMethod("HideGuildWindow")!.Invoke(menu, null);
                if (window.IsHidden || !legacy.IsHidden)
                    throw new InvalidOperationException("Member-only guild cleanup closed the Find a Guild page");
                var search = (TextBox)legacyType.GetField("_textboxSearch", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(legacy)!;
                Intersect.Client.Interface.Interface.FocusComponents.Remove(search);
                System.Console.WriteLine("PASS guild-less User List remains open with Find a Guild");
            }
            System.Console.WriteLine("PASS Admurin native window bounds: " + name);
            if (name == "DailyRewardWindow")
            {
                var state = new Intersect.Network.Packets.Server.DailyRewardStatePacket
                {
                    Enabled = true, CycleDays = 10, CurrentDay = 2, LastClaimedDay = 1,
                };
                type.GetMethod("Apply")!.Invoke(window, new object[] { state });
                var viewport = Descendants(window).Single(child => child.Name == "Cards");
                var cards = Descendants(viewport).Where(child => child.GetType().Name == "RewardCard").ToArray();
                if (cards.Length != 10 || cards.Any(card => card.Y + card.Height > viewport.Height))
                    throw new InvalidOperationException("The ten-day reward grid does not fit the compact window");
                System.Console.WriteLine("PASS all ten daily rewards fit without clipping");
            }
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
