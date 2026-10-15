using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Framework.Database;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Input;
using Intersect.Client.General;
using Intersect.Client.Interface.Game;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.MiniGames.Cooking;
using Intersect.Network.Packets.MiniGames;
using Intersect.Network.Packets.Server;
using ControlBase = Intersect.Client.Framework.Gwen.Control.Base;

internal static class InterfaceRefreshTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    [ModuleInitializer]
    internal static void Run()
    {
        try { RunCore(); } catch (Exception exception) { System.Console.Error.WriteLine(exception); Environment.Exit(1); }
    }

    private static void RunCore()
    {
        _ = new TestContentManager();
        var assembly = typeof(GameInterface).Assembly;
        var oldDatabase = Globals.Database;
        try
        {
            var database = new MemoryPreferences(); Globals.Database = database;
            database.LoadPreferences();
            Check(database.ShowMyLevel && database.ShowQuestHud && database.ShowWorldBossHud, "New settings default to visible");
            database.ShowPlayerLevels = false; database.ShowMyGuild = false; database.ShowQuestHud = false;
            database.ShowInvasionHud = false; database.SavePreferences();
            var restored = new MemoryPreferences(database.Values); restored.LoadPreferences();
            Check(!restored.ShowPlayerLevels && !restored.ShowMyGuild && !restored.ShowQuestHud && !restored.ShowInvasionHud,
                "Display settings must survive reload");
            using var renderer = new MetricsRenderer(); using var skin = new TestSkin(renderer);
            using var canvas = new Canvas(skin) { Size = new Point(1024, 720) };
            Check(!Intersect.Client.Interface.Interface.HasInGameUI, "Login regression fixture must precede GameUi assignment");
            // Exercise the actual constructor and its first Update, as during login.
            var minimap = (ControlBase)Activator.CreateInstance(
                assembly.GetType("Intersect.Client.Interface.Game.MinimapHud", true)!, [canvas, new Action(() => { })])!;
            Check(minimap.IsHidden, "Minimap must initialize safely before GameUi/player exists");
            var boss = (ControlBase)Activator.CreateInstance(
                assembly.GetType("Intersect.Client.Interface.Game.WorldBossStatusWindow", true)!, [canvas])!;
            boss.GetType().GetMethod("Update")!.Invoke(boss, null);
            boss.GetType().GetMethod("Apply")!.Invoke(boss,
                [new Intersect.Network.Packets.WorldEvents.WorldBossStatusPacket
                { Active = true, ExpiresAtUnixMilliseconds = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds() }, 1]);
            var invasion = (ControlBase)Activator.CreateInstance(
                assembly.GetType("Intersect.Client.Interface.Game.InvasionStatusWindow", true)!, [canvas])!;
            invasion.GetType().GetMethod("Update")!.Invoke(invasion, null);
            invasion.GetType().GetMethod("ShowReminder")!.Invoke(invasion, ["Invasion soon", 30_000L]);
            database.ShowInvasionHud = true;
            invasion.GetType().GetMethod("Update")!.Invoke(invasion, null);
            var hud = new ControlBase(canvas) { Size = new Point(280, 315) };
            var title = new Label(hud) { Size = new Point(220, 24) };
            var content = new Label(hud); var initiallyHidden = new Label(hud) { IsHidden = true };
            var disclosureType = assembly.GetType("Intersect.Client.Interface.Game.HudDisclosure", true)!;
            _ = Activator.CreateInstance(disclosureType, BindingFlags.Instance | BindingFlags.NonPublic, null,
                [hud, title, "MinimapCollapsed"], null);
            var toggle = hud.Children.OfType<Button>().Single();
            void ClickToggle() => typeof(Button).GetMethod("OnKeyReturn", Hidden)!.Invoke(toggle, [false]);
            ClickToggle();
            Check(hud.Height == 36 && content.IsHidden && !title.IsHidden && toggle.Text == "+", "HUD collapse and button label");
            database.SavePreferences();
            Check(database.MinimapCollapsed && database.LoadPreference("MinimapCollapsed", false), "Saving settings must preserve the collapsed HUD state");
            ClickToggle();
            Check(hud.Height == 315 && !content.IsHidden && initiallyHidden.IsHidden && toggle.Text == "-", "HUD expansion restores original visibility");

            var requests = new List<CookingActionInput>();
            var worktopType = assembly.GetType("Intersect.Client.Interface.Game.CookingWorktop", true)!;
            var worktop = (ControlBase)Activator.CreateInstance(worktopType, [canvas, new Action<CookingActionInput>(requests.Add)])!;
            worktop.SetBounds(0, 0, 650, 350);
            var configure = worktopType.GetMethod("Configure", Hidden)!;
            var down = worktopType.GetMethod("OnMouseDown", Hidden)!;
            var up = worktopType.GetMethod("OnMouseUp", Hidden)!;
            var move = worktopType.GetMethod("OnMouseMoved", Hidden)!;
            var state = new CookingSessionState { RecipeId = Guid.NewGuid(), StageIndex = 0, StageType = CookingStageType.Chop,
                YourTurn = true, StageDurationMs = 30_000, StageDifficulty = 1, StageStartedUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                RecipeIngredients = [new() { ItemId = Guid.NewGuid(), Name = "Carrot", Needed = 1, Available = 1 }] };
            void Configure() => configure.Invoke(worktop, [state, true, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - Environment.TickCount64]);
            void Mouse(MethodInfo method, int x, int y, MouseButton button = MouseButton.Left) => method.Invoke(worktop, [button, new Point(x, y), true]);
            void Place() { Mouse(down, 50, 175); Mouse(up, 400, 155); }
            Configure(); Mouse(down, 400, 155);
            Check(requests.Count == 0, "Chopping requires placing the actual ingredient first");
            Place(); Mouse(down, 400, 155); Mouse(down, 400, 155);
            Check(requests.SequenceEqual(new[] { CookingActionInput.Primary }), "One chop and input lock before acknowledgement");
            state.StageIndex++; state.StageType = CookingStageType.Heat; Configure();
            worktopType.GetMethod("OnMouseWheeled", Hidden)!.Invoke(worktop, [120]);
            Check(requests[^1] == CookingActionInput.Primary, "Wheel raises the stove heat");
            Configure(); Mouse(down, 200, 290); Check(requests[^1] == CookingActionInput.Secondary, "Stove lower-heat target");
            state.StageIndex++; state.StageType = CookingStageType.Stir; Configure(); Place();
            Mouse(down, 470, 156); move.Invoke(worktop, [450, 206, -20, 50]); move.Invoke(worktop, [395, 235, -55, 29]);
            Check(requests[^1] == CookingActionInput.Primary, "Stir gesture sends the first rhythm action");
            state.CompletedActions = 1; Configure();
            move.Invoke(worktop, [340, 206, -55, -29]); move.Invoke(worktop, [320, 156, -20, -50]);
            Check(requests[^1] == CookingActionInput.Secondary, "Stir continues after acknowledgement using alternating rhythm");
            state.StageIndex++; state.StageType = CookingStageType.Plate; Configure();
            Mouse(down, 50, 175); Mouse(up, 550, 155);
            Check(requests[^1] == CookingActionInput.Tertiary, "Dropping onto the right plate chooses the right zone");
            state.StageIndex++; state.StageType = CookingStageType.Knead; state.CompletedActions = 0; Configure(); Place();
            Mouse(down, 400, 156); move.Invoke(worktop, [300, 156, -100, 0]);
            Check(requests[^1] == CookingActionInput.Primary, "Left knead gesture");
            state.CompletedActions = 1; Configure(); move.Invoke(worktop, [410, 156, 110, 0]);
            Check(requests[^1] == CookingActionInput.Secondary, "Right knead gesture");
            state.StageIndex++; state.StageType = CookingStageType.Flip; Configure(); Place();
            Mouse(down, 400, 156); Mouse(up, 400, 60);
            Check(requests[^1] == CookingActionInput.Primary, "Upward flick flips the food");
            state.StageIndex++; state.StageType = CookingStageType.Season; Configure();
            Mouse(down, 400, 156, MouseButton.Right);
            Check(requests[^1] == CookingActionInput.Secondary, "Right-click removes seasoning");
            Configure(); Mouse(down, 400, 156); move.Invoke(worktop, [500, 156, 100, 0]);
            Check(requests[^1] == CookingActionInput.Primary, "Dragging the shaker adds seasoning");
            foreach (var stage in Enum.GetValues<CookingStageType>())
            {
                state.StageType = stage;
                worktopType.GetMethod("Render", Hidden)!.Invoke(worktop, [skin]);
            }
            configure.Invoke(worktop, [state, false, 0L]); var count = requests.Count;
            Mouse(down, 50, 175); Mouse(up, 250, 155);
            Check(requests.Count == count, "Partner turn / pending state must block actions");

            var windowType = assembly.GetType("Intersect.Client.Interface.Game.CookingWindow", true)!;
            var modelType = assembly.GetType("Intersect.Client.MiniGames.CookingClientModel", true)!;
            var model = Activator.CreateInstance(modelType)!;
            var packet = new CookingStatePacket { Sequence = 1, ServerUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), State = state };
            modelType.GetProperty("Current")!.SetValue(model, packet);
            var window = (ControlBase)Activator.CreateInstance(windowType, [canvas,
                new Action<CookingRequestKind, Guid, Guid, bool, CookingActionInput>((_, _, _, _, _) => { })])!;
            var update = windowType.GetMethod("Update")!;
            update.Invoke(window, [model]);
            var offset = windowType.GetField("_serverOffset", Hidden)!;
            var before = offset.GetValue(window);
            Thread.Sleep(25); update.Invoke(window, [model]);
            Check(Equals(before, offset.GetValue(window)), "Kitchen clock must keep advancing between server snapshots");
            foreach (var size in new[] { new Point(640, 480), new Point(1920, 1080) })
            {
                canvas.Size = size; windowType.GetMethod("ResizeToCanvas")!.Invoke(window, null);
                Check(window.Children.All(child => child.X >= 0 && child.Y >= 0 && child.Right <= window.Width && child.Bottom <= window.Height),
                    "Kitchen controls must remain within the canvas");
            }
            windowType.GetMethod("Destroy")!.Invoke(window, null);

            var small = EventGraphicPlacement.TopLeft(100, 200, 16, 24, 32, 32);
            var large = EventGraphicPlacement.TopLeft(100, 200, 96, 128, 32, 32);
            Check(small.X + 8 == 116 && small.Y + 24 == 232 && large.X + 48 == 116 && large.Y + 128 == 232,
                "All event graphic sizes must share the event tile's bottom-center anchor");
            var layoutType = assembly.GetType("Intersect.Client.Entities.NameplateLayout", true)!;
            var create = layoutType.GetMethod("Create", BindingFlags.NonPublic | BindingFlags.Static)!;
            var compact = create.Invoke(null, [100f, 0, 12f, 0f, 0f, 0f, 0f])!;
            float Row(object layout, string key) => (float)layoutType.GetProperty(key)!.GetValue(layout)!;
            Check(Row(compact, "Name") == 84, "Names must sit four pixels above the graphic");
            var stacked = create.Invoke(null, [100f, 8, 12f, 10f, 11f, 9f, 10f])!;
            Check(Row(stacked, "Footer") + 10 <= 86 && Row(stacked, "Name") + 12 < Row(stacked, "Footer") &&
                Row(stacked, "Header") + 11 < Row(stacked, "Name") && Row(stacked, "Afk") + 9 < Row(stacked, "Header") &&
                Row(stacked, "Guild") + 10 < Row(stacked, "Afk"), "Visible labels and HP bar must form a compact non-overlapping stack");
            TestNewsBanner(assembly, canvas);
            var texture = DispatchProxy.Create<Intersect.Client.Framework.Graphics.IGameTexture, CpuFrameTexture>();
            var paddingMethod = assembly.GetType("Intersect.Client.Entities.SpriteVisibleBounds", true)!
                .GetMethod("TopPadding", BindingFlags.Static | BindingFlags.NonPublic)!;
            var source = new Intersect.Client.Framework.GenericClasses.FloatRect(0, 0, 16, 32);
            Check((int)paddingMethod.Invoke(null, [texture, source])! == 10, "Transparent padding must not push the label away from the visible graphic");
            var calls = ((CpuFrameTexture)(object)texture).Reads;
            paddingMethod.Invoke(null, [texture, source]);
            Check(((CpuFrameTexture)(object)texture).Reads == calls, "Visible sprite bounds must be cached per frame");
            var padded = DispatchProxy.Create<Intersect.Client.Framework.Graphics.IGameTexture, CpuFrameTexture>();
            var pixels = (CpuFrameTexture)(object)padded;
            pixels.Left = 22; pixels.Top = 12; pixels.Right = 46; pixels.Bottom = 43;
            var frame = new Intersect.Client.Framework.GenericClasses.FloatRect(0, 0, 64, 64);
            var bounds = (GraphicBounds)assembly.GetType("Intersect.Client.Entities.SpriteVisibleBounds", true)!
                .GetMethod("Get", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [padded, frame])!;
            Check(bounds == new GraphicBounds(22, 12, 24, 31), "Visible bounds include transparent side and bottom margins");
            var placed = EventGraphicPlacement.VisibleTopLeft(100, 200, bounds, 32, 32);
            Check(placed.X + bounds.X + bounds.Width / 2f == 116 && placed.Y + bounds.Y + bounds.Height == 232,
                "Visible event feet must sit on E instead of the transparent frame boundary");
            var eventFixture = (Intersect.Client.Entities.Events.Event)RuntimeHelpers.GetUninitializedObject(typeof(Intersect.Client.Entities.Events.Event));
            GC.SuppressFinalize(eventFixture);
            var destination = (Intersect.Client.Framework.GenericClasses.FloatRect)eventFixture.GetType()
                .GetMethod("SpriteDestination", Hidden)!.Invoke(eventFixture, [padded, frame])!;
            Check(destination.X + bounds.X + bounds.Width / 2f == 0 && destination.Y + bounds.Y + bounds.Height == 0,
                "Client event drawing must use the shared visible anchor");
            System.Console.WriteLine("PASS interface refresh: persisted display options, HUD collapse, cooking gestures/input gates, advancing clock, responsive controls and event anchors");
        }
        finally { Globals.Database = oldDatabase; }
    }

    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void TestNewsBanner(Assembly assembly, Canvas canvas)
    {
        var newsType = assembly.GetType("Intersect.Client.Interface.Game.LogiklikNewsWindow", true)!;
        var feed = "[{\"category\":\"Corps Royaux\",\"title\":\"Old news\",\"published_at\":\"2026-01-01\",\"image\":\"https://news.logiklik.com/old.png\"}," +
            "{\"category\":\"Corps Royaux\",\"title\":\"Latest news\",\"published_at\":\"2026-10-15\",\"image\":\"https://news.logiklik.com/latest.png\"}]";
        var items = (System.Collections.IList)newsType.GetMethod("ParseFeed", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [feed])!;
        var latest = items[0]!;
        Check((string)latest.GetType().GetProperty("Title")!.GetValue(latest)! == "Latest news", "Shop feed must choose the latest article");
        var urls = (List<string>)latest.GetType().GetProperty("ImageUrls")!.GetValue(latest)!;
        Check(urls[0].EndsWith("latest.png"), "Latest article image selection");
        var preview = newsType.GetField("_preview", BindingFlags.Static | BindingFlags.NonPublic)!;
        var expires = newsType.GetField("_previewExpires", BindingFlags.Static | BindingFlags.NonPublic)!;
        var oldPreview = preview.GetValue(null); var oldExpires = expires.GetValue(null);
        try
        {
            preview.SetValue(null, Activator.CreateInstance(preview.FieldType, ["Latest news", urls[0], null]));
            expires.SetValue(null, DateTime.UtcNow.AddMinutes(5));
            var shopType = assembly.GetType("Intersect.Client.Interface.Game.LogiCoinShopWindow", true)!;
            var shop = (ControlBase)Activator.CreateInstance(shopType, [canvas])!;
            try
            {
                ((Task)shopType.GetMethod("RefreshNewsBannerAsync", Hidden)!.Invoke(shop, null)!).GetAwaiter().GetResult();
                var caption = (Label)shopType.GetField("_newsCaption", Hidden)!.GetValue(shop)!;
                var card = (Button)shopType.GetField("_newsCard", Hidden)!.GetValue(shop)!;
                Check(caption.Text == "Latest news" && card.Width > 190 && card.MouseInputEnabled,
                    "News banner must use the latest title and remain clickable without an image");
            }
            finally { canvas.RemoveChild(shop, false); shop.Dispose(); }
        }
        finally { preview.SetValue(null, oldPreview); expires.SetValue(null, oldExpires); }
    }
    private sealed class MemoryPreferences(Dictionary<string, string>? values = null) : GameDatabase
    {
        internal readonly Dictionary<string, string> Values = values ?? [];
        public override bool LoadConfig() => true;
        public override bool HasPreference(string key) => Values.ContainsKey(key);
        public override void DeletePreference(string key) => Values.Remove(key);
        public override string LoadPreference(string key) => Values.GetValueOrDefault(key, "");
        public override void SavePreference<TValue>(string key, TValue value) => Values[key] = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "";
    }
}

public class CpuFrameTexture : DispatchProxy
{
    public int Reads;
    public int Left, Top = 10, Right = 64, Bottom = 64;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method?.Name is "get_Width" or "get_Height") return 64;
        if (method?.Name == "GetPixel")
        {
            ++Reads; var x = (int)args![0]!; var y = (int)args[1]!;
            return new Color(x >= Left && x < Right && y >= Top && y < Bottom ? 255 : 0, 255, 255, 255);
        }
        return null;
    }
}
