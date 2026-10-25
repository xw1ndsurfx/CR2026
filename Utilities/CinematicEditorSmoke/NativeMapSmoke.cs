using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Configuration;
using Intersect.Editor.Content;
using Intersect.Editor.General;
using Intersect.Editor.Maps;
using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Mapping.Tilesets;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Render = Intersect.Editor.Core.Graphics;

// Optional GPU integration check with an externally supplied export of native map data.
// No content, account credentials, production DBs or cached map screenshots are bundled.
internal static class NativeMapSmoke
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;
    internal static int Run(string fixtureDirectory, string outputDirectory)
    {
        fixtureDirectory = Path.GetFullPath(fixtureDirectory);
        outputDirectory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(outputDirectory);
        Intersect.Options.LoadFromServer(File.ReadAllText(Path.Combine(fixtureDirectory, "options.json")));
        foreach (var item in JArray.Parse(File.ReadAllText(Path.Combine(fixtureDirectory, "tilesets.json"))))
        {
            var descriptor = new TilesetDescriptor(Guid.Parse((string)item["Id"]!)) { Name = (string)item["Name"]! };
            TilesetDescriptor.Lookup[descriptor.Id] = descriptor;
        }
        var metadata = File.ReadAllText(Path.Combine(fixtureDirectory, "map.json"));
        var mapId = Guid.Parse((string)JObject.Parse(metadata)["Id"]!);
        var map = new MapInstance(mapId);
        map.Load(metadata, clearEvents: false);
        map.AttributeData = File.ReadAllBytes(Path.Combine(fixtureDirectory, "attributes.bin"));
        foreach (var item in JArray.Parse(File.ReadAllText(Path.Combine(fixtureDirectory, "events.json"))))
        {
            var id = Guid.Parse((string)item["Id"]!);
            var descriptor = new EventDescriptor(id, item.ToString());
            EventDescriptor.Lookup[id] = descriptor;
            map.LocalEvents[id] = descriptor;
        }
        MapDescriptor.Lookup[map.Id] = map;
        map.LoadTileData(File.ReadAllBytes(Path.Combine(fixtureDirectory, "tiles.bin")));
        Globals.CurrentMap = map;
        // Exercise the real single-map fallback while no neighboring grid is loaded.
        Globals.MapGrid = (MapGrid)RuntimeHelpers.GetUninitializedObject(typeof(MapGrid));

        using var host = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-20000, -20000) };
        var parameters = (PresentationParameters)typeof(Render).GetField("sPresentationParams", Private)!.GetValue(null)!;
        parameters.DeviceWindowHandle = host.Handle;
        Render.InitMonogame();
        if (Render.GetGraphicsDevice() == null) throw new InvalidOperationException("Native graphics device unavailable.");
        ClientConfiguration.Instance.Host = "127.0.0.1";
        ClientConfiguration.Instance.Port = 15463;
        var oldWarning = Intersect.Editor.Core.Preferences.LoadPreference("SuppressTextureWarning");
        try
        {
            Intersect.Editor.Core.Preferences.SavePreference("SuppressTextureWarning", "true");
            GameContentManager.LoadTilesets();
        }
        finally { Intersect.Editor.Core.Preferences.SavePreference("SuppressTextureWarning", oldWarning); }

        var scene = new CinematicScene { MapId = map.Id, Name = "Native map verification", Saga = "QA", Chapter = map.Name };
        var found = false;
        for (var y = 1; y < Intersect.Options.Instance.Map.MapHeight - 1 && !found; y++)
        for (var x = 1; x < Intersect.Options.Instance.Map.MapWidth - 3 && !found; x++)
        {
            if (Enumerable.Range(x, 3).Any(px => map.Attributes[px, y] is MapBlockedAttribute)) continue;
            scene.Actors.Add(new() { Name = "QA player marker", StartX = x, StartY = y });
            scene.Clips.Add(new() { Name = "Native route", Track = CinematicTrack.Movement, DurationMs = 1500,
                Points = [new() { X = x, Y = y }, new() { X = x + 2, Y = y }] });
            found = true;
        }
        if (!found) throw new InvalidOperationException("No free native route fixture found.");
        scene.Clips.Add(new() { Name = "Camera", Track = CinematicTrack.Camera, X = 16, Y = 13, Zoom = 2, StartMs = 2000, DurationMs = 1500 });
        var compiled = CinematicCompiler.Compile(scene, Intersect.Options.Instance.Map.MapWidth, Intersect.Options.Instance.Map.MapHeight,
            (x, y) => map.Attributes[x, y] is MapBlockedAttribute);
        var serialization = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto,
            SerializationBinder = new Intersect.Framework.Core.Serialization.IntersectTypeSerializationBinder() };
        var roundTrip = JsonConvert.DeserializeObject<EventPage>(JsonConvert.SerializeObject(compiled, serialization), serialization)!;
        if (roundTrip.Cinematic?.MapId != map.Id) throw new InvalidOperationException("Scene source persistence failed.");

        foreach (var id in map.Layers.Values.SelectMany(layer => layer.Cast<Tile>())
                     .Select(t => t.TilesetId).Where(id => id != Guid.Empty).Distinct())
        {
            var tileset = TilesetDescriptor.Get(id) ?? throw new InvalidOperationException($"Missing tileset descriptor {id}.");
            if (GameContentManager.GetTexture(GameContentManager.TextureType.Tileset, tileset.Name) == null)
                throw new InvalidOperationException($"Missing native tileset texture {tileset.Name}.");
        }
        var originalView = new System.Drawing.Rectangle(75, 50, 1, 1);
        Render.CurrentView = originalView;
        using var rendered = Render.RenderCinematicMap(scene, 0);
        if (Render.CurrentView != originalView) throw new InvalidOperationException("Director leaked its offscreen view into the map editor.");
        var hideFog = Intersect.Editor.Core.Database.GridHideFog;
        var hideDarkness = Intersect.Editor.Core.Database.GridHideDarkness;
        var hideOverlay = Intersect.Editor.Core.Database.GridHideOverlay;
        try
        {
            Intersect.Editor.Core.Database.GridHideFog = Intersect.Editor.Core.Database.GridHideDarkness =
                Intersect.Editor.Core.Database.GridHideOverlay = true;
            using var terrain = Render.RenderCinematicMap(scene, 0);
            var painted = 0;
            for (var y = 0; y < terrain.Height; y++)
            for (var x = 0; x < terrain.Width; x++) if (terrain.GetPixel(x, y).A > 0) painted++;
            if (painted < terrain.Width * terrain.Height / 4)
                throw new InvalidOperationException("Populated native map was clipped or terrain textures were not rendered.");
        }
        finally
        {
            Intersect.Editor.Core.Database.GridHideFog = hideFog;
            Intersect.Editor.Core.Database.GridHideDarkness = hideDarkness;
            Intersect.Editor.Core.Database.GridHideOverlay = hideOverlay;
        }
        var colors = new HashSet<int>();
        for (var y = 0; y < rendered.Height; y += 3)
        for (var x = 0; x < rendered.Width; x += 3) colors.Add(rendered.GetPixel(x, y).ToArgb());
        if (colors.Count < 32) throw new InvalidOperationException("Native map rendered blank or without tileset detail.");
        rendered.Save(Path.Combine(outputDirectory, "native-map.png"));
        var assembly = typeof(Intersect.Editor.Forms.FrmMain).Assembly;
        using var director = (Form)Activator.CreateInstance(assembly.GetType("Intersect.Editor.Forms.Cinematics.FrmCinematicDirector")!, true)!;
        director.ShowInTaskbar = false; director.StartPosition = FormStartPosition.Manual; director.Location = new(-20000, -20000);
        director.Show();
        ((System.Windows.Forms.Timer)director.GetType().GetField("_timer", Private)!.GetValue(director)!).Stop();
        director.GetType().GetMethod("LoadScene", Private)!.Invoke(director, [scene, Guid.Empty]);
        using var screenshot = new System.Drawing.Bitmap(director.Width, director.Height);
        director.DrawToBitmap(screenshot, new System.Drawing.Rectangle(0, 0, director.Width, director.Height));
        screenshot.Save(Path.Combine(outputDirectory, "native-director.png"));
        Console.WriteLine($"PASS: native GPU map {map.Name} ({map.Id}), {colors.Count} sampled colors, native Event scene round-trip and Director viewport.");
        return 0;
    }
}
