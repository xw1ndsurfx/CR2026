using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Core;
using Intersect.Client.Entities;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Interface.Game;
using Intersect.Client.Maps;
using Intersect.Configuration;
using Intersect.Enums;
using Intersect.Framework.Core.Tutorials;
using Intersect.Network.Packets.Tutorials;

internal static class WorldGuidanceChecks
{
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly Type Marker = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.TutorialWorldMarker", true)!;
    private static object? Call(object target, string name, params object?[] args) => target.GetType().GetMethod(name, Hidden)!.Invoke(target, args);
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static T Empty<T>() where T : class { var value = (T)RuntimeHelpers.GetUninitializedObject(typeof(T)); GC.SuppressFinalize(value); return value; }

    internal static int Run()
    {
        typeof(Options).GetMethod("EnsureCreated", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null);
        using var renderer = new MetricsRenderer(); using var skin = new TestSkin(renderer);
        using var canvas = new Canvas(skin) { Size = new Point(640, 480) };
        var marker = (Base)Activator.CreateInstance(Marker, Hidden, null, [canvas], null)!;
        var oldPlayer = Globals.Me; var oldView = Graphics.CurrentView;
        var map = new MapInstance(Guid.NewGuid());
        MapInstance.Lookup.Set(map.Id, map);
        typeof(MapInstance).GetProperty("IsLoaded")!.SetValue(map, true);
        var player = Empty<Player>(); player.Id = Guid.NewGuid(); player.MapId = map.Id; player.LatestMap = map;
        typeof(Entity).GetField("mOrigin", Hidden)!.SetValue(player, new Vector2(100, 100));
        Globals.Me = player;
        var npc = new Entity(Guid.NewGuid(), null, EntityType.GlobalEntity) { MapId = map.Id, LatestMap = map, NpcDescriptorId = Guid.NewGuid() };
        GC.SuppressFinalize(npc);
        typeof(Entity).GetField("mOrigin", Hidden)!.SetValue(npc, new Vector2(150, 180));
        Globals.Entities[npc.Id] = npc;
        try
        {
            Check(!marker.MouseInputEnabled && !marker.KeyboardInputEnabled, "World guide intercepts input");
            var step = new TutorialStep { MapId = map.Id, TargetNpcId = npc.NpcDescriptorId, HighlightTarget = TutorialHighlightTarget.WorldNpc };
            Graphics.CurrentView = new FloatRect(0, 0, 640, 480);
            Call(marker, "SetStep", step);
            object?[] args = [null];
            Check((bool)Call(marker, "TryGetWorldAnchor", args)!, "Exact NPC not resolved");
            Check((Vector2)args[0]! == new Vector2(150, 180), "Marker is not at NPC feet");
            typeof(Entity).GetField("mOrigin", Hidden)!.SetValue(npc, new Vector2(180, 210));
            Call(marker, "TryGetWorldAnchor", args);
            Check((Vector2)args[0]! == new Vector2(180, 210), "Marker did not follow moving NPC");
            npc.IsHidden = true; Call(marker, "RefreshVisibility"); Check(marker.IsHidden, "Hidden NPC revealed by marker");
            npc.IsHidden = false; step.TargetNpcId = Guid.NewGuid(); Call(marker, "RefreshVisibility"); Check(marker.IsHidden, "Different descriptor matched");
            step.HighlightTarget = TutorialHighlightTarget.WorldTile; step.TargetX = 8; step.TargetY = 9;
            Call(marker, "SetStep", step); Call(marker, "TryGetWorldAnchor", args);
            Check((Vector2)args[0]! == new Vector2(map.X + 8.5f * Options.Instance.Map.TileWidth, map.Y + 9.5f * Options.Instance.Map.TileHeight), "Tile center is wrong");
            Call(marker, "Render", skin); Check(renderer.Rectangles > 0, "Missing-art fallback drew nothing");
            Graphics.CurrentView = new FloatRect(3000, 3000, 640, 480);
            Check(Call(marker, "GetTargetBounds") == null, "Offscreen destination treated as visible");
            renderer.Rectangles = 0; Call(marker, "Render", skin); Check(renderer.Rectangles > 0, "Offscreen pointer absent");
            step.TargetX = Options.Instance.Map.MapWidth; Call(marker, "RefreshVisibility"); Check(marker.IsHidden, "Invalid destination drawn");
            step.TargetX = 8; typeof(MapInstance).GetProperty("IsLoaded")!.SetValue(map, false); Call(marker, "RefreshVisibility"); Check(marker.IsHidden, "Unloaded map drew stale marker");
            step.HighlightTarget = TutorialHighlightTarget.None; Call(marker, "SetStep", step); Check(marker.IsHidden, "Explicit None still draws marker");
            var project = Marker.GetMethod("ProjectPoint", BindingFlags.Static | BindingFlags.NonPublic)!;
            var projected = (Vector2)project.Invoke(null, [new Vector2(300, 200), new Vector2(100, 80), .5f, 2f])!;
            Check(projected == new Vector2(50, 30), "Camera zoom or UI scaling applied incorrectly");
            System.Console.WriteLine("PASS world guide: exact/moving/hidden NPC, tile, absent map, offscreen, input, missing-art fallback and projection");
        }
        finally
        {
            Globals.Entities.Remove(npc.Id, out _); MapInstance.Lookup.DeleteAt(map.Id);
            Globals.Me = oldPlayer; Graphics.CurrentView = oldView; marker.Parent = null; marker.Dispose();
        }

        var cueType = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.TutorialAudioCue", true)!;
        var cue = Activator.CreateInstance(cueType, true)!;
        var config = TutorialConfiguration.CreateDefault();
        TutorialStatePacket State(int i, bool open = true, string revision = "v1") => new() { StepId = i < 0 ? Guid.Empty : config.Steps[i].Id, Open = open, Revision = revision, Finished = i < 0 };
        string Observe(TutorialStatePacket state) => (string)Call(cue, "Observe", state, config)!;
        Check(Observe(State(0)).Contains("objective"), "No new-guide cue");
        Check(Observe(State(0)) == "", "Repeated packet repeats cue");
        Check(Observe(State(0, false)) == "" && Observe(State(0)) == "", "Pause/resume repeats cue");
        Check(Observe(State(1)).Contains("step"), "No validated-progress cue");
        Check(Observe(State(0)).Contains("objective"), "Replay wrongly reports success");
        Check(!Observe(State(1, revision: "v2")).Contains("step"), "Edited configuration claims success");
        Check(Observe(State(-1, false, "v2")).Contains("complete"), "No guide-complete cue");
        Check(Observe(State(-1, false, "v2")) == "", "Completion cue repeats");
        System.Console.WriteLine("PASS tutorial sounds: transition-only, repeat packets, pause/resume, replay, configuration change and completion");
        return 2;
    }
}
