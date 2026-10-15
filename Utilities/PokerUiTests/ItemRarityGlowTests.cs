using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Interface.Game.DescriptionWindows;
using RendererBase = Intersect.Client.Framework.Gwen.Renderer.Base;

internal static class ItemRarityGlowTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var type = typeof(ItemDescriptionWindow).Assembly.GetType(
            "Intersect.Client.Interface.Game.DescriptionWindows.ItemRarityGlow", true)!;
        var resolve = type.GetMethod("ResolveColor", BindingFlags.NonPublic | BindingFlags.Static)!;
        var draw = type.GetMethod("Draw", BindingFlags.NonPublic | BindingFlags.Static)!;
        string[] tiers = ["None", "Common", "Uncommon", "Rare", "Epic", "Legendary"];
        var red = new Color(220, 25, 40);
        var colors = Enumerable.Range(0, tiers.Length).ToDictionary(index => index, _ => red);
        for (var rarity = -1; rarity <= tiers.Length; ++rarity)
        {
            var actual = resolve.Invoke(null, [rarity, tiers, colors]);
            Check(ReferenceEquals(actual, rarity >= 3 && rarity < tiers.Length ? red : null),
                "Only Rare and higher use the configured color");
        }
        Check(resolve.Invoke(null, [2, new[] { "Common", "Rare", "Epic" }, colors]) == red,
            "Custom tier ordering must be respected");
        colors[3] = Color.Transparent;
        Check(resolve.Invoke(null, [3, tiers, colors]) == null, "Transparent rarity must not glow");
        colors.Remove(4);
        Check(resolve.Invoke(null, [4, tiers, colors]) == null, "Missing rarity color must not glow");

        using var renderer = new RecordingRenderer();
        var original = new Color(10, 20, 30);
        renderer.PushDrawColor(original);
        draw.Invoke(null, [renderer, 200, 150, red, 600L]);
        var brightAlpha = renderer.Calls.Max(call => call.Color.A);
        Check(ReferenceEquals(renderer.DrawColor, original), "Drawing must restore the renderer color stack");
        Check(renderer.Calls.Count > 0 && renderer.Calls.All(call =>
            call.Bounds.X + call.Bounds.Width <= 0 || call.Bounds.Y + call.Bounds.Height <= 0 ||
            call.Bounds.X >= 200 || call.Bounds.Y >= 150), "Halo must stay outside the tooltip content");
        Check(renderer.Calls.All(call => call.Color.R == red.R && call.Color.G == red.G && call.Color.B == red.B),
            "Glow must retain the configured rarity hue");
        renderer.Calls.Clear();
        draw.Invoke(null, [renderer, 200, 150, red, 1800L]);
        Check(renderer.Calls.Max(call => call.Color.A) < brightAlpha, "Halo must pulse over time");
        Check(red.A == 255 && red.R == 220, "Shared rarity colors must not be mutated");
        renderer.PopDrawColor();
        System.Console.WriteLine("PASS item rarity glow: tiers, configured colors, outer bounds, pulse and renderer state");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Item rarity glow: " + message);
    }

    private sealed class RecordingRenderer : RendererBase
    {
        internal readonly List<(Color Color, Rectangle Bounds)> Calls = [];
        public override void DrawFilledRect(Rectangle rectangle) => Calls.Add((new Color(DrawColor), rectangle));
    }
}
