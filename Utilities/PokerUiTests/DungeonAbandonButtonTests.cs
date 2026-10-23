using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Network.Packets.Server;
using ControlBase = Intersect.Client.Framework.Gwen.Control.Base;

internal static class DungeonAbandonButtonTests
{
    [ModuleInitializer] internal static void Run()
    {
        try { Verify(); } catch (Exception error) { System.Console.Error.WriteLine(error); Environment.Exit(1); }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    private static void Verify()
    {
        _ = new TestContentManager();
        using var renderer = new MetricsRenderer();
        using var skin = new TestSkin(renderer);
        // Reproduce a skin whose default button foreground is dark.
        skin.Colors.Button.Normal = skin.Colors.Button.Hover = skin.Colors.Button.Active = Color.Black;
        using var canvas = new Canvas(skin) { Size = new Point(1920, 1080) };
        var type = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.DungeonRunHudWindow", true)!;
        var hud = (ControlBase)Activator.CreateInstance(type, [canvas])!;
        var button = (Button)hud.Children.Single(child => child.Name == "DungeonRunAbandon");
        var apply = type.GetMethod("Apply")!;
        var state = new DungeonRunStatePacket { Status = DungeonRunStatus.Active, RunId = Guid.NewGuid() };
        apply.Invoke(hud, [state]);
        Check(!button.IsHidden && button.Bottom <= hud.Height && button.MouseInputEnabled,
            "Active dungeon exposes an accessible abandon button inside the HUD.");
        foreach (var locale in new[] { "en", "fr", "es" })
        {
            button.Text = QualityOfLifeText.Get("Abandon dungeon", locale);
            var measured = renderer.MeasureText(button.Font, button.FontSize, button.Text);
            Check(measured.X + 12 <= button.Width && measured.Y + 8 <= button.Height,
                "Abandon button caption fits without clipping in " + locale);
        }
        button.UpdateColors();
        Check(button.TextColorOverride == Color.White && button.Font != null,
            "The abandon caption has its own white foreground and explicit font, even on dark-default skins.");
        var render = button.GetType().GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var rectangles = renderer.Rectangles;
        render.Invoke(button, [skin]);
        Check(renderer.Rectangles >= rectangles + 2 && !button.ShouldDrawBackground,
            "The button draws an opaque fill and border independently of external skin textures.");
        foreach (var status in new[] { DungeonRunStatus.Completed, DungeonRunStatus.Failed, DungeonRunStatus.Abandoned })
        {
            state.Status = status; apply.Invoke(hud, [state]);
            Check(button.IsHidden && hud.Height == 126, "Results cannot offer an abandon action.");
        }
        state.Status = DungeonRunStatus.Active; state.RunId = Guid.Empty; apply.Invoke(hud, [state]);
        Check(button.IsHidden, "A missing run identifier never exposes abandon.");
        System.Console.WriteLine("PASS dungeon abandon button: locale fit, explicit font/contrast, opaque background, active/result visibility");
    }
}
