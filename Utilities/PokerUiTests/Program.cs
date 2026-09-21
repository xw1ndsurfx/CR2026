using System.Reflection;
using Intersect;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.ControlInternal;
using Intersect.Client.Interface.Game;
using Intersect.Client.MiniGames;
using Intersect.Network.Packets.MiniGames;
using Intersect.Network.Packets.Server;
using Console = System.Console;
using ControlBase = Intersect.Client.Framework.Gwen.Control.Base;
using RendererBase = Intersect.Client.Framework.Gwen.Renderer.Base;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

// Actual scene/Gwen controls with synthetic metrics, not a GPU/art acceptance test.
var assembly = typeof(GameInterface).Assembly;
var pokerType = assembly.GetType("Intersect.Client.Interface.Game.PokerWindow", true)!;
var modelType = assembly.GetType("Intersect.Client.MiniGames.PokerClientModel", true)!;
var textLayout = typeof(Text).GetMethod("Layout", BindingFlags.Instance | BindingFlags.NonPublic)!;
var textField = typeof(Label).GetField("_textElement", BindingFlags.Instance | BindingFlags.NonPublic)!;
var failures = 0; var passed = 0;
Run("PokerMotionSet speed presets remain deterministic", () =>
{
    var normal = new Intersect.Framework.Core.MiniGames.PokerMotionSet();
    Check(normal.Duration(400) == 400 && normal.Enabled, "Normal motion duration");
    Check((normal with { Speed = Intersect.Framework.Core.MiniGames.PokerMotionSpeed.Fast }).Duration(400) == 200, "Fast motion duration");
    Check((normal with { Speed = Intersect.Framework.Core.MiniGames.PokerMotionSpeed.Cinematic }).Duration(400) == 800, "Cinematic motion duration");
    Check(!(normal with { Speed = Intersect.Framework.Core.MiniGames.PokerMotionSpeed.Off }).Enabled, "Off motion still enabled");
});
Run("Missing font still reproduces the old tiny-text defect", () =>
{
    using var renderer = new MetricsRenderer(); using var skin = new TestSkin(renderer); using var canvas = new Canvas(skin);
    var label = new Label(canvas) { AutoSizeToContents = false, Size = new Point(300, 30), Text = "Poker caption" };
    var element = (Text)textField.GetValue(label)!;
    textLayout.Invoke(element, [skin]);
    var expected = renderer.MeasureText(skin.DefaultFont, label.FontSize, label.Text);
    Check(label.Font == null && element.Width < expected.X, "Baseline changed; review regression test");
    label.Font = skin.DefaultFont; textLayout.Invoke(element, [skin]);
    Check(element.Size == expected, "Font did not restore measurement");
});
foreach (var size in new[] { new Point(858, 658), new Point(800, 600), new Point(640, 480), new Point(1024, 768), new Point(1280, 720), new Point(1920, 1080) })
{
    Run($"Borderless scene, XP, unlocks and disposal at {size.X}x{size.Y}", () =>
    {
        using var renderer = new MetricsRenderer(); using var skin = new TestSkin(renderer); using var canvas = new Canvas(skin) { Size = size };
        var scene = (ControlBase)Activator.CreateInstance(pokerType, [canvas, new Action<PokerRequestKind, long>((_, _) => throw new InvalidOperationException("Unexpected network action"))])!;
        try
        {
            Check(scene is not WindowControl, "Poker still uses window chrome");
            var content = Descendants(scene).Single(c => c.Name == "PokerContent");
            var labels = Descendants(content).OfType<Label>().ToArray();
            Check(labels.Length >= 50, "Scene labels missing");
            Check(labels.OfType<Button>().Count() == 16, "Betting, picker or six back buttons missing");
            Check(labels.OfType<TextBox>().Count() == 1, "Wager input missing");
            foreach (var label in labels)
            {
                Check(ReferenceEquals(label.Font, skin.DefaultFont), label.Name + ": no explicit font");
                if (string.IsNullOrEmpty(label.Text)) label.Text = label.Name.StartsWith("Board") ? "[AS]" : "Poker 123";
                var element = (Text)textField.GetValue(label)!; textLayout.Invoke(element, [skin]);
                var expected = renderer.MeasureText(label.Font, label.FontSize, label.Text);
                Check(element.Size == expected && element.Height > 10 && element.Width > 10, label.Name + ": unmeasured text");
            }
            var design = new PokerSceneLayout(size.X, size.Y);
            Check(labels.Where(l => l.Name.StartsWith("Board")).All(l => l.FontSize == design.FontSize(22)), "Board scaling wrong");
            Check(labels.Single(l => l.Name == "MyCards").FontSize == design.FontSize(18), "Private card font scaling wrong");
            var call = labels.Single(l => l.Name == "Call"); var callText = (Text)textField.GetValue(call)!;
            call.Text = "Call 999"; textLayout.Invoke(callText, [skin]);
            Check(callText.Size == renderer.MeasureText(call.Font, call.FontSize, call.Text), "Dynamic caption not remeasured");
            call.IsDisabled = true; textLayout.Invoke(callText, [skin]); Check(callText.Height > 10, "Disabled font measurement lost");
            Check(scene.Width == size.X && scene.Height == size.Y, "Scene does not fit canvas");
            foreach (var button in labels.OfType<Button>())
                Check(button.X >= 0 && button.Y >= 0 && button.X + button.Width <= button.Parent!.Width && button.Y + button.Height <= button.Parent.Height,
                    button.Name + ": hitbox outside its panel");
            var model = Activator.CreateInstance(modelType)!;
            var player = Guid.NewGuid(); var npc = Guid.NewGuid();
            var state = new PokerTableState
            {
                HandId = 1, Revision = 1, Stage = PokerStage.PreFlop, ActingSeat = 2, DealerSeat = 5,
                CanRaise = true, MinimumRaiseTo = 20, MaximumRaiseTo = 1000, MyCards = [1, 2],
                Seats = [new() { PlayerId = player, Seat = 2, Name = "Alice", Chips = 1000, InHand = true },
                    new() { PlayerId = npc, Seat = 5, Name = "Marlow", Chips = 1000, InHand = true }],
                NpcIds = [npc], DealerNpcId = npc,
                Decisions = [new() { Sequence = 1, PlayerId = npc, Name = "Marlow", Action = "check" }],
            };
            var packet = new PokerStatePacket { TableInstanceId = Guid.NewGuid(), ViewId = Guid.NewGuid(), PlayerId = player,
                Sequence = 1, TableName = "test-table", ServerUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), State = state };
            Check((bool)modelType.GetMethod("Apply")!.Invoke(model, [packet, player, Environment.TickCount64])!, "Fixture packet rejected");
            pokerType.GetMethod("Update")!.Invoke(scene, [model]);
            Check(labels.Single(l => l.Name == "SeatName0").Text.Contains("Alice"), "Local player not at bottom");
            Check(labels.Single(l => l.Name == "SeatName3").Text.Contains("Marlow"), "NPC name/order wrong");
            Check(labels.Single(l => l.Name == "SeatDecision3").Text.Contains("Check", StringComparison.OrdinalIgnoreCase), "NPC decision not bound");
            Check(!labels.Single(l => l.Name == "BackChoice0").IsDisabled && labels.Single(l => l.Name == "BackChoice1").IsDisabled, "Initial level gate missing");
            state.Experience = 1000; state.Wins = 40; packet.Sequence = 2;
            Check((bool)modelType.GetMethod("Apply")!.Invoke(model, [packet, player, Environment.TickCount64])!, "XP packet rejected");
            pokerType.GetMethod("Update")!.Invoke(scene, [model]);
            Check(!labels.Single(l => l.Name == "BackChoice1").IsDisabled && labels.Single(l => l.Name == "BackChoice2").IsDisabled, "Level-five unlock wrong");
            Check(labels.Single(l => l.Name == "Experience").Text.Contains("5"), "XP not visible");
            pokerType.GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(scene, [skin]);
            Check(renderer.Rectangles > 100, "Table fallback not drawn");
            canvas.Size = new Point(Math.Max(640, size.X - 60), Math.Max(480, size.Y - 40));
            pokerType.GetMethod("ResizeToCanvas")!.Invoke(scene, null);
            Check(scene.Size == canvas.Size, "Runtime resize ignored");
            var overlays = canvas.Children.Where(c => c.Name.StartsWith("PokerVictory")).ToArray();
            Check(overlays.Length == 2 && overlays.All(c => !c.MouseInputEnabled && !c.KeyboardInputEnabled), "Victory overlay captures input");
        }
        finally { pokerType.GetMethod("Destroy")!.Invoke(scene, null); }
        Check(Intersect.Client.Interface.Interface.FocusComponents.Count == 0, "Wager input leaked");
        Check(!canvas.Children.Any(c => c.Name.StartsWith("PokerVictory")) && !canvas.Children.Contains(scene), "Destroyed scene/overlay still attached");
        pokerType.GetMethod("Destroy")!.Invoke(scene, null);
    });
}
Console.WriteLine($"{passed}/{passed + failures} poker UI scene test groups passed."); Environment.ExitCode = failures == 0 ? 0 : 1;
void Run(string name, Action test)
{ try { test(); ++passed; Console.WriteLine("PASS UI: " + name); } catch (Exception ex) { ++failures; Console.Error.WriteLine($"{ex.GetBaseException().GetType().Name}: {name}: {ex.GetBaseException().Message}\nFAIL UI: {ex}"); } }
static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
static IEnumerable<ControlBase> Descendants(ControlBase parent)
{ foreach (var child in parent.Children) { yield return child; foreach (var descendant in Descendants(child)) yield return descendant; } }
sealed class TestSkin : SkinBase
{
    public TestSkin(RendererBase renderer) : base(renderer)
    { DefaultFont = new TestFont(); DefaultFontSize = 12; Colors.Label.Normal = Colors.Label.Disabled = Color.White; Colors.Window.TitleActive = Colors.Window.TitleInactive = Color.White; }
}
sealed class MetricsRenderer : RendererBase
{
    public int Rectangles;
    public override void DrawFilledRect(Rectangle rectangle) { ++Rectangles; }
    public override Point MeasureText(IFont? font, int fontSize, string? text, float scale = 1f)
    {
        if (font == null) throw new InvalidOperationException("Measuring without a font");
        return new Point((int)Math.Ceiling((text?.Length ?? 0) * fontSize * 0.6f * scale), (int)Math.Ceiling((fontSize + 3) * scale));
    }
}
sealed class TestFont : IFont
{
    public string Name => "poker-test-metrics";
    public ICollection<int> SupportedSizes { get; } = new[] { 10, 12, 18, 22 };
    public int PickBestMatchFor(int size) => size;
    public int GetNextFontSize(int startSize, int direction, int limit = 0) => startSize + direction;
}
