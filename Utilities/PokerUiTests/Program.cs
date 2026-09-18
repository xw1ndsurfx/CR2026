using System.Reflection;
using Intersect;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.ControlInternal;
using Intersect.Client.Interface.Game;
using Intersect.Network.Packets.MiniGames;
using ControlBase = Intersect.Client.Framework.Gwen.Control.Base;
using RendererBase = Intersect.Client.Framework.Gwen.Renderer.Base;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

// Exercise the actual PokerWindow and Gwen text layout without a GPU, game assets, network,
// or copied production classes. Synthetic font metrics are deliberate: this is a regression
// for missing font assignment / unmeasured text, NOT a visual or real-font acceptance test.
var pokerType = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.PokerWindow", true)!;
var textLayout = typeof(Text).GetMethod("Layout", BindingFlags.Instance | BindingFlags.NonPublic)!;
var textField = typeof(Label).GetField("_textElement", BindingFlags.Instance | BindingFlags.NonPublic)!;
var failures = 0;
var passed = 0;

Run("Control without a font reproduces the unmeasured text defect", () =>
{
    using var renderer = new MetricsRenderer();
    using var skin = new TestSkin(renderer);
    using var canvas = new Canvas(skin);
    using var label = new Label(canvas) { AutoSizeToContents = false, Size = new Point(300, 30), Text = "Poker caption" };
    var element = (Text)textField.GetValue(label)!;
    textLayout.Invoke(element, [skin]);
    var expected = renderer.MeasureText(skin.DefaultFont, label.FontSize, label.Text);
    Check(label.Font == null && element.Width < expected.X, "Baseline no longer reproduces missing measurement; review the fix/test.");
    label.Font = skin.DefaultFont;
    textLayout.Invoke(element, [skin]);
    Check(element.Size == expected, "Assigning the skin font did not restore measurement.");
});

foreach (var size in new[] { new Point(858, 658), new Point(800, 600), new Point(640, 480), new Point(1024, 768) })
{
    Run($"All Poker labels, buttons and amount text are measured at {size.X}x{size.Y}", () =>
    {
        using var renderer = new MetricsRenderer();
        using var skin = new TestSkin(renderer);
        using var canvas = new Canvas(skin) { Size = size };
        var window = (WindowControl)Activator.CreateInstance(pokerType,
            [canvas, new Action<PokerRequestKind, long>((_, _) => throw new InvalidOperationException("Unexpected network action"))])!;
        try
        {
            var content = Descendants(window).Single(c => c.Name == "PokerContent");
            var labels = Descendants(content).OfType<Label>().ToArray();
            Check(labels.Length >= 40, "Missing poker controls in test traversal.");
            Check(labels.OfType<Button>().Count() == 9, "Not all action buttons were covered.");
            Check(labels.OfType<TextBox>().Count() == 1, "Amount input was not covered.");
            foreach (var label in labels)
            {
                Check(ReferenceEquals(label.Font, skin.DefaultFont), $"{label.Name}: no explicit skin font.");
                if (string.IsNullOrEmpty(label.Text)) label.Text = label.Name.StartsWith("Board") ? "[AS]" : "Poker 123";
                var element = (Text)textField.GetValue(label)!;
                textLayout.Invoke(element, [skin]);
                var expected = renderer.MeasureText(label.Font, label.FontSize, label.Text);
                Check(element.Size == expected, $"{label.Name}: expected {expected}, got {element.Size}.");
                Check(element.Height > 10 && element.Width > 10, $"{label.Name}: text remains at its tiny default bounds.");
            }
            Check(labels.Where(l => l.Name.StartsWith("Board")).All(l => l.FontSize == 22), "Board card font size changed.");
            Check(labels.Single(l => l.Name == "MyCards").FontSize == 18, "Private card font size changed.");
            var call = labels.Single(l => l.Name == "Call");
            var callText = (Text)textField.GetValue(call)!;
            call.Text = "Call 999";
            textLayout.Invoke(callText, [skin]);
            Check(callText.Size == renderer.MeasureText(call.Font, call.FontSize, call.Text), "Dynamic caption not remeasured.");
            call.IsDisabled = true;
            textLayout.Invoke(callText, [skin]);
            Check(callText.Height > 10, "Disabled button lost text measurement.");
            Check(window.Width <= size.X && window.Height <= size.Y, "Window exceeds canvas.");
        }
        finally
        {
            pokerType.GetMethod("Destroy")!.Invoke(window, null);
        }
        Check(Intersect.Client.Interface.Interface.FocusComponents.Count == 0, "Amount input leaked in focus registry.");
    });
}

Console.WriteLine($"{passed}/{passed + failures} poker UI measurement test groups passed.");
Environment.ExitCode = failures == 0 ? 0 : 1;

void Run(string name, Action action)
{
    try { action(); ++passed; Console.WriteLine("PASS UI: " + name); }
    catch (Exception ex) { ++failures; Console.Error.WriteLine("FAIL UI: " + name + "\n" + ex); }
}
static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
static IEnumerable<ControlBase> Descendants(ControlBase parent)
{
    foreach (var child in parent.Children)
    {
        yield return child;
        foreach (var descendant in Descendants(child)) yield return descendant;
    }
}
sealed class TestSkin : SkinBase
{
    public TestSkin(RendererBase renderer) : base(renderer)
    {
        DefaultFont = new TestFont();
        DefaultFontSize = 12;
        Colors.Label.Normal = Color.White;
        Colors.Label.Disabled = Color.White;
        Colors.Window.TitleActive = Color.White;
        Colors.Window.TitleInactive = Color.White;
    }
}
sealed class MetricsRenderer : RendererBase
{
    public override Point MeasureText(IFont? font, int fontSize, string? text, float scale = 1f)
    {
        if (font == null) throw new InvalidOperationException("Measuring text without a font.");
        return new Point((int)Math.Ceiling((text?.Length ?? 0) * fontSize * 0.6f * scale),
            (int)Math.Ceiling((fontSize + 3) * scale));
    }
}
sealed class TestFont : IFont
{
    public string Name => "poker-test-metrics";
    public ICollection<int> SupportedSizes { get; } = new[] { 12, 18, 22 };
    public int PickBestMatchFor(int size) => size;
    public int GetNextFontSize(int startSize, int direction, int limit = 0) => startSize + direction;
}
