using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Entities;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Interface.Game;
using Intersect.Client.Networking;
using Intersect.Framework.Core.WorldMaps;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using MessagePack;
using Intersect.Client.Framework.Graphics;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Resources;
using ControlBase = Intersect.Client.Framework.Gwen.Control.Base;
using RendererBase = Intersect.Client.Framework.Gwen.Renderer.Base;

internal static class WorldMapTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        try { RunCore(); } catch (Exception exception) { System.Console.Error.WriteLine(exception); Environment.Exit(1); }
    }

    private static void RunCore()
    {
        var content = new TestContentManager();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var assembly = typeof(GameInterface).Assembly;
        var type = assembly.GetType("Intersect.Client.Interface.Game.WorldMapWindow+WorldMapCanvas", true)!;
        var renderPasses = type.GetMethod("RenderPreviewLayers", BindingFlags.Static | BindingFlags.NonPublic)!;
        var customLayers = new Intersect.Config.LayerOptions();
        customLayers.LowerLayers.Clear(); customLayers.LowerLayers.Add("Custom ground");
        customLayers.MiddleLayers.Clear(); customLayers.MiddleLayers.Add("Custom roof");
        customLayers.UpperLayers.Clear(); customLayers.UpperLayers.Add("Custom canopy"); customLayers.Validate();
        var pixels = new int[3]; var resourcePasses = 0;
        Func<string, bool> drawLayer = layer => {
            if (layer == "Custom ground") Array.Fill(pixels, 1);
            if (layer == "Custom roof") pixels[1] = 3;
            if (layer == "Custom canopy") pixels[2] = 4;
            return true;
        };
        Func<bool> drawResources = () => { ++resourcePasses; Array.Fill(pixels, 2); return true; };
        Check((bool)renderPasses.Invoke(null, [customLayers, drawLayer, drawResources])! && pixels.SequenceEqual(new[] { 2, 3, 4 }) && resourcePasses == 1,
            "World/minimap resource silhouettes must remain behind custom Fringe/upper tiles while uncovered resources remain visible");
        customLayers.LowerLayers.Clear(); customLayers.Validate(); resourcePasses = 0;
        Check((bool)renderPasses.Invoke(null, [customLayers, new Func<string, bool>(_ => false), drawResources])! && resourcePasses == 1,
            "Missing tile textures and maps without lower layers still render resources exactly once");
        var oldOptions = Options.Instance;
        var oldContent = Globals.ContentManager;
        var oldMe = Globals.Me;
        var oldGrid = Globals.MapGrid;
        var oldGridMaps = Globals.GridMaps.ToDictionary(pair => pair.Key, pair => pair.Value);
        var oldMarkers = Globals.WorldMapSocialMarkers;
        var oldEventMarkers = Globals.WorldMapEventMarkers;
        var animation = new AnimationDescriptor(Guid.NewGuid()) { Lower = new() { Sprite = "marker-test", XFrames = 1, YFrames = 1 } };
        var resource = new ResourceDescriptor(Guid.NewGuid());
        var strangerId = Guid.NewGuid();
        var oldLegend = Globals.WorldMapLegend;
        var oldReceived = Globals.WorldMapStateReceivedAt;
        var throttle = typeof(PacketSender).GetField("_nextWorldMapStateRequest", BindingFlags.NonPublic | BindingFlags.Static)!;
        var oldThrottle = throttle.GetValue(null);
        try
        {
            Globals.ContentManager = content;
            typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, new Options());
            throttle.SetValue(null, long.MaxValue); // Exercise drawing without a real network connection.
            var mapId = Guid.NewGuid();
            var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
            GC.SuppressFinalize(player); // Data-only fixture does not initialize the rendering lifecycle.
            player.MapId = mapId; player.Id = Guid.NewGuid(); player.X = 10; player.Y = 8;
            Globals.Me = player;
            Globals.MapGrid = new Guid[3, 3]; Globals.MapGrid[1, 1] = mapId;
            Globals.GridMaps.Clear(); Globals.GridMaps[mapId] = new Point(1, 1);
            Globals.WorldMapSocialMarkers =
            [
                new() { PlayerId = Guid.NewGuid(), MapId = mapId, X = 13, Y = 9, Party = true, Name = "Party member" },
                new() { PlayerId = Guid.NewGuid(), MapId = mapId, X = 16, Y = 10, Name = "Friend" },
            ];
            Globals.WorldMapStateReceivedAt = Environment.TickCount64;
            using var renderer = new MapRenderer();
            using var skin = new TestSkin(renderer);
            using var canvas = new Canvas(skin) { Size = new Point(1280, 720) };
            var map = (ControlBase)Activator.CreateInstance(type, [canvas, "TestMap", true, false, false, false, false, 0.78f, false])!;
            map.SetSize(660, 584);
            var zoom = type.GetField("_zoom", flags)!;
            var markerSize = type.GetMethod("EventMarkerSize", BindingFlags.Static | BindingFlags.NonPublic)!;
            int SizeAt(float value) => (int)markerSize.Invoke(null, [value])!;
            Check(SizeAt(.3f) >= 28 && SizeAt(2f) > SizeAt(.78f) && SizeAt(12f) >= 128,
                "Animation markers must grow with close zoom and stay readable when zoomed out");
            var panX = type.GetField("_panX", flags)!;
            var panY = type.GetField("_panY", flags)!;
            AnimationDescriptor.Lookup[animation.Id] = animation;
            content.AddAnimationTexture("marker-test", DispatchProxy.Create<IGameTexture, CpuFrameTexture>());
            animation.Lower.ScalePercent = 1;
            var runtimeAnimation = (Intersect.Client.Entities.Animation)RuntimeHelpers.GetUninitializedObject(typeof(Intersect.Client.Entities.Animation));
            GC.SuppressFinalize(runtimeAnimation); runtimeAnimation.Descriptor = animation;
            var tinySize = runtimeAnimation.CalculateAnimationSize();
            Check(tinySize.X == 1 && tinySize.Y == 1, "Client animation bounds must allow one percent scale");
            animation.Lower.ScalePercent = 100;
            var resourceTexture = DispatchProxy.Create<IGameTexture, CpuFrameTexture>();
            content.AddResourceTexture("resource-test", resourceTexture); content.AddTilesetTexture("resource-test", resourceTexture);
            ResourceDescriptor.Lookup[resource.Id] = resource;
            var resourceGraphic = type.GetMethod("ResourcePreviewGraphics", BindingFlags.Static | BindingFlags.NonPublic)!;
            foreach (var sourceType in new[] { ResourceTextureSource.Resource, ResourceTextureSource.Tileset, ResourceTextureSource.Animation })
            {
                resource.States.Clear(); resource.States[Guid.NewGuid()] = new()
                { TextureName = "resource-test", TextureType = sourceType, MaximumHealth = 100, X = 1, AnimationId = animation.Id };
                var graphics = ((IEnumerable<(IGameTexture Texture, Intersect.Client.Framework.GenericClasses.FloatRect Source, float Scale)>)resourceGraphic.Invoke(null, [resource.Id])!).ToArray();
                Check(graphics.Length == 1 && ReferenceEquals(graphics[0].Texture, sourceType == ResourceTextureSource.Animation ? content.GetTexture(Intersect.Client.Framework.Content.TextureType.Animation, "marker-test") : resourceTexture),
                    "World Map previews must resolve standalone, tileset and animated resource graphics");
                if (sourceType == ResourceTextureSource.Tileset) Check(graphics[0].Source.X == Options.Instance.Map.TileWidth, "Resource preview uses the selected tileset cell");
            }
            Globals.WorldMapEventMarkers = [new(Guid.NewGuid(), mapId, 10, 8, animation.Id)];
            var drawMarkers = type.GetMethod("DrawWorldMapEventMarkers", flags)!;
            panX.SetValue(map, 0f); panY.SetValue(map, 0f);
            foreach (var testZoom in new[] { .3f, .78f, 2f, 12f })
            {
                zoom.SetValue(map, testZoom); renderer.Textures.Clear();
                drawMarkers.Invoke(map, [renderer, 200, 160]);
                var drawn = renderer.Textures.Single();
                Check(drawn.Width == SizeAt(testZoom) && drawn.Height == SizeAt(testZoom),
                    "Actual event animation rendering must apply zoom-dependent size");
            }
            renderer.Texts.Clear(); renderer.Textures.Clear();
            Globals.WorldMapEventMarkers = [new(Guid.NewGuid(), mapId, 10, 8, Guid.Empty) { Name = "Harbor master" }];
            drawMarkers.Invoke(map, [renderer, 200, 160]);
            Check(renderer.Texts.Contains("Harbor master") && renderer.Textures.Count == 0,
                "World Map renders event names independently of animation markers");
            var namedMarker = MessagePackSerializer.Deserialize<WorldMapEventMarker>(MessagePackSerializer.Serialize(Globals.WorldMapEventMarkers[0]));
            Check(namedMarker.Name == "Harbor master", "Event names survive the map packet roundtrip");
            Globals.WorldMapEventMarkers = [];
            void Zoom(float delta) => type.GetMethod("ZoomBy")!.Invoke(map, [delta]);
            void CheckCentered()
            {
                var width = (int)type.GetProperty("CellWidth", flags)!.GetValue(map)!;
                var height = (int)type.GetProperty("CellHeight", flags)!.GetValue(map)!;
                var x = (float)panX.GetValue(map)! + (1 + (player.X + 0.5f) / Options.Instance.Map.MapWidth) * width;
                var y = (float)panY.GetValue(map)! + (1 + (player.Y + 0.5f) / Options.Instance.Map.MapHeight) * height;
                Check(Math.Abs(x - 330) < 0.01f && Math.Abs(y - 292) < 0.01f, "Zoom must center on the player tile");
            }
            for (var index = 0; index < 25; ++index) Zoom(0.15f);
            Check((float)zoom.GetValue(map)! == 12f, "Close zoom limit"); CheckCentered();
            panX.SetValue(map, -999f); Zoom(-0.15f); CheckCentered();
            for (var index = 0; index < 40; ++index) Zoom(-0.15f);
            Check((float)zoom.GetValue(map)! == 0.30f, "Far zoom limit"); CheckCentered();
            zoom.SetValue(map, 0.78f); type.GetMethod("CenterOnPlayer")!.Invoke(map, null);
            var render = type.GetMethod("Render", flags)!;
            render.Invoke(map, [skin]);
            Check(renderer.Calls.Any(call => SameRgb(call.Color, 75, 185, 255)), "Party marker missing");
            Check(renderer.Calls.Any(call => SameRgb(call.Color, 95, 225, 125)), "Friend marker missing");
            var social = Globals.WorldMapSocialMarkers;
            Globals.WorldMapSocialMarkers = [..social, new() { PlayerId = strangerId, MapId = mapId, X = player.X + 1, Y = player.Y, Guild = true }];
            renderer.Calls.Clear(); render.Invoke(map, [skin]);
            Check(renderer.Calls.Any(call => SameRgb(call.Color, 195, 125, 255)), "World Map guild marker missing");
            Globals.WorldMapSocialMarkers = social;
            Check(!renderer.Calls.Any(call => SameRgb(call.Color, 86, 96, 104) || SameRgb(call.Color, 255, 214, 92)),
                "Map grid lines / yellow selection square must not be rendered");
            renderer.Calls.Clear();
            Globals.WorldMapStateReceivedAt = Environment.TickCount64 - 6000;
            render.Invoke(map, [skin]);
            Check(!renderer.Calls.Any(call => SameRgb(call.Color, 75, 185, 255) || SameRgb(call.Color, 95, 225, 125)),
                "Stale social positions must expire");

            Globals.WorldMapStateReceivedAt = Environment.TickCount64;
            var mini = (ControlBase)Activator.CreateInstance(type, [canvas, "TestMinimap", false, false, true, true, false, 1.55f, true])!;
            mini.SetSize(212, 178);
            renderer.Calls.Clear(); render.Invoke(mini, [skin]);
            Check(renderer.Calls.Any(call => SameRgb(call.Color, 75, 185, 255)) &&
                renderer.Calls.Any(call => SameRgb(call.Color, 95, 225, 125)), "Minimap must also show friends and party");
            type.GetMethod("ZoomBy")!.Invoke(mini, [0.15f]);
            Check((float)zoom.GetValue(mini)! == 1.55f, "Minimap keeps its fixed zoom");
            var shipsType=assembly.GetType("Intersect.Client.Interface.Game.WorldMapShips",true)!;
            var receiveShips=shipsType.GetMethod("Receive",BindingFlags.Static|BindingFlags.NonPublic)!;
            var clearShips=shipsType.GetMethod("Clear",BindingFlags.Static|BindingFlags.NonPublic)!;
            var shipMarkers=type.GetMethod("DrawShipMarkers",flags)!;
            var enemyShip=new WorldMapShipMarker{Id=Guid.NewGuid(),MapId=mapId,X=player.X+2,Y=player.Y,Enemy=true,Name="Enemy"};
            try
            {
                receiveShips.Invoke(null,[new[]{enemyShip,new WorldMapShipMarker{Id=Guid.NewGuid(),MapId=mapId,X=player.X,Y=player.Y}},Environment.TickCount64-1100]);
                renderer.Calls.Clear();shipMarkers.Invoke(mini,[renderer]);
                var enemyMarker=renderer.Calls.Single(call=>SameRgb(call.Color,232,64,64));
                Check(renderer.Calls.Count==1,"Minimap must show hostile ship squares without friendly ship duplicates");
                enemyShip.X+=2;
                receiveShips.Invoke(null,[new[]{enemyShip},Environment.TickCount64-1100]);
                renderer.Calls.Clear();shipMarkers.Invoke(mini,[renderer]);
                Check(renderer.Calls.Single(call=>SameRgb(call.Color,232,64,64)).Bounds.X>enemyMarker.Bounds.X,"Enemy ship minimap marker must follow fresh positions");
            }
            finally{clearShips.Invoke(null,null);}
            var beforePan = (float)panX.GetValue(mini)!;
            player.X++; render.Invoke(mini, [skin]);
            Check((float)panX.GetValue(mini)! < beforePan, "Minimap must follow player movement");
            var nearby = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player)); GC.SuppressFinalize(nearby);
            nearby.Id = strangerId; nearby.MapId = mapId; nearby.X = (byte)(player.X + 1); nearby.Y = player.Y;
            typeof(Intersect.Client.Entities.Entity).GetProperty("Status")!.SetValue(nearby, new List<Intersect.Client.Framework.Entities.IStatus>());
            Globals.Entities[strangerId] = nearby;
            renderer.Calls.Clear(); render.Invoke(mini, [skin]);
            Check(renderer.Calls.Any(call => SameRgb(call.Color, 210, 210, 215)), "Minimap must display visible nearby players outside social groups");
            player.Guild = "Test guild"; nearby.Guild = "Test guild";
            renderer.Calls.Clear(); render.Invoke(mini, [skin]);
            Check(renderer.Calls.Any(call => SameRgb(call.Color, 195, 125, 255)), "Minimap must distinguish guild members");
            nearby.IsHidden = true; renderer.Calls.Clear(); render.Invoke(mini, [skin]);
            Check(!renderer.Calls.Any(call => SameRgb(call.Color, 195, 125, 255)), "Hidden players must not leak onto the minimap");
            Globals.Entities.Remove(strangerId); player.Guild = null;

            var legend = new WorldMapLegend { Entries = [new() { AnimationId = animation.Id, Label = "Potion shop" }] };
            Globals.WorldMapLegend = WorldMapLegend.FromJson(legend.ToJson());
            var legendType = assembly.GetType("Intersect.Client.Interface.Game.WorldMapLegendView", true)!;
            var view = (ControlBase)Activator.CreateInstance(legendType, [canvas])!;
            view.SetSize(210, 584);
            var update = legendType.GetMethod("OnPreDraw", flags)!;
            update.Invoke(view, [skin]);
            var rows = (System.Collections.ICollection)legendType.GetField("_rows", flags)!.GetValue(view)!;
            Check(rows.Count == 8, "Legend must include guild, both ship factions and configured animation entries");
            var legendLabels = rows.Cast<ControlBase>().SelectMany(row => row.Children).OfType<Label>().Select(label => label.Text).ToArray();
            foreach (var source in new[] { "Player ships", "Enemy ships" })
                Check(legendLabels.Contains(Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get(source,
                    Intersect.Client.Localization.ClientLanguages.Current)), "Missing ship legend label: " + source);
            Check(legendLabels.Contains(Intersect.Client.Localization.ClientLanguages.Text("Guild")) && legendLabels.Contains("Potion shop"),
                "Ship entries must preserve existing social and configured legend labels");
            var legendRow = rows.Cast<ControlBase>().Last();
            var legendIcon = legendRow.Children.Single(child => child.GetType().Name == "LegendIcon");
            Check(legendRow.Height == 52 && legendIcon.Width == 52 && legendRow.Children.All(child => child.Bottom <= legendRow.Height),
                "Legend rows must fit larger icons without excessive empty space or overflowing labels");
            renderer.Textures.Clear(); legendIcon.GetType().GetMethod("Render", flags)!.Invoke(legendIcon, [skin]);
            Check(renderer.Textures.Count == 1 && Math.Max(renderer.Textures[0].Width, renderer.Textures[0].Height) == 48,
                "Legend animation must fill a 48px icon using visible pixels");
            Globals.WorldMapLegend = new(); update.Invoke(view, [skin]);
            Check(rows.Count == 7, "Live legend changes must rebuild without stale controls");
            var state = new WorldMapStatePacket { Players = Globals.WorldMapSocialMarkers, LegendJson = legend.ToJson() };
            state.Players[0].Guild = true;
            var decoded = MessagePackSerializer.Deserialize<WorldMapStatePacket>(MessagePackSerializer.Serialize(state));
            Check(decoded.Players.Length == 2 && decoded.Players[0].Party && decoded.Players[0].Guild && decoded.LegendJson == state.LegendJson,
                "World Map state wire round-trip");
            Check(!MessagePackSerializer.Deserialize<RequestWorldMapPacket>(new byte[] { 0x90 }).StateOnly,
                "Existing full World Map requests must remain supported");
            System.Console.WriteLine("PASS World Map: close zoom, player centering, seamless canvas, social markers, stale expiry, live legend and packet compatibility");
        }
        finally
        {
            Globals.Me = oldMe; Globals.MapGrid = oldGrid;
            Globals.ContentManager = oldContent;
            Globals.GridMaps.Clear(); foreach (var pair in oldGridMaps) Globals.GridMaps[pair.Key] = pair.Value;
            Globals.WorldMapSocialMarkers = oldMarkers; Globals.WorldMapLegend = oldLegend;
            Globals.WorldMapEventMarkers = oldEventMarkers; AnimationDescriptor.Lookup.DeleteAt(animation.Id);
            ResourceDescriptor.Lookup.DeleteAt(resource.Id); Globals.Entities.Remove(strangerId);
            Globals.WorldMapStateReceivedAt = oldReceived; throttle.SetValue(null, oldThrottle);
            typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, oldOptions);
        }
    }

    private static bool SameRgb(Color color, int r, int g, int b) => color.R == r && color.G == g && color.B == b;
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("World Map: " + message); }

    private sealed class MapRenderer : RendererBase
    {
        internal readonly List<(Color Color, Rectangle Bounds)> Calls = [];
        internal readonly List<Rectangle> Textures = [];
        internal readonly List<string> Texts = [];
        public override void RenderText(IFont? font, int fontSize, Point position, string text, float scale = 1f) => Texts.Add(text);
        public override void DrawTexturedRect(IGameTexture? texture, Rectangle targetBounds, Color color,
            float u1 = 0, float v1 = 0, float u2 = 1, float v2 = 1) => Textures.Add(targetBounds);
        public override void DrawFilledRect(Rectangle rectangle) => Calls.Add((new Color(DrawColor), rectangle));
        public override Point MeasureText(Intersect.Client.Framework.Graphics.IFont? font, int fontSize, string? text, float scale = 1f) =>
            new((int)Math.Ceiling((text?.Length ?? 0) * fontSize * 0.6f * scale), (int)Math.Ceiling((fontSize + 3) * scale));
    }
}
