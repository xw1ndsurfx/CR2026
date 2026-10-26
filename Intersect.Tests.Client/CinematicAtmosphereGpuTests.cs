using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Intersect.Client.Cinematics;
using Intersect.Client.Entities;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.Database;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.General;
using Intersect.Client.Maps;
using Intersect.Client.MonoGame.Graphics;
using Intersect.Client.WorldEvents.Invasions;
using Intersect.Framework.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NUnit.Framework;
using ClientGraphics = Intersect.Client.Core.Graphics;
using ClientTime = Intersect.Client.General.Time;
using NumericsVector2 = System.Numerics.Vector2;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace Intersect.Tests.Client;

/// <summary>
/// Runs the production DesktopGL renderer, including its perspective pass and
/// atmosphere composition. On headless Linux use CODEX_GPU_TESTS=1,
/// SDL_VIDEODRIVER=offscreen, LIBGL_ALWAYS_SOFTWARE=1 and ALSOFT_DRIVERS=null.
/// CODEX_GPU_PREVIEW optionally receives the actual framebuffer PNGs.
/// </summary>
[TestFixture, NonParallelizable, Category("GPU")]
public sealed partial class CinematicAtmosphereGpuTests
{
    private const int Width = 320;
    private const int Height = 240;
    private const string FogName = "cinematic-gpu-fog";
    private static readonly FloatRect Screen = new(0, 0, Width, Height);
    private static readonly FloatRect Pixel = new(0, 0, 1, 1);

    [Test]
    public void RealPerspectiveAtmospherePreservesTerrainAndCompositesFogOnce()
    {
        if (Environment.GetEnvironmentVariable("CODEX_GPU_TESTS") != "1")
        {
            Assert.Ignore("Set CODEX_GPU_TESTS=1 and provide a real OpenGL context to execute this GPU regression.");
        }

        using var world = new TestWorld();
        using var game = new GpuGame((renderer, device) => VerifyFrames(world.Map, renderer, device));
        game.Run();
        game.Failure?.Throw();
        Assert.That(game.Executed, Is.True, "The MonoGame draw callback must actually execute.");
    }

    private static void VerifyFrames(MapInstance map, MonoRenderer renderer, GraphicsDevice device)
    {
        XnaColor[] palette = [new(190, 45, 35), new(25, 170, 55), new(35, 65, 195), new(200, 170, 40)];
        using var atlas = CreateTexture(renderer, device, "cinematic-gpu-tiles", 64, 64,
            Enumerable.Range(0, 64 * 64).Select(i => palette[(i % 64 / 32) + (i / 64 / 32) * 2]).ToArray());
        using var fog = CreateTexture(renderer, device, FogName, 64, 64,
            Enumerable.Repeat(new XnaColor(110, 130, 150), 64 * 64).ToArray());
        using var perspective = renderer.CreateRenderTexture(Width, Height);
        using var nativeLight = renderer.CreateRenderTexture(1, 1);
        var terrain = renderer.CreateTileBuffer();
        try
        {
            for (var y = 0; y < 24; y++)
            for (var x = 0; x < 24; x++)
            {
                Assert.That(terrain.TryAddTile(atlas, x * 32, y * 32, x % 2 * 32, y % 2 * 32, 32, 32), Is.True);
            }

            Assert.That(terrain.SetData(), Is.True);
            var scene = new MapPerspectiveScene(new NumericsVector2(384, 576), new NumericsVector2(0, -1),
                32, 32, [terrain], [], EyeHeight: 3, PitchDegrees: -24, FieldOfView: 65);
            Globals.ContentManager = new FogContent(fog);
            map.Fog = FogName;
            map.FogTransparency = 128;
            // Start at a settled visual state: this test checks GPU composition,
            // independently of the wall-clock duration of the fog fade-in.
            Field(typeof(MapInstance), "mRenderedFogName").SetValue(map, FogName);
            Field(typeof(MapInstance), "mCurFogIntensity").SetValue(map, 1f);
            Field(typeof(MapInstance), "mFogUpdateTime").SetValue(map, Timing.Global.MillisecondsUtc);

            void DrawScene(bool includeCoveredWorldFog = false)
            {
                renderer.EndSpriteBatch();
                device.SetRenderTarget(null);
                device.Clear(XnaColor.Black);
                if (includeCoveredWorldFog)
                {
                    // This is the ordinary world atmosphere drawn before the
                    // perspective image in Graphics.Draw. It must be covered.
                    ClientGraphics.DrawScreenTexture(renderer.WhitePixel, Pixel, Screen, new Color(255, 40, 90, 30));
                    map.DrawFog();
                }

                Assert.That(renderer.RenderMapPerspective(perspective, scene), Is.True,
                    "The real perspective renderer must draw the native terrain buffers.");
                ClientGraphics.DrawScreenTexture(perspective, Screen, Screen, Color.White);
            }

            XnaColor[] Capture(string name)
            {
                renderer.EndSpriteBatch();
                var pixels = new XnaColor[Width * Height];
                device.GetBackBufferData(pixels);
                SavePreview(device, name, pixels);
                return pixels;
            }

            void DrawReferenceFog(int alpha) => ClientGraphics.DrawScreenTexture(fog,
                new FloatRect(0, 0, fog.Width, fog.Height), Screen, new Color(alpha, 255, 255, 255));

            Field(typeof(ClientTime), "sCurrentColor").SetValue(null, ColorF.White);
            DrawScene();
            var baseline = Capture("01-perspective-terrain");
            Assert.That(baseline.Count(p => palette.Any(c => RgbDistance(c, p) <= 2)), Is.GreaterThan(Width * Height / 4),
                "The baseline must contain substantial colored terrain, not just the sky or an empty render target.");

            DrawScene();
            ClientGraphics.DrawCinematicAmbient(map);
            AssertFrame(baseline, Capture("02-white-ambient"), "Opaque white daylight must preserve the terrain.");

            XnaColor[]? singleFog = null;
            foreach (var alpha in new[] { 0, 128, 255 })
            {
                map.FogTransparency = alpha;
                DrawScene();
                DrawReferenceFog(alpha);
                var expected = Capture($"reference-fog-{alpha}");
                DrawScene(includeCoveredWorldFog: true);
                CinematicEnvironment.DrawPerspectiveAtmosphere();
                var actual = Capture($"03-perspective-fog-{alpha}");
                AssertFrame(expected, actual,
                    $"Fog opacity {alpha} must be applied once above the opaque perspective image.");
                if (alpha == 128) singleFog = actual;
            }

            map.FogTransparency = 128;
            DrawScene();
            DrawReferenceFog(128);
            DrawReferenceFog(128);
            var doubledFog = Capture("control-double-fog");
            Assert.That(MaxRgbError(singleFog!, doubledFog), Is.GreaterThan(10),
                "The fixture must distinguish one fog layer from two; otherwise its regression check is ineffective.");

            // Keep an explicit negative control for the original bug: ordinary
            // alpha-compositing opaque white replaces the scene before fog.
            DrawScene();
            ClientGraphics.DrawScreenTexture(renderer.WhitePixel, Pixel, Screen, Color.White);
            DrawReferenceFog(128);
            var legacy = Capture("control-legacy-white-overlay");
            Assert.That(MaxRgbError(singleFog!, legacy), Is.GreaterThan(40),
                "The captured terrain must distinguish the corrected pass from the original white-overlay failure.");

            var tint = new Color(160, 60, 95, 155);
            Field(typeof(ClientTime), "sCurrentColor").SetValue(null, new ColorF(tint.R, tint.G, tint.B, tint.A));
            // Construct the reference through GPU blending, exactly like the
            // native light map: tint over white, then multiply the scene. Do
            // not call CinematicAmbientLight or repeat its CPU formula here.
            nativeLight.Clear(Color.White);
            renderer.DrawTexture(renderer.WhitePixel, 0, 0, 1, 1, 0, 0, 1, 1, tint, nativeLight);
            renderer.EndSpriteBatch();
            device.SetRenderTarget(null);

            DrawScene();
            DrawReferenceFog(128);
            ClientGraphics.DrawScreenTexture(nativeLight, Pixel, Screen, Color.White, GameBlendModes.Multiply);
            var native = Capture("reference-native-colored-light");
            DrawScene(includeCoveredWorldFog: true);
            CinematicEnvironment.DrawPerspectiveAtmosphere();
            AssertFrame(native, Capture("04-colored-ambient"),
                "Colored ambiance must match native GPU lighting and darken the fog together with the terrain.");

            Globals.Database!.EnableLighting = false;
            DrawScene();
            ClientGraphics.DrawCinematicAmbient(map);
            AssertFrame(baseline, Capture("05-lighting-disabled"),
                "Disabled lighting must preserve the scene even while the time tint is colored.");
            DrawScene(includeCoveredWorldFog: true);
            CinematicEnvironment.DrawPerspectiveAtmosphere();
            AssertFrame(singleFog!, Capture("06-fog-lighting-disabled"),
                "Disabling lighting must leave exactly the configured fog layer.");
            TestContext.Progress.WriteLine("Real DesktopGL perspective, native ambient composition, and fog opacity 0/128/255 verified by framebuffer readback.");
        }
        finally
        {
            renderer.EndSpriteBatch();
            device.SetRenderTarget(null);
            terrain.Dispose();
        }
    }

    private static int RgbDistance(XnaColor left, XnaColor right) => Math.Max(Math.Abs(left.R - right.R),
        Math.Max(Math.Abs(left.G - right.G), Math.Abs(left.B - right.B)));

    private static int MaxRgbError(XnaColor[] expected, XnaColor[] actual) =>
        expected.Zip(actual, RgbDistance).Max();

    private static void AssertFrame(XnaColor[] expected, XnaColor[] actual, string message)
    {
        Assert.That(actual, Has.Length.EqualTo(expected.Length));
        Assert.That(MaxRgbError(expected, actual), Is.LessThanOrEqualTo(2), message);
    }

    private static void SavePreview(GraphicsDevice device, string name, XnaColor[] pixels)
    {
        var directory = Environment.GetEnvironmentVariable("CODEX_GPU_PREVIEW");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        using var texture = new Texture2D(device, Width, Height);
        texture.SetData(pixels);
        using var stream = File.Create(Path.Combine(directory, name + ".png"));
        texture.SaveAsPng(stream, Width, Height);
    }

    private static IGameTexture CreateTexture(MonoRenderer renderer, GraphicsDevice device, string name,
        int width, int height, XnaColor[] pixels)
    {
        // Feed the normal Texture2D asset path used by terrain buffers. A render
        // texture has a distinct generic platform type and is not a tileset.
        using var texture = new Texture2D(device, width, height);
        texture.SetData(pixels);
        using var stream = new MemoryStream();
        texture.SaveAsPng(stream, width, height);
        var png = stream.ToArray();
        return renderer.CreateTextureFromStreamFactory(name, () => new MemoryStream(png, writable: false));
    }

    private static FieldInfo Field(Type type, string name) => type.GetField(name,
        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
        ?? throw new InvalidOperationException($"Missing fixture field {type.FullName}.{name}.");

    private sealed class GpuGame : Game
    {
        private readonly GraphicsDeviceManager _manager;
        private readonly Action<MonoRenderer, GraphicsDevice> _verify;
        private MonoRenderer _renderer = null!;
        internal bool Executed { get; private set; }
        internal ExceptionDispatchInfo? Failure { get; private set; }

        internal GpuGame(Action<MonoRenderer, GraphicsDevice> verify)
        {
            _verify = verify;
            _manager = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = Width,
                PreferredBackBufferHeight = Height,
                SynchronizeWithVerticalRetrace = false,
                GraphicsProfile = GraphicsProfile.Reach,
            };
            IsFixedTimeStep = false;
            InactiveSleepTime = TimeSpan.Zero;
            Window.Title = "Cinematic atmosphere GPU regression";
        }

        protected override void LoadContent()
        {
            _renderer = new MonoRenderer(_manager, Content, this);
            _renderer.Init(GraphicsDevice);
            _renderer.UpdateGraphicsState(Width, Height, initial: true);
            _renderer.SetView(Screen);
            ClientGraphics.Renderer = _renderer;
            ClientGraphics.CurrentView = Screen;
        }

        protected override void Draw(GameTime gameTime)
        {
            if (Executed) return;
            Executed = true;
            try { _verify(_renderer, GraphicsDevice); }
            catch (Exception error) { Failure = ExceptionDispatchInfo.Capture(error); }
            finally { Exit(); }
        }
    }

    private sealed class TestWorld : IDisposable
    {
        private readonly Options _options = Options.Instance;
        private readonly GameRenderer? _renderer = ClientGraphics.Renderer;
        private readonly FloatRect _view = ClientGraphics.CurrentView;
        private readonly GameDatabase? _database = Globals.Database;
        private readonly GameContentManager? _content = Globals.ContentManager;
        private readonly Player? _player = Globals.Me;
        private readonly Color _overlay = ClientGraphics.OverlayColor;
        private readonly (FieldInfo Field, object? Value)[] _statics;
        private readonly ConcurrentDictionary<Guid, InvasionEnvironmentState> _invasions;
        private readonly KeyValuePair<Guid, InvasionEnvironmentState>[] _previousInvasions;
        internal MapInstance Map { get; }

        internal TestWorld()
        {
            _statics = new[]
            {
                Field(typeof(ClientTime), "sCurrentColor"),
                Field(typeof(ClientGraphics), "sOverlayUpdate"),
                Field(typeof(GameContentManager), "_current"),
                Field(typeof(CinematicEnvironment), "_state"),
            }.Select(field => (field, field.GetValue(null))).ToArray();
            _invasions = (ConcurrentDictionary<Guid, InvasionEnvironmentState>)
                Field(typeof(InvasionEnvironmentManager), "Active").GetValue(null)!;
            _previousInvasions = _invasions.ToArray();
            _invasions.Clear();
            Field(typeof(CinematicEnvironment), "_state").SetValue(null, null);
            typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, new Options());
            Globals.Database = new MemoryDatabase { EnableLighting = true };
            ClientGraphics.OverlayColor = Color.Transparent;
            Map = new MapInstance(Guid.NewGuid()) { WeatherIntensity = 0, IsIndoors = false };
            MapInstance.Lookup.Set(Map.Id, Map);
            var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
            GC.SuppressFinalize(player);
            player.Id = Guid.NewGuid();
            player.LatestMap = Map;
            Field(typeof(Entity), "<MapId>k__BackingField").SetValue(player, Map.Id);
            Globals.Me = player;
        }

        public void Dispose()
        {
            MapInstance.Lookup.Delete(Map);
            GC.SuppressFinalize(Map);
            Globals.Me = _player;
            Globals.ContentManager = _content;
            Globals.Database = _database;
            ClientGraphics.Renderer = _renderer!;
            ClientGraphics.CurrentView = _view;
            ClientGraphics.OverlayColor = _overlay;
            foreach (var entry in _statics) entry.Field.SetValue(null, entry.Value);
            _invasions.Clear();
            foreach (var entry in _previousInvasions) _invasions[entry.Key] = entry.Value;
            typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, _options);
        }
    }

    private sealed class MemoryDatabase : GameDatabase
    {
        public override void DeletePreference(string key) { }
        public override bool HasPreference(string key) => false;
        public override void SavePreference<TValue>(string key, TValue value) { }
        public override string LoadPreference(string key) => string.Empty;
        public override bool LoadConfig() => true;
    }

    private sealed class FogContent(IGameTexture fog) : GameContentManager
    {
        public override IGameTexture? GetTexture(TextureType type, string name) =>
            type == TextureType.Fog && name == FogName ? fog : null;
        public override void LoadTilesets(string[] names) => throw new NotSupportedException();
        public override void LoadTexturePacks() => throw new NotSupportedException();
        public override void LoadItems() => throw new NotSupportedException();
        public override void LoadEntities() => throw new NotSupportedException();
        public override void LoadSpells() => throw new NotSupportedException();
        public override void LoadAnimations() => throw new NotSupportedException();
        public override void LoadFaces() => throw new NotSupportedException();
        public override void LoadImages() => throw new NotSupportedException();
        public override void LoadDungeons() => throw new NotSupportedException();
        public override void LoadFogs() => throw new NotSupportedException();
        public override void LoadResources() => throw new NotSupportedException();
        public override void LoadPaperdolls() => throw new NotSupportedException();
        public override void LoadGui() => throw new NotSupportedException();
        public override void LoadMisc() => throw new NotSupportedException();
        public override void LoadFonts() => throw new NotSupportedException();
        public override void LoadShaders() => throw new NotSupportedException();
        public override void LoadSounds() => throw new NotSupportedException();
        public override void LoadMusic() => throw new NotSupportedException();
        protected override TAsset Load<TAsset>(Dictionary<string, IAsset> lookup, ContentType type,
            string name, Func<Stream> stream) => throw new NotSupportedException();
    }
}
