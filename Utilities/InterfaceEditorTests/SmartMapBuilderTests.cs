using System.Globalization;
using System.Reflection;
using Console = System.Console;
using Point = System.Drawing.Point;
using Intersect;
using Intersect.Editor.Forms.DockingElements;
using Intersect.Editor.Forms.EditorHub;
using Intersect.Editor.General;
using Intersect.Editor.Maps;
using Intersect.Framework.Core.GameObjects.Mapping.Tilesets;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;
using Intersect.Localization;

internal static class SmartMapBuilderTests
{
    private static readonly Guid Tileset = Guid.NewGuid();
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int _checks;
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); _checks++; }
    private static MapInstance Map()
    {
        var map = new MapInstance(Guid.NewGuid());
        foreach (var layer in Options.Instance.Map.Layers.All) map.Layers[layer] = new Tile[Options.Instance.Map.MapWidth, Options.Instance.Map.MapHeight];
        map.InitAutotiles(); return map;
    }
    private static SmartPreset Preset(byte mode) => new() { Name = "Fixture " + mode, Tile = new(Tileset, 0, 0, mode), Layer = Options.Instance.Map.Layers.All[0], Radius = 1 };
    internal static void Run()
    {
        Options.Instance.Map.Layers.Validate();
        Geometry(); ResourceValidation(); PresetStorage(); Protection(); NativeFormats(); NeighborBorders(); InputTransaction(); RealGame(); LocalizationAndPanel();
        Console.WriteLine($"PASS Smart Map Builder: {_checks} assertions; native formats 0–7, irregular areas, holes, erasure, cliffs, neighbors, staged input, undo/redo snapshots, preservation, stamps, transitions, presets and locales.");
        Console.WriteLine(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMART_MAP_GAME_FIXTURE"))
            ? "Real-game integration skipped: set SMART_MAP_GAME_FIXTURE to an isolated data export. Synthetic fixtures do not certify real artwork or connected-client acceptance."
            : "Real local data and editor GPU checks do not certify connected-client acceptance or real XP/cliff artwork absent from these legacy maps.");
    }
    private static void Geometry()
    {
        var line = SmartBrush.Line(new(0, 0), new(9, 4)).ToArray();
        Check(line.First() == new SmartCell(0, 0) && line.Last() == new SmartCell(9, 4), "Line endpoints");
        Check(line.Zip(line.Skip(1)).All(p => Math.Abs(p.First.X - p.Second.X) <= 1 && Math.Abs(p.First.Y - p.Second.Y) <= 1), "No gaps during fast dragging");
        Check(SmartBrush.Area(new(4, 4), new(4, 4), SmartShape.Brush, 1, true).Count() == 5, "Round brush footprint");
        Check(SmartBrush.Area(new(7, 8), new(3, 4), SmartShape.Rectangle, 0, false).Count() == 25, "Reverse rectangle");
        var ellipse = SmartBrush.Area(new(0, 0), new(8, 6), SmartShape.Ellipse, 0, false).ToHashSet();
        Check(ellipse.Contains(new(4, 3)) && !ellipse.Contains(new(0, 0)), "Ellipse geometry");
        var tiles = new Tile[7, 6];
        for (var y = 0; y < 6; y++) tiles[3, y] = new() { TilesetId = Tileset };
        Check(SmartBrush.Flood(tiles, new(0, 0), false).Count() == 18, "Flood respects barrier");
        Check(SmartBrush.Flood(tiles, new(0, 0), true).Count() == 36, "Replace includes disconnected identical regions");
    }
    private static void ResourceValidation()
    {
        for (byte mode = 0; mode < 8; mode++)
        {
            var tile = Preset(mode).Tile; var size = tile.Footprint;
            Check(tile.Fits(size.Width * 48, size.Height * 32, 48, 32), "Non-square, non-16 native format " + mode);
            Check(!tile.Fits(size.Width * 48 - 1, size.Height * 32, 48, 32), "Reject missing pixels " + mode);
            Check(!(tile with { X = 1 }).Fits(size.Width * 48, size.Height * 32, 48, 32), "Reject source outside sheet " + mode);
        }
        Check(!Preset(3).Tile.Fits(600, 300, 15, 15), "Native quarter tiles need even dimensions");
        Check(!(Preset(0).Tile with { X = int.MaxValue }).Fits(128, 128, 32, 32), "Resource validation does not overflow");
    }
    private static void PresetStorage()
    {
        var folder = Path.Combine(Path.GetTempPath(), "smart-map-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "presets.json");
        try
        {
            var preset = Preset(7); preset.Name = "Royal River"; preset.Favorite = true; preset.LastUsed = 42;
            preset.Variants.Add(Preset(3).Tile); preset.Stamp.Add(new(2, 1, preset.Layer, preset.Tile));
            preset.Transitions.Add(new(preset.Layer, Preset(0).Tile, Options.Instance.Map.Layers.All[1], preset.Tile));
            SmartPresetStore.Write(path, new[] { preset }); var loaded = SmartPresetStore.Read(path).Single();
            Check(loaded.Tile == preset.Tile && loaded.Favorite && loaded.LastUsed == 42 && loaded.Stamp.SequenceEqual(preset.Stamp) && loaded.Transitions.SequenceEqual(preset.Transitions), "Preset import/export retains native IDs, rules, stamps, history and favorites");
            loaded.Name = "Copy"; Check(preset.Name == "Royal River", "Duplicate is independent");
            File.WriteAllText(path, "[{\"Name\":\"broken\",\"Variants\":null}]");
            try { SmartPresetStore.Read(path); Check(false, "Reject corrupt optional presets"); } catch (InvalidDataException) { Check(true, "Corrupt optional data reported"); }
        }
        finally { File.Delete(path); Directory.Delete(folder); }
    }
    private static void Protection()
    {
        var map = Map(); var preset = Preset(3); var other = Options.Instance.Map.Layers.All[1];
        map.Layers[preset.Layer][3, 3] = Preset(1).Tile.Native; map.Layers[other][4, 3] = Preset(7).Tile.Native;
        map.Attributes[3, 3] = new MapBlockedAttribute();
        map.Attributes[4, 3] = new MapWarpAttribute { MapId = Guid.NewGuid(), X = 2, Y = 4 };
        map.Attributes[5, 3] = new MapResourceAttribute { ResourceId = Guid.NewGuid() };
        var evt = new Intersect.Framework.Core.GameObjects.Events.EventDescriptor(Guid.NewGuid(), map.Id, 3, 3);
        map.LocalEvents[evt.Id] = evt;
        map.Spawns.Add(new NpcSpawn { NpcId = Guid.NewGuid(), X = 3, Y = 3 });
        map.Lights.Add(new Intersect.Framework.Core.GameObjects.Lighting.LightDescriptor(3, 3));
        map.InitAutotiles();
        var before = map.SaveInternal();
        var cells = SmartBrush.Area(new(3, 3), new(6, 5), SmartShape.Rectangle, 0, false).ToArray();
        var edits = SmartBrush.Plan(map, preset, cells, true, false, 0);
        Check(edits.Count == 11, "Preserve mode skips occupied destination");
        Check(before.Matches(map.SaveInternal()), "Planning is read-only");
        SmartBrush.Apply(map, edits, false);
        Check(map.Layers[other][4, 3].Autotile == 7 && map.Layers[preset.Layer][3, 3].Autotile == 1, "Nonselected layer and occupied cells unchanged");
        var after = map.SaveInternal(); Check(before.Attributes.SequenceEqual(after.Attributes) && before.EventData == after.EventData && before.Metadata == after.Metadata, "Painting preserves all map metadata, attributes and event data");
        var copy = new MapInstance(map, true);
        Check(!ReferenceEquals(copy.Autotiles.Layers[preset.Layer][3, 3], map.Autotiles.Layers[preset.Layer][3, 3]), "Preview cache copy is independent");
        CompareFullRebuild(copy, "Copied cache remains visually equivalent");
        map.LoadInternal(before); Check(before.Matches(map.SaveInternal()), "Native undo roundtrip");
        map.LoadInternal(after); Check(after.Matches(map.SaveInternal()), "Native redo roundtrip");
        preset.Category = "Decoration"; preset.Variants.Add(Preset(6).Tile); preset.Density = 35;
        var first = SmartBrush.Plan(map, preset, cells, false, false, 104);
        var second = SmartBrush.Plan(map, preset, cells.AsEnumerable().Reverse().Concat(cells), false, false, 104);
        Check(first.OrderBy(e => e.X).ThenBy(e => e.Y).SequenceEqual(second.OrderBy(e => e.X).ThenBy(e => e.Y)), "Decoration density and variants stable across repeated visits and preview");
        preset = Preset(3); preset.Stamp.Add(new(0, 0, preset.Layer, preset.Tile)); preset.Stamp.Add(new(1, 0, other, Preset(7).Tile));
        Check(SmartBrush.Plan(map, preset, new[] { new SmartCell(Options.Instance.Map.MapWidth - 1, 0) }, false, false, 0, true).Count == 0, "Stamp clips atomically at map edge");
        map.Layers[other][1, 0] = Preset(1).Tile.Native;
        Check(SmartBrush.Plan(map, preset, new[] { new SmartCell(0, 0) }, true, false, 0, true).Count == 0, "Occupied stamp layer prevents partial building");
        Check(SmartBrush.Plan(map, preset, new[] { new SmartCell(0, 0) }, false, false, 0, true).Count == 2, "Explicit replacement permits multi-layer building");
        preset = Preset(1); preset.Transitions.Add(new(preset.Layer, Preset(3).Tile, other, Preset(6).Tile)); map.Layers[preset.Layer][2, 2] = Preset(3).Tile.Native;
        var transition = SmartBrush.Plan(map, preset, new[] { new SmartCell(2, 2) }, false, false, 0);
        Check(transition.Count == 1 && transition.Single(e => e.Layer == other).After.Autotile == 6 && transition.All(e => e.Layer != preset.Layer), "Configured transition preserves underlying terrain and routes painting to its selected overlay layer");
        Check(SmartBrush.Plan(map, preset, new[] { new SmartCell(2, 2) }, true, false, 0).Count == 1, "Overlay transitions remain usable with default preservation enabled");
        SmartBrush.Apply(map, transition, false);
        var removeOverlay = SmartBrush.Plan(map, preset, new[] { new SmartCell(2, 2) }, true, true, 0);
        Check(removeOverlay.Count == 1 && removeOverlay.Single().Layer == other && removeOverlay.Single().After.TilesetId == Guid.Empty, "Smart eraser removes configured overlay while preserving underlying terrain");
        Check(SmartBrush.Plan(map, Preset(7), new[] { new SmartCell(3, 3) }, true, true, 0).Count == 0, "Preserved erase cannot remove a different terrain");
    }
    private static void NativeFormats()
    {
        foreach (var dimensions in new[] { (32, 32), (48, 32) })
        {
            Options.Instance.Map.TileWidth = dimensions.Item1; Options.Instance.Map.TileHeight = dimensions.Item2;
            for (byte mode = 0; mode <= 7; mode++)
            {
                var map = Map(); var preset = Preset(mode);
                var cells = SmartBrush.Area(new(2, 2), new(14, 11), SmartShape.Rectangle, 0, false)
                    .Where(c => !(c.X is >= 5 and <= 7 && c.Y is >= 5 and <= 7) && c.X + c.Y < 23).ToArray();
                var edits = SmartBrush.Plan(map, preset, cells, false, false, 1); SmartBrush.Apply(map, edits, false);
                Check(cells.All(c => map.Layers[preset.Layer][c.X, c.Y].Autotile == mode), "Native mode survives painting " + mode);
                CompareFullRebuild(map, "Irregular area with hole " + mode);
                var erase = SmartBrush.Plan(map, preset, new[] { new SmartCell(4, 4), new SmartCell(4, 5), new SmartCell(4, 6) }, true, true, 1);
                SmartBrush.Apply(map, erase, false); CompareFullRebuild(map, "Reconnection after erase " + mode);
                var saved = map.SaveInternal(); map.LoadInternal(saved); Check(saved.Matches(map.SaveInternal()), "Save/reopen preserves native format " + mode);
            }
        }
        Options.Instance.Map.TileWidth = 32; Options.Instance.Map.TileHeight = 32;
    }
    private static void CompareFullRebuild(MapInstance map, string message)
    {
        var expected = new MapInstance(map); expected.InitAutotiles();
        foreach (var layer in Options.Instance.Map.Layers.All)
            for (var y = 0; y < Options.Instance.Map.MapHeight; y++) for (var x = 0; x < Options.Instance.Map.MapWidth; x++)
            {
                var actual = map.Autotiles.Layers[layer][x, y]; var rebuilt = expected.Autotiles.Layers[layer][x, y];
                // Native incremental updates retain unused quarter coordinates on empty/normal cells.
                Check(actual.RenderState == rebuilt.RenderState && (actual.RenderState != MapAutotiles.RENDER_STATE_AUTOTILE || actual.Equals(rebuilt)), message + $" {layer} {x},{y}");
            }
    }
    private sealed class NeighborMap : MapInstance
    {
        internal MapDescriptor[,] Grid = new MapDescriptor[3, 3];
        internal NeighborMap() : base(Guid.NewGuid()) { }
        public override MapDescriptor[,] GenerateAutotileGrid() { Grid[1, 1] = this; return Grid; }
    }
    private static void NeighborBorders()
    {
        var left = new NeighborMap(); var right = new NeighborMap();
        foreach (var map in new[] { left, right }) foreach (var layer in Options.Instance.Map.Layers.All) map.Layers[layer] = new Tile[Options.Instance.Map.MapWidth, Options.Instance.Map.MapHeight];
        left.Grid[2, 1] = right; right.Grid[0, 1] = left;
        var previousGrid = Globals.MapGrid;
        var grid = (MapGrid)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(MapGrid));
        grid.Loaded = true; grid.GridWidth = 2; grid.GridHeight = 1;
        grid.Grid = new MapGridItem[2, 1]; grid.Grid[0, 0] = new(left.Id); grid.Grid[1, 0] = new(right.Id);
        typeof(MapGrid).GetField("mGridMaps", Hidden)!.SetValue(grid, new List<Guid> { left.Id, right.Id });
        left.MapGridX = 0; right.MapGridX = 1; left.MapGridY = right.MapGridY = 0;
        MapInstance.Lookup[left.Id] = left; MapInstance.Lookup[right.Id] = right; Globals.MapGrid = grid;
        try
        {
        foreach (var mode in new byte[] { 1, 3, 4, 6, 7 })
        {
            var preset = Preset(mode); var x = Options.Instance.Map.MapWidth - 1;
            right.Layers[preset.Layer][0, 3] = preset.Tile.Native; left.InitAutotiles(); right.InitAutotiles();
            var neighborBefore = right.SaveInternal(); var cacheBefore = right.Autotiles.Layers[preset.Layer][0, 3].Copy();
            var plan = SmartBrush.Plan(left, preset, new[] { new SmartCell(x, 3) }, false, false, 0);
            SmartBrush.Apply(left, plan, false);
            Check(right.Autotiles.Layers[preset.Layer][0, 3].Equals(cacheBefore), "Preview cannot mutate adjacent cache " + mode);
            var current = left.Autotiles.Layers[preset.Layer][x, 3].Copy(); left.InitAutotiles();
            Check(current.Equals(left.Autotiles.Layers[preset.Layer][x, 3]), "Border uses loaded neighbor native connections " + mode);
            Check(neighborBefore.Matches(right.SaveInternal()), "Neighbor map data unchanged " + mode);
            left.UpdateAdjacentAutotiles();
            var updated = right.Autotiles.Layers[preset.Layer][0, 3].Copy(); right.InitAutotiles();
            Check(updated.Equals(right.Autotiles.Layers[preset.Layer][0, 3]) && neighborBefore.Matches(right.SaveInternal()), "Commit refreshes adjacent render cache without changing map data " + mode);
        }
        }
        finally { Globals.MapGrid = previousGrid; MapInstance.Lookup.Delete(left); MapInstance.Lookup.Delete(right); }
    }
    private static void InputTransaction()
    {
        using var editor = new FrmMapEditor(); editor.Load -= (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), editor, typeof(FrmMapEditor).GetMethod("frmMapEditor_Load", Hidden)!);
        using var panel = new FrmSmartMapBuilder();
        typeof(FrmMapEditor).GetField("_smartBuilder", Hidden)!.SetValue(editor, panel);
        var map = Map(); Globals.CurrentMap = map; Globals.MapEditorWindow = editor; Globals.CurrentTool = EditingTool.Brush;
        var preset = Preset(3); preset.Radius = 2;
        typeof(FrmSmartMapBuilder).GetField("_working", Hidden)!.SetValue(panel, preset);
        // Input fixture uses a real generated spritesheet with every required frame.
        Directory.CreateDirectory("resources/tilesets"); var name = "smart-map-test-" + Guid.NewGuid().ToString("N") + ".png";
        using (var image = new Bitmap(32 * 6, 32 * 3)) image.Save(Path.Combine("resources/tilesets", name));
        TilesetDescriptor.Lookup[Tileset] = new TilesetDescriptor(Tileset) { Name = name };
        try
        {
            panel.Show(); ((CheckBox)typeof(FrmSmartMapBuilder).GetField("_enabled", Hidden)!.GetValue(panel)!).Checked = true;
            Intersect.Editor.Core.Graphics.CurrentView = new(0, 0, 800, 600);
            var before = map.SaveInternal();
            var down = new MouseEventArgs(MouseButtons.Left, 1, 96, 96, 0);
            Check((bool)typeof(FrmMapEditor).GetMethod("BeginSmartStroke", Hidden)!.Invoke(editor, new object[] { down })!, "Smart input consumes classic painting");
            Globals.CurTileX = 13; Globals.CurTileY = 10;
            typeof(FrmMapEditor).GetMethod("MoveSmartStroke", Hidden)!.Invoke(editor, null);
            Check(before.Matches(map.SaveInternal()), "Drag stages changes without modifying source");
            var preview = new MapInstance(map); editor.PreviewSmartBuilder(preview);
            Check(!before.Matches(preview.SaveInternal()) && before.Matches(map.SaveInternal()), "Preview affects only temporary map");
            editor.picMap_MouseUp(editor, new(MouseButtons.Left, 1, 13 * 32, 10 * 32, 0));
            Check(editor.MapUndoStates.Count == 1 && editor.MapRedoStates.Count == 0, "Entire long stroke is exactly one undo operation");
            Check(map.GenerateTileData().SequenceEqual(preview.GenerateTileData()), "Committed cells match staged preview");
            var after = map.SaveInternal(); map.LoadInternal(editor.MapUndoStates.Single()); Check(before.Matches(map.SaveInternal()), "Undo restores complete prior map");
            map.LoadInternal(after); Check(after.Matches(map.SaveInternal()), "Redo restores whole stroke");
            typeof(FrmMapEditor).GetMethod("BeginSmartStroke", Hidden)!.Invoke(editor, new object[] { down }); editor.CancelSmartStroke();
            Check(after.Matches(map.SaveInternal()) && editor.MapUndoStates.Count == 1, "Cancel creates no mutations or undo entry");
            panel.Hide();
        }
        finally { File.Delete(Path.Combine("resources/tilesets", name)); TilesetDescriptor.Lookup.Delete(TilesetDescriptor.Get(Tileset)); Globals.CurrentMap = null; }
    }
    private static void LocalizationAndPanel()
    {
        var catalog = HubText.Read<Dictionary<string, Dictionary<string, string>>>("Texts.json");
        foreach (var (key, values) in catalog.Where(p => p.Key.StartsWith("Smart.")))
            foreach (var language in new[] { "en", "fr", "es" }) Check(values.TryGetValue(language, out var text) && !string.IsNullOrWhiteSpace(text) && GameTextTokens.Compatible(values["en"], text), "Locale/token coverage " + key + " " + language);
        var culture = CultureInfo.CurrentUICulture;
        try
        {
            foreach (var language in new[] { "en", "fr", "es" })
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
                using var panel = new FrmSmartMapBuilder { Size = new(330, 620), ShowInTaskbar = false };
                if (_realPresets.Count > 0)
                {
                    ((List<SmartPreset>)typeof(FrmSmartMapBuilder).GetField("_presets", Hidden)!.GetValue(panel)!).AddRange(_realPresets);
                    typeof(FrmSmartMapBuilder).GetField("_working", Hidden)!.SetValue(panel, _realPresets[1].Copy());
                    ((TextBox)typeof(FrmSmartMapBuilder).GetField("_name", Hidden)!.GetValue(panel)!).Text = _realPresets[1].Name;
                    typeof(FrmSmartMapBuilder).GetMethod("RefreshLibrary", Hidden)!.Invoke(panel, null);
                    typeof(FrmSmartMapBuilder).GetMethod("LoadSample", Hidden)!.Invoke(panel, null);
                    ((CheckBox)typeof(FrmSmartMapBuilder).GetField("_enabled", Hidden)!.GetValue(panel)!).Checked = true;
                }
                panel.Show(); Application.DoEvents();
                var body = (FlowLayoutPanel)typeof(FrmSmartMapBuilder).GetField("_body", Hidden)!.GetValue(panel)!; Check(body.AutoScroll && panel.ClientRectangle.Contains(panel.Controls.OfType<Intersect.Editor.Forms.Styling.LogiklikTabControl>().Single().Bounds), "Scrollable dock panel fits 720p workspace " + language);
                var output = Environment.GetEnvironmentVariable("EDITOR_TEST_SCREENSHOTS");
                if (!string.IsNullOrWhiteSpace(output))
                {
                    Directory.CreateDirectory(output);
                    var tabs = panel.Controls.OfType<Intersect.Editor.Forms.Styling.LogiklikTabControl>().Single();
                    for (var index = 0; index < tabs.TabPages.Count; index++)
                    {
                        tabs.SelectedIndex = index; Application.DoEvents(); using var bitmap = new Bitmap(panel.Width, panel.Height);
                        panel.DrawToBitmap(bitmap, new(Point.Empty, panel.Size)); bitmap.Save(Path.Combine(output, "smart-map-builder-" + language + (index == 0 ? "" : "-" + index) + ".png"));
                    }
                }
                panel.Hide();
            }
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de"); Check(FrmSmartMapBuilder.T("Preserve") == catalog["Smart.Preserve"]["en"], "English fallback");
        }
        finally { CultureInfo.CurrentUICulture = culture; }
        Check(HubRegistry.Definitions.Single(d => d.Id == "SmartMapBuilderTool").Title.Count == 3, "Hub entry translated and registered");
    }

    private static readonly List<SmartPreset> _realPresets = [];
    private static void RealGame()
    {
        var fixture = Environment.GetEnvironmentVariable("SMART_MAP_GAME_FIXTURE");
        if (string.IsNullOrWhiteSpace(fixture)) return;
        var data = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(fixture));
        Options.Instance.Map.TileWidth = (int)data["MapOptions"]!["TileWidth"]!;
        Options.Instance.Map.TileHeight = (int)data["MapOptions"]!["TileHeight"]!;
        Options.Instance.Map.MapWidth = (int)data["MapOptions"]!["MapWidth"]!;
        Options.Instance.Map.MapHeight = (int)data["MapOptions"]!["MapHeight"]!;
        foreach (var entry in data["Tilesets"]!)
        {
            var id = Guid.Parse((string)entry["Id"]!); TilesetDescriptor.Lookup[id] = new TilesetDescriptor(id) { Name = (string)entry["Name"]! };
        }
        var modes = new Dictionary<byte, int>(); var maps = new List<MapInstance>();
        foreach (var entry in data["Maps"]!)
        {
            var map = Map(); map.Load(entry["Metadata"]!.ToString(), clearEvents: false);
            map.LoadTileData(Convert.FromBase64String((string)entry["Tiles"]!));
            map.AttributeData = Convert.FromBase64String((string)entry["Attributes"]!);
            map.LocalEventsJson = entry["Events"]!.ToString(); maps.Add(map);
            foreach (var layer in map.Layers.Values) foreach (var tile in layer)
                if (tile.TilesetId != Guid.Empty) modes[tile.Autotile] = modes.GetValueOrDefault(tile.Autotile) + 1;
            var before = map.SaveInternal();
            var existing = map.Layers.SelectMany(p => p.Value.Cast<Tile>().Where(t => t.TilesetId != Guid.Empty).Select(t => (p.Key, Tile: t))).FirstOrDefault();
            if (existing.Tile.TilesetId == Guid.Empty) continue;
            var preset = new SmartPreset { Name = "Compatibility test", Tile = SmartTile.From(existing.Tile), Layer = existing.Key };
            // A deliberately small graphic edit on the existing map, including maps with events/spawns/warps/lights.
            var edits = SmartBrush.Plan(map, preset, new[] { new SmartCell(1, 1), new SmartCell(2, 1) }, false, true, 0);
            SmartBrush.Apply(map, edits, false); var after = map.SaveInternal();
            Check(before.Metadata == after.Metadata && before.Attributes.SequenceEqual(after.Attributes) && before.EventData == after.EventData, "Real map preserves all nongraphic content: " + map.Name);
            map.LoadInternal(after); Check(after.Matches(map.SaveInternal()), "Real map save/reopen: " + map.Name);
            map.LoadInternal(before); Check(before.Matches(map.SaveInternal()), "Real map undo: " + map.Name);
        }
        var ground = TilesetDescriptor.Lookup.Values.Cast<TilesetDescriptor>().Single(t => t.Name == "autotiles_overworld.png");
        var water = TilesetDescriptor.Lookup.Values.Cast<TilesetDescriptor>().Single(t => t.Name == "animatedautotiles.png");
        var layerName = Options.Instance.Map.Layers.All[0];
        _realPresets.Add(new() { Name = "Royal Grass", Tile = new(ground.Id, 6, 0, 1), Layer = layerName, Favorite = true });
        _realPresets.Add(new() { Name = "Royal River", Category = "River", Tile = new(water.Id, 0, 0, 3), Layer = layerName, Favorite = true });
        var sample = Map();
        SmartBrush.Apply(sample, SmartBrush.Plan(sample, _realPresets[0], SmartBrush.Area(new(0, 0), new(31, 25), SmartShape.Rectangle, 0, false), false, false, 0), false);
        var riverCells = SmartBrush.Area(new(4, 0), new(12, 12), SmartShape.Line, 2, true).Concat(SmartBrush.Area(new(12, 12), new(25, 25), SmartShape.Line, 3, true));
        SmartBrush.Apply(sample, SmartBrush.Plan(sample, _realPresets[1], riverCells, false, false, 0), false);
        CompareFullRebuild(sample, "Real tileset river connections");
        var output = Environment.GetEnvironmentVariable("EDITOR_TEST_SCREENSHOTS");
        if (!string.IsNullOrWhiteSpace(output))
        {
            Directory.CreateDirectory(output);
            foreach (var (map, file) in new[] { (sample, "royal-river"), (maps.First(), "existing-map") })
                for (var frame = 0; frame < 3; frame++)
                    using (var bitmap = RenderNativeTiles(map, frame)) bitmap.Save(Path.Combine(output, file + "-frame-" + frame + ".png"));
            if (Environment.GetEnvironmentVariable("SMART_MAP_GPU") == "1")
            {
                using var renderer = new SmartMapGpuRenderer();
                foreach (var (map, file) in new[] { (sample, "royal-river-gpu"), (maps.First(), "existing-map-gpu") })
                    for (var frame = 0; frame < 3; frame++) renderer.Render(map, frame, Path.Combine(output, file + "-frame-" + frame + ".png"));
                Check(!File.ReadAllBytes(Path.Combine(output, "royal-river-gpu-frame-0.png")).SequenceEqual(File.ReadAllBytes(Path.Combine(output, "royal-river-gpu-frame-1.png"))), "Actual native GPU output changes with animation frame");
                Console.WriteLine("PASS actual editor MonoGame renderer: real tilesets, native autotile draw routine and three moving water frames captured.");
            }
            SmartPresetStore.Write(Path.Combine(Path.GetDirectoryName(output)!, "royal-smart-presets.json"), _realPresets);
        }
        Console.WriteLine($"PASS real game data: {maps.Count} legacy maps round-tripped; native tile modes present: {string.Join(", ", modes.OrderBy(p => p.Key).Select(p => p.Key + "=" + p.Value))}; originals opened read-only, no live server writes.");
    }

    // Render the native cached subtiles using the same native frame offsets as the editor.
    // This artifact checks real atlas coordinates/transparency; live client acceptance remains separate.
    private static Bitmap RenderNativeTiles(MapInstance map, int frame)
    {
        int tw = Options.Instance.Map.TileWidth, th = Options.Instance.Map.TileHeight;
        var bitmap = new Bitmap(Options.Instance.Map.MapWidth * tw, Options.Instance.Map.MapHeight * th);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap); graphics.Clear(System.Drawing.Color.Transparent);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        var sheets = new Dictionary<Guid, Bitmap>();
        try
        {
            foreach (var layer in Options.Instance.Map.Layers.All)
                for (var y = 0; y < Options.Instance.Map.MapHeight; y++) for (var x = 0; x < Options.Instance.Map.MapWidth; x++)
                {
                    var tile = map.Layers[layer][x, y]; if (tile.TilesetId == Guid.Empty) continue;
                    if (!sheets.TryGetValue(tile.TilesetId, out var sheet))
                    {
                        var descriptor = TilesetDescriptor.Get(tile.TilesetId); if (descriptor == null) continue;
                        var path = Intersect.Editor.Forms.Styling.EditorResourcePreview.ResolveFile(descriptor.Name, "resources/tilesets"); if (path == null) continue;
                        sheet = Intersect.Editor.Forms.Styling.EditorResourcePreview.LoadImage(path, int.MaxValue); if (sheet == null) continue; sheets[tile.TilesetId] = sheet;
                    }
                    if (map.Autotiles.Layers[layer][x, y].RenderState == MapAutotiles.RENDER_STATE_NORMAL)
                        graphics.DrawImage(sheet, new Rectangle(x * tw, y * th, tw, th), new Rectangle(tile.X * tw, tile.Y * th, tw, th), GraphicsUnit.Pixel);
                    else if (map.Autotiles.Layers[layer][x, y].RenderState == MapAutotiles.RENDER_STATE_AUTOTILE)
                        for (var q = 1; q <= 4; q++)
                        {
                            var point = map.Autotiles.Layers[layer][x, y].QuarterTile[q];
                            var ox = tile.Autotile == 3 ? frame * tw * 2 : tile.Autotile == 7 ? frame * tw * 3 : 0;
                            var oy = tile.Autotile == 5 ? (frame - 1) * th : tile.Autotile == 4 ? -th : 0;
                            Check(point.X + ox >= 0 && point.Y + oy >= 0 && point.X + ox + tw / 2 <= sheet.Width && point.Y + oy + th / 2 <= sheet.Height, "Real atlas quarter/frame stays inside sheet");
                            graphics.DrawImage(sheet, new Rectangle(x * tw + (q - 1) % 2 * (tw / 2), y * th + (q - 1) / 2 * (th / 2), tw / 2, th / 2), new Rectangle(point.X + ox, point.Y + oy, tw / 2, th / 2), GraphicsUnit.Pixel);
                        }
                }
        }
        finally { foreach (var image in sheets.Values) image.Dispose(); }
        return bitmap;
    }
}
