using Intersect;
using Intersect.Editor.Maps;
using Intersect.Editor.Forms.DockingElements;
using Intersect.Editor.General;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.Mapping.Tilesets;
using Color = System.Drawing.Color;

internal static partial class SmartMapBuilderTests
{
    private static void RealWaterConnection(Bitmap atlas, Guid id, string output)
    {
        var reference = new SmartTile(id, 0, 0, 3); var selected = new SmartTile(id, 0, 27, 3);
        using var image = SmartWaterConnection.Create(atlas, reference, atlas, selected);
        using var bytes = new MemoryStream(); image.Save(bytes, System.Drawing.Imaging.ImageFormat.Png);
        var name = GroundShoreVariant.FileName(reference.Native, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes.ToArray()))[..20].ToLowerInvariant());
        File.WriteAllBytes(Path.Combine("resources/tilesets", name), bytes.ToArray());
        var alias = Guid.NewGuid(); TilesetDescriptor.Lookup[alias] = new TilesetDescriptor(alias) { Name = name };
        try
        {
            using var panel = new FrmSmartMapBuilder();
            typeof(FrmSmartMapBuilder).GetField("_working", Hidden)!.SetValue(panel, new SmartPreset { Name = "Connected water", Tile = reference, Layer = Options.Instance.Map.Layers.All[0] });
            ((TextBox)typeof(FrmSmartMapBuilder).GetField("_name", Hidden)!.GetValue(panel)!).Text = "Connected water";
            Globals.CurrentTileset = TilesetDescriptor.Get(id); Globals.CurSelX = 0; Globals.CurSelY = 27; Globals.Autotilemode = 3;
            panel.Show(); Application.DoEvents();
            typeof(FrmSmartMapBuilder).GetMethod("CreateWaterConnection", Hidden)!.Invoke(panel, null);
            var preset = panel.Settings(true)!;
            Check(panel.Shape == SmartShape.ConnectWater && preset.ConnectedWaterSource == selected && preset.Tile.TilesetId == alias && panel.Preserve,
                "Real palette connection selects dedicated conversion, retains protection setting and registers native PNG");
            var map = Map(); var ground = preset.Layer;
            for (var y = 0; y < Options.Instance.Map.MapHeight; y++) for (var x = 0; x < Options.Instance.Map.MapWidth; x++)
                map.Layers[ground][x, y] = (x < Options.Instance.Map.MapWidth / 2 ? reference : selected).Native;
            map.InitAutotiles();
            using (var before = RenderNativeTiles(map, 0)) before.Save(Path.Combine(output, "water-connection-before.png"));
            SmartBrush.Apply(map, SmartBrush.ConnectWater(map, preset, new(Options.Instance.Map.MapWidth - 1, 3), false), true);
            var sea = Map();
            for (var y = 0; y < Options.Instance.Map.MapHeight; y++) for (var x = 0; x < Options.Instance.Map.MapWidth; x++) sea.Layers[ground][x, y] = reference.Native;
            sea.InitAutotiles();
            using var gpu = Environment.GetEnvironmentVariable("SMART_MAP_GPU") == "1" ? new SmartMapGpuRenderer() : null;
            for (var frame = 0; frame < 3; frame++)
            {
                using var after = RenderNativeTiles(map, frame); using var expected = RenderNativeTiles(sea, frame);
                after.Save(Path.Combine(output, $"water-connection-after-{frame}.png"));
                gpu?.Render(map, frame, Path.Combine(output, $"water-connection-gpu-{frame}.png"));
                int seam = Options.Instance.Map.MapWidth / 2 * Options.Instance.Map.TileWidth;
                for (var y = Options.Instance.Map.TileHeight; y < after.Height - Options.Instance.Map.TileHeight; y++)
                    for (var x = seam - Options.Instance.Map.TileWidth; x < seam + Options.Instance.Map.TileWidth; x++)
                        Check(after.GetPixel(x, y) == expected.GetPixel(x, y), "Real row 27 water joins row 0 without bank pixels, frame " + frame);
            }
            panel.Hide();
        }
        finally { TilesetDescriptor.Lookup.DeleteAt(alias); }
    }

    private static void WaterConnections()
    {
        foreach (byte mode in new byte[] { 1, 3, 6, 7 })
        {
            var reference = Preset(mode).Tile;
            var appearance = reference with { Y = 6 };
            int tw = Options.Instance.Map.TileWidth, th = Options.Instance.Map.TileHeight;
            var size = reference.Footprint;
            using var original = new Bitmap(size.Width * tw, 12 * th);
            for (var y = 0; y < original.Height; y++) for (var x = 0; x < original.Width; x++)
                original.SetPixel(x, y, Color.FromArgb(255, (x * 7) % 255, (y * 11) % 255, (x + y) % 255));
            using var image = SmartWaterConnection.Create(original, reference, original, appearance);
            for (var y = 0; y < size.Height * th; y++) for (var x = 0; x < size.Width * tw; x++)
                Check(image.GetPixel(x, y) == original.GetPixel(x, 6 * th + y), "Relocation preserves every native animation pixel " + mode);
            var originalId = reference.TilesetId; var aliasId = Guid.NewGuid();
            TilesetDescriptor.Lookup[originalId] = new TilesetDescriptor(originalId) { Name = "water-fixture.png" };
            TilesetDescriptor.Lookup[aliasId] = new TilesetDescriptor(aliasId) { Name = GroundShoreVariant.FileName(reference.Native, "0123456789abcdefabcd") };
            try
            {
                var preset = Preset(mode); preset.Tile = reference with { TilesetId = aliasId }; preset.ConnectedWaterSource = appearance;
                preset.Validate(); Check(preset.Copy().ConnectedWaterSource == appearance, "Connection conversion source survives preset storage");
                var map = Map(); var ground = preset.Layer; var mask = Options.Instance.Map.Layers.All[1];
                for (var y = 0; y < Options.Instance.Map.MapHeight; y++) for (var x = 0; x < Options.Instance.Map.MapWidth; x++) map.Layers[ground][x, y] = reference.Native;
                map.Layers[ground][3, 3] = appearance.Native; map.Layers[ground][7, 7] = appearance.Native;
                map.Layers[mask][3, 3] = appearance.Native; map.InitAutotiles(); var before = map.SaveInternal();
                Check(!map.Autotiles.CheckTileMatch(ground, 3, 3, 4, 3, map.GenerateAutotileGrid()), "Different water rows initially retain their border");
                Check(SmartBrush.ConnectWater(map, preset, new(4, 3), false).Count == 0, "Wrong clicked terrain cannot be converted");
                var edits = SmartBrush.ConnectWater(map, preset, new(3, 3), false);
                Check(edits.Count == 2 && before.Matches(map.SaveInternal()), "Only matching Ground tiles planned; map remains untouched before commit");
                SmartBrush.Apply(map, edits, true);
                Check(map.Autotiles.CheckTileMatch(ground, 3, 3, 4, 3, map.GenerateAutotileGrid()) && map.Autotiles.CheckTileMatch(ground, 4, 3, 3, 3, map.GenerateAutotileGrid()), "Converted water connects in both directions " + mode);
                Check(SmartTile.From(map.Layers[mask][3, 3]) == appearance, "Mask 1 remains unchanged");
                CompareFullRebuild(map, "Connected water native cache " + mode);
                SmartBrush.Apply(map, SmartBrush.ConnectWater(map, preset, new(3, 3), true), true);
                Check(before.Matches(map.SaveInternal()), "Right-click restores exact original water");
                SmartBrush.Apply(map, edits, true); map.LoadInternal(before);
                Check(before.Matches(map.SaveInternal()), "Undo restores the entire conversion");
            }
            finally { TilesetDescriptor.Lookup.DeleteAt(originalId); TilesetDescriptor.Lookup.DeleteAt(aliasId); }
        }
    }
}
