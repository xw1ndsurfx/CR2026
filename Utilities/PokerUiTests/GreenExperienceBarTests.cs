using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Client.MiniGames;
using Intersect.Network.Packets.MiniGames;
using Newtonsoft.Json;
using Console = System.Console;
using ControlBase = Intersect.Client.Framework.Gwen.Control.Base;
using RendererBase = Intersect.Client.Framework.Gwen.Renderer.Base;

internal static class GreenExperienceBarTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        // Module initializer order is unspecified: provide the same explicit fixture first.
        _ = new TestContentManager();
        var type = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.PokerWindow", true)!;
        var draw = type.GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var fraction = type.GetField("_xpFraction", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var green = JsonConvert.SerializeObject(new Color(46, 196, 90));
        var outline = JsonConvert.SerializeObject(new Color(45, 99, 61));
        var passed = 0;
        foreach (var size in new[] { new Point(1280, 720), new Point(640, 480) })
        {
            using var renderer = new BarRenderer();
            using var skin = new TestSkin(renderer);
            using var canvas = new Canvas(skin) { Size = size };
            var scene = (ControlBase)Activator.CreateInstance(type,
                [canvas, new Action<PokerRequestKind, long>((_, _) => throw new InvalidOperationException("Unexpected network action"))])!;
            try
            {
                var layout = new PokerSceneLayout(size.X, size.Y);
                foreach (var value in new[] { 0f, 0.5f, 1f })
                {
                    renderer.Calls.Clear(); fraction.SetValue(scene, value); draw.Invoke(scene, [skin]);
                    var border = renderer.Calls.Single(c => c.Color == outline).Bounds;
                    var expectedBorder = layout.Rect(52, 710, 618, 18);
                    Check(border.X == expectedBorder.X && border.Y == expectedBorder.Y &&
                        border.Width == expectedBorder.Width && border.Height == expectedBorder.Height, "Track bounds");
                    var fills = renderer.Calls.Where(c => c.Color == green).ToArray();
                    if (value == 0) Check(fills.Length == 0, "Zero XP must not draw progress");
                    else
                    {
                        Check(fills.Length == 1, "Green fill missing");
                        var expected = layout.Rect(53, 711, (int)(616 * value), 16);
                        var actual = fills[0].Bounds;
                        Check(actual.X == expected.X && actual.Y == expected.Y &&
                            actual.Width == expected.Width && actual.Height == expected.Height, "Progress width");
                    }
                    ++passed;
                    Console.WriteLine($"PASS green XP bar: {value:P0} at {size.X}x{size.Y}");
                }
            }
            finally { type.GetMethod("Destroy")!.Invoke(scene, null); }
        }
        Check(passed == 6, "Missing green bar test cases");
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Green XP bar: " + message); }

    private sealed class BarRenderer : RendererBase
    {
        internal readonly List<(string Color, Rectangle Bounds)> Calls = [];
        public override void DrawFilledRect(Rectangle rectangle) => Calls.Add((JsonConvert.SerializeObject(DrawColor), rectangle));
        public override Point MeasureText(IFont? font, int fontSize, string? text, float scale = 1f) =>
            new((int)Math.Ceiling((text?.Length ?? 0) * fontSize * 0.6f * scale), (int)Math.Ceiling((fontSize + 3) * scale));
    }
}
