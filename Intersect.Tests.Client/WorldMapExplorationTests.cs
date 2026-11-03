using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Client.Entities;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Interface.Game;
using Intersect.Client.Localization;
using Intersect.Client.Networking;
using Intersect.Configuration;
using Intersect.Network.Packets.Server;
using NUnit.Framework;
using ControlBase = Intersect.Client.Framework.Gwen.Control.Base;
using RendererBase = Intersect.Client.Framework.Gwen.Renderer.Base;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Tests.Client;

[TestFixture, NonParallelizable]
public sealed class WorldMapExplorationTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly Guid _a = Guid.NewGuid(), _b = Guid.NewGuid(), _c = Guid.NewGuid();
    private Options _oldOptions = null!;
    private Player? _oldPlayer;
    private Guid[,]? _oldGrid;
    private Dictionary<Guid, Point> _oldPositions = null!;
    private long _oldThrottle;
    private FieldInfo Throttle => typeof(PacketSender).GetField("_nextWorldMapStateRequest", BindingFlags.Static | BindingFlags.NonPublic)!;

    [SetUp]
    public void SetUp()
    {
        _oldOptions = Options.Instance; _oldPlayer = Globals.Me; _oldGrid = Globals.MapGrid;
        _oldPositions = Globals.GridMaps.ToDictionary(p => p.Key, p => p.Value);
        _oldThrottle = (long)Throttle.GetValue(null)!;
        Throttle.SetValue(null, long.MaxValue);
        typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, new Options());
        var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
        GC.SuppressFinalize(player);
        player.Id = Guid.NewGuid(); player.MapId = _a; player.X = 1; player.Y = 1;
        Globals.Me = player;
        Globals.MapGrid = new Guid[3, 1];
        Globals.GridMaps.Clear();
        var maps = new[] { _a, _b, _c };
        for (var i = 0; i < maps.Length; i++) { Globals.MapGrid[i, 0] = maps[i]; Globals.GridMaps[maps[i]] = new(i, 0); }
        WorldMapExploration.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        WorldMapExploration.Clear(); WorldMapShips.Clear();
        Globals.Me = _oldPlayer; Globals.MapGrid = _oldGrid;
        Globals.GridMaps.Clear(); foreach (var pair in _oldPositions) Globals.GridMaps[pair.Key] = pair.Value;
        Throttle.SetValue(null, _oldThrottle);
        typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, _oldOptions);
    }

    private void Discover(params Guid[] maps) => WorldMapExploration.Receive(new()
        { PlayerId = Globals.Me!.Id, DiscoveredMapIds = maps });

    [Test]
    public void SnapshotsAreCharacterSpecificAndGridChangesDoNotEraseDiscoveries()
    {
        Assert.That(WorldMapExploration.IsDiscovered(_a), Is.True, "Current map is readable while initial state arrives.");
        Assert.That(WorldMapExploration.IsDiscovered(_b), Is.False);
        WorldMapExploration.Receive(new() { PlayerId = Guid.NewGuid(), DiscoveredMapIds = [_b] });
        Assert.That(WorldMapExploration.IsDiscovered(_b), Is.False);
        Discover(_a, _b, Guid.Empty, _b);
        var revision = WorldMapExploration.Revision;
        Discover(_b, _a);
        Assert.That(WorldMapExploration.Revision, Is.EqualTo(revision), "Unchanged snapshots do not rebuild quest routes.");
        Globals.MapGrid = new Guid[1, 1]; Globals.Me!.MapId = _c;
        Assert.That(WorldMapExploration.IsDiscovered(_b), Is.True);
        Globals.Me.Id = Guid.NewGuid();
        Assert.That(WorldMapExploration.IsDiscovered(_b), Is.False, "Changing characters cannot reuse the old owner's progress.");
        WorldMapExploration.Clear();
        Assert.That(WorldMapExploration.IsDiscovered(_a), Is.False);
    }

    [Test]
    public void FogHidesCachedTerrainEventNamesSocialAndShipMarkersUntilDiscovery()
    {
        var oldEvents = Globals.WorldMapEventMarkers; var oldSocial = Globals.WorldMapSocialMarkers;
        var oldReceived = Globals.WorldMapStateReceivedAt;
        using var renderer = new RecordingRenderer(); using var skin = new TestSkin(renderer);
        using var canvas = new Canvas(skin);
        var map = CreateCanvas(canvas, true);
        var previews = (Dictionary<Guid, IGameRenderTexture>)Field(map, "_previews");
        var hiddenPreview = DispatchProxy.Create<IGameRenderTexture, PreviewTexture>();
        previews[_b] = hiddenPreview;
        try
        {
            Discover(_a);
            Globals.WorldMapEventMarkers = [new(Guid.NewGuid(), _b, 1, 1, Guid.Empty) { Name = "Secret harbor" }];
            Globals.WorldMapSocialMarkers = [new() { PlayerId = Guid.NewGuid(), MapId = _b, X = 1, Y = 1, Name = "Hidden friend" }];
            Globals.WorldMapStateReceivedAt = Environment.TickCount64;
            WorldMapShips.Receive([new() { Id = Guid.NewGuid(), MapId = _b, X = 1, Y = 1, Name = "Hidden ship" }], Environment.TickCount64);
            Render(map, skin);
            Assert.That(renderer.Textures, Is.Empty, "Even cached or gameplay-preloaded terrain must stay hidden.");
            Assert.That(renderer.Texts, Does.Not.Contain("Secret harbor"));
            Assert.That(renderer.Texts, Does.Not.Contain("Hidden friend"));
            Assert.That(renderer.Texts.Any(t => t.Contains("Hidden ship")), Is.False);
            Assert.That(renderer.Fills.Count(c => c.A == 255 && c.R == 27 && c.G == 31 && c.B == 37), Is.EqualTo(2));

            Discover(_a, _b);
            renderer.Clear(); Render(map, skin);
            Assert.That(renderer.Textures, Does.Contain(hiddenPreview));
            Assert.That(renderer.Texts, Does.Contain("Secret harbor"));
            Assert.That(renderer.Texts, Does.Contain("Hidden friend"));
            Assert.That(renderer.Texts.Any(t => t.Contains("Hidden ship")), Is.True);
            Assert.That(renderer.Fills.Count(c => c.R == 27 && c.G == 31 && c.B == 37), Is.EqualTo(1));

            var mini = CreateCanvas(canvas, false);
            Assert.That((bool)Invoke(mini, "IsMapVisible", _c)!, Is.True, "Minimap retains adjacent terrain.");
            WorldMapExploration.Clear();
            Assert.That((bool)Invoke(map, "IsMapVisible", _b)!, Is.False);
        }
        finally
        {
            Globals.WorldMapEventMarkers = oldEvents; Globals.WorldMapSocialMarkers = oldSocial;
            Globals.WorldMapStateReceivedAt = oldReceived;
        }
    }

    [Test]
    public void QuestRoutesCannotEnterUnknownMapsOrTargetAnUnknownDestination()
    {
        using var renderer = new RecordingRenderer(); using var skin = new TestSkin(renderer);
        using var canvas = new Canvas(skin); var map = CreateCanvas(canvas, true);
        var blocked = (Dictionary<Guid, (int Revision, bool[,] Blocked)>)Field(map, "_blockedTiles");
        foreach (var id in new[] { _a, _b, _c }) blocked[id] = (-1, new bool[Options.Instance.Map.MapWidth, Options.Instance.Map.MapHeight]);
        Discover(_a, _c);
        var destination = new QuestGuidanceTarget(_c, 1, 1);
        Assert.That(Invoke(map, "BuildQuestRoute", _a, 1, 1, destination), Is.Null,
            "Cached pathfinding data cannot route through unknown B to known C.");
        Assert.That(Invoke(map, "BuildQuestRoute", _a, 1, 1, new QuestGuidanceTarget(_b, 1, 1)), Is.Null);
        Discover(_a, _b, _c);
        Assert.That(Invoke(map, "BuildQuestRoute", _a, 1, 1, destination), Is.Not.Null);
    }

    [TestCase("en", "Unexplored region")]
    [TestCase("fr", "Région inexplorée")]
    [TestCase("es", "Región inexplorada")]
    [TestCase("xx", "Unexplored region")]
    public void ExplorationUsesTheExistingLanguageCatalogAndEnglishFallback(string language, string expected)
    {
        var current = typeof(ClientLanguages).GetProperty(nameof(ClientLanguages.Current))!;
        var old = ClientLanguages.Current;
        try
        {
            current.SetValue(null, language);
            Assert.That(ClientLanguages.Text("Unexplored region"), Is.EqualTo(expected));
            var hint = ClientLanguages.Text("Explore to reveal new regions. Discoveries are saved for this character.");
            Assert.That(hint, Is.Not.Empty);
            if (language is "fr" or "es") Assert.That(hint, Is.Not.EqualTo("Explore to reveal new regions. Discoveries are saved for this character."));
        }
        finally { current.SetValue(null, old); }
    }

    private static WorldMapWindow.WorldMapCanvas CreateCanvas(Canvas parent, bool interactive)
    {
        var map = new WorldMapWindow.WorldMapCanvas(parent, "ExplorationTest", interactive, true, false, false, false, 1f, false);
        map.SetSize(660, 584); return map;
    }
    private static object Field(object target, string name) => target.GetType().GetField(name, Private)!.GetValue(target)!;
    private static object? Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private)!.Invoke(target, args);
    private static void Render(ControlBase control, SkinBase skin) => Invoke(control, "Render", skin);

    private sealed class RecordingRenderer : RendererBase
    {
        internal readonly List<Color> Fills = [];
        internal readonly List<string> Texts = [];
        internal readonly List<IGameTexture?> Textures = [];
        internal void Clear() { Fills.Clear(); Texts.Clear(); Textures.Clear(); }
        public override void DrawFilledRect(Rectangle rectangle) => Fills.Add(new(DrawColor));
        public override void RenderText(IFont? font, int size, Point position, string text, float scale = 1f) => Texts.Add(text);
        public override void DrawTexturedRect(IGameTexture? texture, Rectangle bounds, Color color,
            float u1 = 0, float v1 = 0, float u2 = 1, float v2 = 1) => Textures.Add(texture);
        public override Point MeasureText(IFont? font, int size, string? text, float scale = 1f) => new((text?.Length ?? 0) * 5, 12);
    }
    private sealed class TestSkin(RendererBase renderer) : SkinBase(renderer);
}

public class PreviewTexture : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name switch
    {
        "get_Width" or "get_Height" => 288,
        _ => null,
    };
}
