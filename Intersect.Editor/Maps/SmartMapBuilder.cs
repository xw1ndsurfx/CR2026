using System.Text.Json;
using Intersect.Framework.Core.GameObjects.Maps;

namespace Intersect.Editor.Maps;

// Editor-only metadata. Maps still contain exactly the native Tile values.
internal sealed record SmartTile(Guid TilesetId, int X, int Y, byte Autotile)
{
    internal Tile Native => new() { TilesetId = TilesetId, X = X, Y = Y, Autotile = Autotile };
    internal static SmartTile From(Tile tile) => new(tile.TilesetId, tile.X, tile.Y, tile.Autotile);
    internal (int Width, int Height) Footprint => Autotile switch
    {
        0 or 2 => (1, 1), 1 or 5 => (2, 3), 3 => (6, 3), 4 => (2, 2), 6 => (3, 4), 7 => (9, 4), _ => (0, 0),
    };
    internal bool Fits(int width, int height, int tileWidth, int tileHeight)
    {
        var size = Footprint;
        return TilesetId != Guid.Empty && X >= 0 && Y >= 0 && size.Width > 0 && tileWidth > 0 && tileHeight > 0
            && (Autotile == 0 || (tileWidth % 2 == 0 && tileHeight % 2 == 0))
            && width % tileWidth == 0 && height % tileHeight == 0
            && ((long)X + size.Width) * tileWidth <= width && ((long)Y + size.Height) * tileHeight <= height;
    }
}

internal sealed record SmartStampCell(int X, int Y, string Layer, SmartTile Tile);
// Match the underlying terrain and place the configured native overlay on a second layer.
internal sealed record SmartTransition(string MatchLayer, SmartTile Match, string TargetLayer, SmartTile Overlay);
internal sealed class SmartPreset
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "Terrain";
    public SmartTile Tile { get; set; } = new(Guid.Empty, 0, 0, 0);
    public string Layer { get; set; } = "";
    public int Radius { get; set; } = 1;
    public int Density { get; set; } = 25;
    public bool Round { get; set; } = true;
    public bool Favorite { get; set; }
    public long LastUsed { get; set; }
    public List<SmartTile> Variants { get; set; } = [];
    public List<SmartTransition> Transitions { get; set; } = [];
    public List<SmartStampCell> Stamp { get; set; } = [];
    public int AnchorX { get; set; }
    public int AnchorY { get; set; }
    public int StampWidth { get; set; }
    public int StampHeight { get; set; }
    public bool SoftWater { get; set; }
    public SmartTile? SoftWaterTile { get; set; }
    public string SoftWaterLayer { get; set; } = "";
    public int WaterFeather { get; set; } = 2;
    public bool GroundShore { get; set; }
    public SmartTile? GroundLandTile { get; set; }
    public SmartTile? GroundWaterTile { get; set; }
    public SmartTile? ConnectedWaterSource { get; set; }
    internal SmartTile PaintTile => GroundShore ? GroundLandTile ?? throw new InvalidDataException("Smart.GroundShoreRequired")
        : SoftWater ? SoftWaterTile ?? throw new InvalidDataException("Smart.SoftWaterRequired") : Tile;
    internal string PaintLayer => SoftWater ? SoftWaterLayer : Layer;
    internal SmartPreset Copy() => JsonSerializer.Deserialize<SmartPreset>(JsonSerializer.Serialize(this))!;
    internal IEnumerable<SmartTile> Tiles => new[] { Tile }.Concat(Variants).Concat(Transitions.Select(t => t.Overlay))
        .Concat(Transitions.Select(t => t.Match).Where(t => t.TilesetId != Guid.Empty)).Concat(Stamp.Select(c => c.Tile))
        .Concat(SoftWater && SoftWaterTile != null ? new[] { SoftWaterTile } : Array.Empty<SmartTile>())
        .Concat(GroundShore && GroundLandTile != null && GroundWaterTile != null ? new[] { GroundLandTile, GroundWaterTile } : Array.Empty<SmartTile>())
        .Concat(ConnectedWaterSource != null ? new[] { ConnectedWaterSource } : Array.Empty<SmartTile>());
    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 128 || !SmartBrush.Categories.Contains(Category)
            || Radius is < 0 or > 32 || Density is < 1 or > 100 || Layer == null || Tile == null
            || Variants == null || Transitions == null || Stamp == null || Variants.Count > 256 || Transitions.Count > 256 || Stamp.Count > 100000
            || Variants.Any(t => t == null) || Transitions.Any(t => t == null || t.Match == null || t.Overlay == null || t.MatchLayer == null || t.TargetLayer == null)
            || Stamp.Any(c => c == null || c.Tile == null || c.Layer == null || c.X is < 0 or > 4096 || c.Y is < 0 or > 4096)
            || AnchorX is < 0 or > 4096 || AnchorY is < 0 or > 4096
            || StampWidth is < 0 or > 4096 || StampHeight is < 0 or > 4096
            || WaterFeather is < 0 or > 8 || SoftWater && (SoftWaterTile == null || string.IsNullOrWhiteSpace(SoftWaterLayer) || SoftWaterLayer == Layer
                || Tile.Autotile is not (1 or 3 or 6 or 7) || SoftWaterTile.Autotile != Tile.Autotile || Category == "Decoration" || Stamp.Count > 0 || Transitions.Count > 0)
            || GroundShore && (SoftWater || GroundLandTile == null || GroundWaterTile == null || GroundWaterTile.Autotile != Tile.Autotile
                || Tile.Autotile is not (1 or 3 or 6 or 7) || GroundLandTile.Autotile != 0
                || Layer != Options.Instance.Map.Layers.All[0] || Category != "Terrain" || Stamp.Count > 0 || Transitions.Count > 0 || Variants.Count > 0)
            || ConnectedWaterSource != null && (ConnectedWaterSource.Autotile is not (1 or 3 or 6 or 7) || ConnectedWaterSource.Autotile != Tile.Autotile
                || SoftWater || GroundShore || Category != "Terrain" || Stamp.Count > 0 || Transitions.Count > 0 || Variants.Count > 0 || Layer != Options.Instance.Map.Layers.All[0])
            || Tiles.Any(t => t.Autotile > 7 || t.X < 0 || t.Y < 0 || t.TilesetId == Guid.Empty))
            throw new InvalidDataException("InvalidPreset");
    }
}

internal static class SmartPresetStore
{
    internal static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    internal static List<SmartPreset> Read(string path)
    {
        if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new InvalidDataException("InvalidPreset");
        var presets = JsonSerializer.Deserialize<List<SmartPreset>>(File.ReadAllText(path)) ?? throw new InvalidDataException("InvalidPreset");
        if (presets.Count > 2000 || presets.Any(p => p == null)) throw new InvalidDataException("InvalidPreset");
        foreach (var preset in presets) preset.Validate();
        return presets;
    }
    internal static void Write(string path, IEnumerable<SmartPreset> presets)
    {
        var values = presets.ToArray();
        foreach (var preset in values) preset.Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(values, Json)); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

internal enum SmartShape { Brush, Line, Rectangle, Ellipse, Fill, Replace, Stamp, ConnectWater }
internal readonly record struct SmartCell(int X, int Y);
internal sealed record SmartEdit(string Layer, int X, int Y, Tile Before, Tile After);

internal static class SmartBrush
{
    internal static List<SmartEdit> ConnectWater(MapInstance map, SmartPreset preset, SmartCell clicked, bool erase)
    {
        // Explicit conversion is scoped to the selected native water identity,
        // even when the clicked cell is land or another water family.
        if (preset.ConnectedWaterSource is not { } source || source.Autotile is not (1 or 3 or 6 or 7)
            || source.Autotile != preset.Tile.Autotile || preset.SoftWater || preset.GroundShore
            || preset.Category != "Terrain" || preset.Stamp.Count > 0 || preset.Transitions.Count > 0
            || preset.Layer != Options.Instance.Map.Layers.All[0]
            || !map.Layers.TryGetValue(preset.Layer, out var layer)
            || clicked.X < 0 || clicked.Y < 0 || clicked.X >= layer.GetLength(0) || clicked.Y >= layer.GetLength(1)) return [];
        var match = erase ? preset.Tile : source;
        var replacement = erase ? source : preset.Tile;
        if (SmartTile.From(layer[clicked.X, clicked.Y]) != match) return [];
        return Flood(layer, clicked, true).Select(cell => new SmartEdit(preset.Layer, cell.X, cell.Y,
            layer[cell.X, cell.Y], replacement.Native)).Where(edit => !Same(edit.Before, edit.After)).ToList();
    }

    internal static readonly string[] Categories = ["Terrain", "Road", "River", "Cliff", "Decoration", "Building"];
    internal static IEnumerable<SmartCell> Line(SmartCell start, SmartCell end)
    {
        int x = start.X, y = start.Y, dx = Math.Abs(end.X - x), sx = x < end.X ? 1 : -1,
            dy = -Math.Abs(end.Y - y), sy = y < end.Y ? 1 : -1, error = dx + dy;
        while (true)
        {
            yield return new(x, y);
            if (x == end.X && y == end.Y) break;
            var twice = error * 2;
            if (twice >= dy) { error += dy; x += sx; }
            if (twice <= dx) { error += dx; y += sy; }
        }
    }
    internal static IEnumerable<SmartCell> Area(SmartCell start, SmartCell end, SmartShape shape, int radius, bool round)
    {
        radius = Math.Clamp(radius, 0, 32);
        if (shape is SmartShape.Brush or SmartShape.Line)
        {
            var visited = new HashSet<SmartCell>();
            foreach (var center in Line(start, end))
                for (var y = -radius; y <= radius; y++) for (var x = -radius; x <= radius; x++)
                    if ((!round || x * x + y * y <= radius * radius) && visited.Add(new(center.X + x, center.Y + y)))
                        yield return new(center.X + x, center.Y + y);
            yield break;
        }
        int left = Math.Min(start.X, end.X), top = Math.Min(start.Y, end.Y), right = Math.Max(start.X, end.X), bottom = Math.Max(start.Y, end.Y);
        double rx = (right - left + 1) / 2d, ry = (bottom - top + 1) / 2d, cx = (left + right) / 2d, cy = (top + bottom) / 2d;
        for (var y = top; y <= bottom; y++) for (var x = left; x <= right; x++)
            if (shape != SmartShape.Ellipse || Math.Pow((x - cx) / rx, 2) + Math.Pow((y - cy) / ry, 2) <= 1)
                yield return new(x, y);
    }
    internal static bool Same(Tile left, Tile right) => SmartTile.From(left) == SmartTile.From(right);
    internal static IEnumerable<SmartCell> Flood(Tile[,] tiles, SmartCell start, bool replace)
    {
        int width = tiles.GetLength(0), height = tiles.GetLength(1);
        if (start.X < 0 || start.Y < 0 || start.X >= width || start.Y >= height) yield break;
        var match = tiles[start.X, start.Y];
        if (replace)
        {
            for (var y = 0; y < height; y++) for (var x = 0; x < width; x++) if (Same(tiles[x, y], match)) yield return new(x, y);
            yield break;
        }
        var seen = new HashSet<SmartCell>(); var pending = new Queue<SmartCell>(); pending.Enqueue(start);
        while (pending.TryDequeue(out var cell))
        {
            if (cell.X < 0 || cell.Y < 0 || cell.X >= width || cell.Y >= height || !seen.Add(cell) || !Same(tiles[cell.X, cell.Y], match)) continue;
            yield return cell;
            pending.Enqueue(new(cell.X - 1, cell.Y)); pending.Enqueue(new(cell.X + 1, cell.Y));
            pending.Enqueue(new(cell.X, cell.Y - 1)); pending.Enqueue(new(cell.X, cell.Y + 1));
        }
    }
    // Stable across hover, preview, and commit; no random flicker or density increase on repeated visits.
    private static uint Noise(SmartCell cell, int seed)
    {
        unchecked { var n = (uint)(cell.X * 73856093 ^ cell.Y * 19349663 ^ seed); n ^= n >> 16; n *= 0x7feb352d; n ^= n >> 15; return n; }
    }
    internal static List<SmartEdit> Plan(MapInstance map, SmartPreset preset, IEnumerable<SmartCell> cells, bool preserve, bool erase, int seed, bool stamp = false)
    {
        var edits = new Dictionary<(string Layer, int X, int Y), SmartEdit>();
        foreach (var cell in cells.Distinct())
        {
            if (stamp && preset.StampWidth > 0 && preset.StampHeight > 0
                && (cell.X - preset.AnchorX < 0 || cell.Y - preset.AnchorY < 0
                    || cell.X - preset.AnchorX + preset.StampWidth > Options.Instance.Map.MapWidth
                    || cell.Y - preset.AnchorY + preset.StampHeight > Options.Instance.Map.MapHeight)) continue;
            var noise = Noise(cell, seed);
            if (!erase && preset.Category == "Decoration" && noise % 100 >= preset.Density) continue;
            var tile = preset.Variants.Count > 0 && preset.Category == "Decoration" ? preset.Variants[(int)(noise % (uint)preset.Variants.Count)] : preset.PaintTile;
            var placements = new List<(string Layer, int X, int Y, SmartTile Tile)>();
            if (stamp)
                placements.AddRange(preset.Stamp.Select(c => (c.Layer, cell.X + c.X - preset.AnchorX, cell.Y + c.Y - preset.AnchorY, c.Tile)));
            else
            {
                foreach (var rule in preset.Transitions)
                        if (map.Layers.TryGetValue(rule.MatchLayer, out var source) && cell.X >= 0 && cell.Y >= 0 && cell.X < source.GetLength(0) && cell.Y < source.GetLength(1)
                            && SmartTile.From(source[cell.X, cell.Y]) == rule.Match)
                        placements.Add((rule.TargetLayer, cell.X, cell.Y, rule.Overlay));
                // A matching rule routes the brush onto its overlay layer(s), preserving
                // the underlying terrain so transparent native borders have something to blend over.
                if (placements.Count == 0) placements.Add((preset.PaintLayer, cell.X, cell.Y, tile));
            }
            // Stamps and multi-layer transitions are atomic at each placement.
            if (placements.Count == 0 || placements.Any(p => !map.Layers.TryGetValue(p.Layer, out var layer) || p.X < 0 || p.Y < 0 || p.X >= layer.GetLength(0) || p.Y >= layer.GetLength(1)
                || preserve && layer[p.X, p.Y].TilesetId != Guid.Empty && (!erase || SmartTile.From(layer[p.X, p.Y]) != p.Tile)
                    && !(preset.GroundShore && !erase && GroundShoreVariant.Connects(layer[p.X, p.Y], preset.Tile.Native)))) continue;
            foreach (var placement in placements)
            {
                var before = map.Layers[placement.Layer][placement.X, placement.Y];
                var after = erase ? preset.GroundShore ? preset.Tile.Native : default : placement.Tile.Native;
                if (!Same(before, after)) edits[(placement.Layer, placement.X, placement.Y)] = new(placement.Layer, placement.X, placement.Y, before, after);
            }
        }
        if (preset.GroundShore && !erase && !stamp && preset.GroundWaterTile is { } water)
        {
            // Only water next to the new land needs a different bank. Keep distant
            // shores and every other layer intact; planning and preview are read-only.
            var shore = new Dictionary<(string Layer, int X, int Y), SmartEdit>();
            foreach (var land in edits.Values)
                foreach (var cell in Area(new(land.X, land.Y), new(land.X, land.Y), SmartShape.Brush, 1, false))
                {
                    var key = (land.Layer, cell.X, cell.Y);
                    var layer = map.Layers[land.Layer];
                    if (cell.X < 0 || cell.Y < 0 || cell.X >= layer.GetLength(0) || cell.Y >= layer.GetLength(1) || edits.ContainsKey(key)) continue;
                    var before = layer[cell.X, cell.Y];
                    if (before.Autotile == preset.Tile.Autotile && GroundShoreVariant.Connects(before, preset.Tile.Native) && !Same(before, water.Native))
                        shore[key] = new(land.Layer, cell.X, cell.Y, before, water.Native);
                }
            foreach (var (key, edit) in shore) edits[key] = edit;
        }
        return edits.Values.ToList();
    }
    internal static void Apply(MapInstance map, IReadOnlyCollection<SmartEdit> edits, bool adjacent)
    {
        if (edits.Count == 0) return;
        lock (map.MapLock)
        {
            foreach (var edit in edits) map.Layers[edit.Layer][edit.X, edit.Y] = edit.After;
            if (map.Autotiles.Layers == null) map.InitAutotiles();
            var grid = map.GenerateAutotileGrid();
            foreach (var group in edits.GroupBy(e => e.Layer))
            {
                var affected = new HashSet<SmartCell>();
                foreach (var edit in group)
                    foreach (var cell in Area(new(edit.X, edit.Y), new(edit.X, edit.Y), SmartShape.Brush, 1, false)) affected.Add(cell);
                foreach (var cell in affected) map.Autotiles.UpdateAutoTile(cell.X, cell.Y, group.Key, grid);
                // Cliff heights can depend on the entire connected run, beyond a 3x3 halo.
                // Recalculate the local layer without writing neighboring cache state during preview.
                if (group.Any(e => e.Before.Autotile == 4 || e.After.Autotile == 4))
                    for (var y = 0; y < map.Layers[group.Key].GetLength(1); y++) for (var x = 0; x < map.Layers[group.Key].GetLength(0); x++)
                        if (map.Layers[group.Key][x, y].Autotile == 4) map.Autotiles.UpdateAutoTile(x, y, group.Key, grid);
            }
        }
        // Only derived caches are refreshed. Neighboring map tile/attribute data is never written.
        if (adjacent && edits.Any(e => e.X == 0 || e.Y == 0 || e.X == Options.Instance.Map.MapWidth - 1 || e.Y == Options.Instance.Map.MapHeight - 1
                || e.Before.Autotile == 4 || e.After.Autotile == 4)) map.UpdateAdjacentAutotiles();
    }
}
