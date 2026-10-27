using System.Globalization;
using System.Reflection;
using Console = System.Console;
using Point = System.Drawing.Point;
using Color = System.Drawing.Color;
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
        Geometry(); ResourceValidation(); PresetStorage(); Protection(); NativeFormats(); SoftWater(); GroundShore(); GroundPaint(); NeighborBorders(); InputTransaction(); RealGame(); LocalizationAndPanel();
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
    private static void GroundPaint()
    {
        using var original = new Bitmap(16, 16);
        using (var graphics = System.Drawing.Graphics.FromImage(original)) graphics.Clear(Color.FromArgb(80, 100, 60));
        original.SetPixel(3, 3, Color.FromArgb(160, 200, 120));
        using var paint = new SmartGroundPaint(original);
        paint.Colorize(Color.SaddleBrown, false);
        Check(paint.Image.GetPixel(3, 3).R > paint.Image.GetPixel(4, 3).R, "Tint preserves texture luminance details");
        Check(original.GetPixel(3, 3).G == 200, "Ground editing never changes the source atlas");
        paint.Undo(); Check(paint.Image.GetPixel(3, 3) == original.GetPixel(3, 3), "Undo tint");
        paint.Colorize(Color.Brown, true); Check(paint.Image.GetPixel(3, 3) == Color.FromArgb(255, Color.Brown), "Solid color replaces texture");
        paint.Checkpoint(); paint.Stroke(new(0, 0), new(15, 15), Color.White);
        Check(Enumerable.Range(0, 16).All(i => paint.Image.GetPixel(i, i).ToArgb() == Color.White.ToArgb()), "Fast pixel dragging leaves no gaps");
        paint.Undo(); Check(paint.Image.GetPixel(0, 0) == Color.FromArgb(255, Color.Brown), "Whole pixel stroke undoes once");
        paint.Reset(); Check(paint.Image.GetPixel(3, 3) == original.GetPixel(3, 3), "Restore original ground");
        paint.Undo(); Check(paint.Image.GetPixel(3, 3) == Color.FromArgb(255, Color.Brown), "Restore itself is reversible");
        using var dialog = new FrmSmartGroundPaint(original); dialog.Show(); Application.DoEvents();
        var output = Environment.GetEnvironmentVariable("EDITOR_TEST_SCREENSHOTS");
        if (!string.IsNullOrEmpty(output)) { Directory.CreateDirectory(output); using var image = new Bitmap(dialog.Width, dialog.Height); dialog.DrawToBitmap(image, new(Point.Empty, dialog.Size)); image.Save(Path.Combine(output, "smart-ground-paint.png")); }
        dialog.Hide();
    }
    private static void GroundShore()
    {
        var originalId = Guid.NewGuid(); var variantId = Guid.NewGuid(); var landId = Guid.NewGuid();
        TilesetDescriptor.Lookup[originalId] = new TilesetDescriptor(originalId) { Name = "fixture_water.png" };
        var name = GroundShoreVariant.FileName(new Tile { TilesetId = originalId, Autotile = 3 }, "0123456789abcdefabcd");
        TilesetDescriptor.Lookup[variantId] = new TilesetDescriptor(variantId) { Name = name };
        TilesetDescriptor.Lookup[landId] = new TilesetDescriptor(landId) { Name = "fixture_land.png" };
        try
        {
            var preset = Preset(3); preset.Tile = new(originalId, 0, 0, 3);
            preset.GroundShore = true; preset.GroundLandTile = new(landId, 0, 0, 0); preset.GroundWaterTile = preset.Tile with { TilesetId = variantId };
            preset.Validate(); var copy = preset.Copy(); Check(copy.GroundShore && copy.GroundLandTile == preset.GroundLandTile, "Ground recipe survives serialization");
            Check(GroundShoreVariant.Connects(preset.Tile.Native, preset.GroundWaterTile.Native), "Generated bank and original water connect");
            Check(!GroundShoreVariant.Connects(preset.Tile.Native, (preset.GroundWaterTile with { Y = 3 }).Native), "Unmodified atlas rows keep their independent identity");
            var map = Map(); var ground = preset.Layer; var mask = Options.Instance.Map.Layers.All[1];
            for (var y = 0; y < Options.Instance.Map.MapHeight; y++) for (var x = 0; x < Options.Instance.Map.MapWidth; x++) map.Layers[ground][x, y] = preset.Tile.Native;
            map.Layers[mask][8, 8] = Preset(7).Tile.Native;
            map.Layers[ground][9, 8] = Preset(0).Tile.Native;
            var before = map.SaveInternal();
            var edits = SmartBrush.Plan(map, preset, new[] { new SmartCell(8, 8) }, true, false, 0);
            Check(edits.Count == 8 && edits.All(e => e.Layer == ground), "One land cell plus water halo, only on Ground");
            Check(before.Matches(map.SaveInternal()), "Island planning leaves map and neighbor data untouched");
            SmartBrush.Apply(map, edits, false);
            Check(SmartTile.From(map.Layers[ground][8, 8]) == preset.GroundLandTile && map.Layers[mask][8, 8].Autotile == 7, "Island paints through water with preserve enabled and leaves Mask 1 intact");
            Check(SmartTile.From(map.Layers[ground][9, 8]) == Preset(0).Tile, "Water halo preserves other terrain");
            Check(SmartTile.From(map.Layers[ground][2, 2]) == preset.Tile, "Distant banks are unchanged");
            var erase = SmartBrush.Plan(map, preset, new[] { new SmartCell(8, 8) }, true, true, 0); SmartBrush.Apply(map, erase, false);
            Check(SmartTile.From(map.Layers[ground][8, 8]) == preset.Tile, "Erasing island restores water on Ground");
            map.LoadInternal(before); Check(before.Matches(map.SaveInternal()), "Undo restores the complete island operation");
            // Every map edge, both directions, uses exactly the native connected cache.
            foreach (var direction in new[] { new SmartCell(-1, 0), new SmartCell(1, 0), new SmartCell(0, -1), new SmartCell(0, 1) })
            {
                var center = new NeighborMap(); var neighbor = new NeighborMap();
                foreach (var layer in Options.Instance.Map.Layers.All)
                { center.Layers[layer] = new Tile[Options.Instance.Map.MapWidth, Options.Instance.Map.MapHeight]; neighbor.Layers[layer] = new Tile[Options.Instance.Map.MapWidth, Options.Instance.Map.MapHeight]; }
                center.Grid[direction.X + 1, direction.Y + 1] = neighbor; neighbor.Grid[1 - direction.X, 1 - direction.Y] = center;
                int cx = direction.X < 0 ? 0 : direction.X > 0 ? Options.Instance.Map.MapWidth - 1 : 4;
                int cy = direction.Y < 0 ? 0 : direction.Y > 0 ? Options.Instance.Map.MapHeight - 1 : 4;
                int nx = direction.X < 0 ? Options.Instance.Map.MapWidth - 1 : direction.X > 0 ? 0 : 4;
                int ny = direction.Y < 0 ? Options.Instance.Map.MapHeight - 1 : direction.Y > 0 ? 0 : 4;
                center.Layers[ground][cx, cy] = preset.GroundWaterTile.Native; neighbor.Layers[ground][nx, ny] = preset.Tile.Native;
                center.InitAutotiles(); neighbor.InitAutotiles(); var neighborBefore = neighbor.SaveInternal();
                Check(center.Autotiles.CheckTileMatch(ground, cx, cy, cx + direction.X, cy + direction.Y, center.GenerateAutotileGrid()), "Ground water connects outward " + direction);
                Check(neighbor.Autotiles.CheckTileMatch(ground, nx, ny, nx - direction.X, ny - direction.Y, neighbor.GenerateAutotileGrid()), "Ground water connects inward " + direction);
                Check(neighborBefore.Matches(neighbor.SaveInternal()), "Connecting does not rewrite neighbor map " + direction);
            }
            int tw = Options.Instance.Map.TileWidth, th = Options.Instance.Map.TileHeight;
            using var waterImage = new Bitmap(tw * 6, th * 3); using (var graphics = System.Drawing.Graphics.FromImage(waterImage)) graphics.Clear(Color.CornflowerBlue);
            waterImage.SetPixel(0, 0, Color.Lime);
            using var landImage = new Bitmap(tw, th); using (var graphics = System.Drawing.Graphics.FromImage(landImage)) graphics.Clear(Color.SaddleBrown);
            using var shore = SmartWaterShore.CreateGround(waterImage, preset.Tile, landImage, preset.GroundLandTile, 2);
            Check(shore.GetPixel(0, 0).ToArgb() == Color.SaddleBrown.ToArgb(), "Ground bank replaces green with chosen land color");
            for (var y = 0; y < shore.Height; y++) for (var x = 0; x < shore.Width; x++) Check(shore.GetPixel(x, y).A == 255, "Ground shore is fully opaque");
            Check(waterImage.GetPixel(0, 0).ToArgb() == Color.Lime.ToArgb(), "Generating Ground shore preserves source artwork");
            for (var frame = 0; frame < 3; frame++) Check(shore.GetPixel(frame * tw * 2 + tw, th * 2).ToArgb() == waterImage.GetPixel(frame * tw * 2 + tw, th * 2).ToArgb(), "Water animation frame remains unchanged " + frame);
        }
        finally { foreach (var id in new[] { originalId, variantId, landId }) TilesetDescriptor.Lookup.Delete(TilesetDescriptor.Get(id)); }
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
    private static void SoftWater()
    {
        foreach (byte mode in new byte[] { 1, 3, 6, 7 })
        {
            var preset = Preset(mode); var tile = preset.Tile;
            int tw = Options.Instance.Map.TileWidth, th = Options.Instance.Map.TileHeight;
            var size = tile.Footprint; using var source = new Bitmap(size.Width * tw, size.Height * th);
            int frames = mode is 3 or 7 ? 3 : 1, fw = source.Width / frames;
            using (var g = System.Drawing.Graphics.FromImage(source)) g.Clear(System.Drawing.Color.CornflowerBlue);
            // The native interior is away from the upper-left outside-bank sample in VX and XP.
            source.SetPixel(0, 0, System.Drawing.Color.Yellow);
            using var variant = SmartWaterShore.Create(source, tile, 2);
            Check(source.GetPixel(0, 0).A == 255 && variant.GetPixel(0, 0).A == 0, "Optional shore removes bank without editing source " + mode);
            Check(variant.GetPixel(1, 0).A is > 0 and < 255, "Shore feathers water alpha " + mode);
            Check(variant.GetPixel(fw - 1, source.Height - 1).A == 255, "Atlas edges do not introduce transparent seams " + mode);
            if (frames == 3) Check(variant.GetPixel(fw, 0).A == 255, "Animation frames have independent masks " + mode);
            using var hard = SmartWaterShore.Create(source, tile, 0);
            Check(hard.GetPixel(1, 0).A == 255, "Zero width keeps a crisp transparent shore " + mode);
            preset.SoftWater = true; preset.SoftWaterTile = tile with { TilesetId = Guid.NewGuid() }; preset.SoftWaterLayer = Options.Instance.Map.Layers.All[1];
            preset.Validate(); var copy = preset.Copy();
            Check(copy.SoftWater && copy.PaintTile == preset.SoftWaterTile && copy.PaintLayer == preset.SoftWaterLayer, "Optional shore settings persist " + mode);
            var map = Map(); map.Layers[preset.Layer][3, 3] = Preset(0).Tile.Native;
            var edits = SmartBrush.Plan(map, preset, new[] { new SmartCell(3, 3) }, true, false, 0);
            Check(edits.Count == 1 && edits[0].Layer == preset.SoftWaterLayer && edits[0].After.Autotile == mode, "Soft water paints above preserved terrain " + mode);
            SmartBrush.Apply(map, edits, false);
            Check(SmartBrush.Plan(map, preset, new[] { new SmartCell(3, 3) }, true, true, 0).Single().Layer == preset.SoftWaterLayer, "Soft water eraser preserves grass " + mode);
            preset.SoftWater = false;
            Check(preset.PaintTile == tile && preset.PaintLayer == preset.Layer, "Unchecked option restores original source " + mode);
        }
    }
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
        using var waterSource = Intersect.Editor.Forms.Styling.EditorResourcePreview.LoadImage(Path.Combine("resources/tilesets", water.Name), int.MaxValue)!;
        using var softAtlas = SmartWaterShore.Create(waterSource, _realPresets[1].Tile, 2);
        using var softBytes = new MemoryStream(); softAtlas.Save(softBytes, System.Drawing.Imaging.ImageFormat.Png);
        var softName = "smart_water_" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(softBytes.ToArray()))[..20].ToLowerInvariant() + ".png";
        File.WriteAllBytes(Path.Combine("resources/tilesets", softName), softBytes.ToArray());
        var softId = Guid.NewGuid(); TilesetDescriptor.Lookup[softId] = new TilesetDescriptor(softId) { Name = softName };
        var softPreset = _realPresets[1].Copy(); softPreset.SoftWater = true; softPreset.SoftWaterTile = softPreset.Tile with { TilesetId = softId }; softPreset.SoftWaterLayer = Options.Instance.Map.Layers.All[1];
        using (var panel = new FrmSmartMapBuilder())
        {
            typeof(FrmSmartMapBuilder).GetField("_working", Hidden)!.SetValue(panel, _realPresets[1].Copy());
            ((TextBox)typeof(FrmSmartMapBuilder).GetField("_name", Hidden)!.GetValue(panel)!).Text = "Royal River";
            panel.Show(); Application.DoEvents();
            var toggle = (CheckBox)typeof(FrmSmartMapBuilder).GetField("_softWater", Hidden)!.GetValue(panel)!;
            Check(!toggle.Checked, "No-sand option is off by default");
            typeof(FrmSmartMapBuilder).GetMethod("CreateSoftWater", Hidden)!.Invoke(panel, null);
            var settings = panel.Settings(true)!;
            Check(toggle.Checked && settings.SoftWater && settings.PaintTile.TilesetId == softId && settings.PaintLayer == softPreset.SoftWaterLayer, "Native variant registration resolves the optional brush and selects the upper layer");
            toggle.Checked = false;
            Check(panel.Settings(true)!.PaintTile == _realPresets[1].Tile, "Unchecking the UI restores the original water source");
            panel.Hide();
        }
        var softMap = Map();
        SmartBrush.Apply(softMap, SmartBrush.Plan(softMap, _realPresets[0], SmartBrush.Area(new(0, 0), new(31, 25), SmartShape.Rectangle, 0, false), false, false, 0), false);
        var baseTiles = softMap.GenerateTileData();
        SmartBrush.Apply(softMap, SmartBrush.Plan(softMap, softPreset, riverCells, true, false, 0), false);
        Check(softMap.Layers[layerName].Cast<Tile>().All(t => SmartTile.From(t) == _realPresets[0].Tile), "Real grass under soft water remains intact");
        CompareFullRebuild(softMap, "Soft water native connections");
        var waterCells = riverCells.ToHashSet();
        var interiorCell = waterCells.First(c => c.X > 0 && c.Y > 0 && c.X + 1 < Options.Instance.Map.MapWidth && c.Y + 1 < Options.Instance.Map.MapHeight
            && SmartBrush.Area(c, c, SmartShape.Brush, 1, false).All(waterCells.Contains));
        for (var frame = 0; frame < 3; frame++) for (var q = 1; q <= 4; q++)
        {
            var point = softMap.Autotiles.Layers[softPreset.SoftWaterLayer][interiorCell.X, interiorCell.Y].QuarterTile[q];
            for (var y = 0; y < Options.Instance.Map.TileHeight / 2; y++) for (var x = 0; x < Options.Instance.Map.TileWidth / 2; x++)
                Check(softAtlas.GetPixel(point.X + frame * Options.Instance.Map.TileWidth * 2 + x, point.Y + y).A == 255, "Connected water interiors remain opaque: no tile-grid seams");
        }
        var output = Environment.GetEnvironmentVariable("EDITOR_TEST_SCREENSHOTS");
        if (!string.IsNullOrWhiteSpace(output))
        {
            Directory.CreateDirectory(output);
            RealGroundIsland(waterSource, water.Id, output);
            foreach (var (map, file) in new[] { (sample, "royal-river"), (maps.First(), "existing-map") })
                for (var frame = 0; frame < 3; frame++)
                    using (var bitmap = RenderNativeTiles(map, frame)) bitmap.Save(Path.Combine(output, file + "-frame-" + frame + ".png"));
            if (Environment.GetEnvironmentVariable("SMART_MAP_GPU") == "1")
            {
                using var renderer = new SmartMapGpuRenderer();
                foreach (var (map, file) in new[] { (sample, "royal-river-gpu"), (softMap, "royal-river-soft-gpu"), (maps.First(), "existing-map-gpu") })
                    for (var frame = 0; frame < 3; frame++) renderer.Render(map, frame, Path.Combine(output, file + "-frame-" + frame + ".png"));
                Check(!File.ReadAllBytes(Path.Combine(output, "royal-river-gpu-frame-0.png")).SequenceEqual(File.ReadAllBytes(Path.Combine(output, "royal-river-gpu-frame-1.png"))), "Actual native GPU output changes with animation frame");
                Console.WriteLine("PASS actual editor MonoGame renderer: real tilesets, native autotile draw routine and three moving water frames captured.");
            }
            SmartPresetStore.Write(Path.Combine(Path.GetDirectoryName(output)!, "royal-smart-presets.json"), _realPresets);
            File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(output)!, softName), softBytes.ToArray());
        }
        Console.WriteLine($"PASS real game data: {maps.Count} legacy maps round-tripped; native tile modes present: {string.Join(", ", modes.OrderBy(p => p.Key).Select(p => p.Key + "=" + p.Value))}; originals opened read-only, no live server writes.");
    }

    private static void RealGroundIsland(Bitmap waterSource, Guid waterId, string output)
    {
        var ground = Options.Instance.Map.Layers.All[0];
        var water = new SmartTile(waterId, 0, 0, 3);
        using var land = new Bitmap(Options.Instance.Map.TileWidth, Options.Instance.Map.TileHeight);
        using (var graphics = System.Drawing.Graphics.FromImage(land)) graphics.Clear(Color.FromArgb(129, 86, 60));
        // This is what the built-in pixel editor writes, without a hand-authored external ground image.
        using var paint = new SmartGroundPaint(land); paint.Checkpoint();
        paint.Stroke(new(3, 5), new(5, 5), Color.FromArgb(116, 76, 52));
        paint.Stroke(new(11, 10), new(12, 10), Color.FromArgb(142, 97, 68));
        using var bytes = new MemoryStream(); paint.Image.Save(bytes, System.Drawing.Imaging.ImageFormat.Png);
        var landName = "smart_land_" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes.ToArray()))[..20].ToLowerInvariant() + ".png";
        File.WriteAllBytes(Path.Combine("resources/tilesets", landName), bytes.ToArray());
        var landId = Guid.NewGuid(); TilesetDescriptor.Lookup[landId] = new TilesetDescriptor(landId) { Name = landName };
        var landTile = new SmartTile(landId, 0, 0, 0);
        using var atlas = SmartWaterShore.CreateGround(waterSource, water, paint.Image, landTile, 2);
        using var atlasBytes = new MemoryStream(); atlas.Save(atlasBytes, System.Drawing.Imaging.ImageFormat.Png);
        var name = GroundShoreVariant.FileName(water.Native, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(atlasBytes.ToArray()))[..20].ToLowerInvariant());
        File.WriteAllBytes(Path.Combine("resources/tilesets", name), atlasBytes.ToArray());
        var variantId = Guid.NewGuid(); TilesetDescriptor.Lookup[variantId] = new TilesetDescriptor(variantId) { Name = name };
        var preset = new SmartPreset { Name = "Brown Ground Island", Tile = water, Layer = ground, GroundShore = true, GroundLandTile = landTile, GroundWaterTile = water with { TilesetId = variantId } };
        preset.Validate();
        var map = Map();
        for (var y = 0; y < Options.Instance.Map.MapHeight; y++) for (var x = 0; x < Options.Instance.Map.MapWidth; x++) map.Layers[ground][x, y] = water.Native;
        map.InitAutotiles();
        var cells = SmartBrush.Area(new(7, 5), new(25, 20), SmartShape.Ellipse, 0, false).ToArray();
        SmartBrush.Apply(map, SmartBrush.Plan(map, preset, cells, true, false, 0), false);
        Check(map.Layers.Where(p => p.Key != ground).All(p => p.Value.Cast<Tile>().All(t => t.TilesetId == Guid.Empty)), "Real Ground island leaves all Mask/Fringe layers empty");
        CompareFullRebuild(map, "Real Ground island caches");
        using (var panel = new FrmSmartMapBuilder())
        {
            typeof(FrmSmartMapBuilder).GetField("_working", Hidden)!.SetValue(panel, new SmartPreset { Name = "Ground recipe", Tile = water, Layer = ground });
            typeof(FrmSmartMapBuilder).GetField("_landTile", Hidden)!.SetValue(panel, landTile);
            panel.Show(); Application.DoEvents();
            typeof(FrmSmartMapBuilder).GetMethod("CreateGroundShore", Hidden)!.Invoke(panel, null);
            var settings = panel.Settings(true);
            Check(settings is { GroundShore: true, SoftWater: false } && settings.PaintLayer == ground && settings.GroundWaterTile?.TilesetId == variantId, "Ground preparation resolves registered asset and configures one-layer island brush");
            panel.Hide();
        }
        for (var frame = 0; frame < 3; frame++)
            using (var image = RenderNativeTiles(map, frame)) image.Save(Path.Combine(output, $"brown-ground-island-frame-{frame}.png"));
        if (Environment.GetEnvironmentVariable("SMART_MAP_GPU") == "1")
        {
            using var renderer = new SmartMapGpuRenderer();
            for (var frame = 0; frame < 3; frame++) renderer.Render(map, frame, Path.Combine(output, $"brown-ground-island-gpu-frame-{frame}.png"));
        }
        var destination = Path.GetDirectoryName(output)!;
        File.WriteAllBytes(Path.Combine(destination, landName), bytes.ToArray());
        File.WriteAllBytes(Path.Combine(destination, name), atlasBytes.ToArray());
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
