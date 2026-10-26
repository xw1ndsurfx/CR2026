using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Framework.Core.Archery;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Network.Packets.Server;

internal static class RoyalArcheryUiTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        _ = new TestContentManager();
        var type = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.ArcheryWindow", true)!;
        var apply = type.GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (var resolution in new[] { new Point(1280, 720), new Point(1366, 768), new Point(1920, 1080) })
        {
            using var renderer = new MetricsRenderer(); using var skin = new TestSkin(renderer);
            using var canvas = new Canvas(skin) { Size = resolution };
            var window = (WindowControl)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, [canvas], null)!;
            apply.Invoke(window, [new ArcheryStatePacket { Open = true, ConfigurationJson = new RoyalCombatConfiguration().ToJson() }]);
            if (!window.IsVisibleInTree || window.Width > resolution.X || window.Height > resolution.Y)
                throw new Exception("Royal archery does not fit the supported viewport.");
            var viewport = type.GetField("_view", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
            var render = viewport.GetType().GetMethod("Render", BindingFlags.NonPublic | BindingFlags.Instance)!;
            foreach (var phase in Enum.GetValues<ArcheryPhase>())
            {
                apply.Invoke(window, [new ArcheryStatePacket { ViewJson = JsonSerializer.Serialize(new ArcheryView {
                    MatchId = Guid.NewGuid(), Phase = phase, Distance = 25, Shot = 2, Score = 20,
                    Impacts = [new(0, 0, 10), new(.1, .1, 8)] }) }]);
                render.Invoke(viewport, [skin]);
            }
            window.Hide();
            apply.Invoke(window, [new ArcheryStatePacket { ViewJson = JsonSerializer.Serialize(new ArcheryView { Phase = ArcheryPhase.Abandoned }) }]);
            if (window.IsVisibleInTree) throw new Exception("Late server snapshots reopened a closed training window.");
        }
        foreach (var source in new[] { "Royal Archery Training", "Weapon Masteries", "Stand at the royal shooting position outside combat.",
            "Round {0}/3 · Score {1}/150 · Arrows {2} · Distance {3} m · Wind {4}", "Training complete", "Training ended" })
            foreach (var language in new[] { "fr", "es" })
                if (QualityOfLifeText.Get(source, language) == source) throw new Exception("Untranslated archery text: " + source);
        System.Console.WriteLine("PASS: royal archery client layout, phase rendering, close/late snapshot and FR/ES catalog checks.");
    }
}
