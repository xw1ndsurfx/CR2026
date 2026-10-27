using System.Drawing.Imaging;
using Intersect.Framework.Core.GameObjects.Maps;

namespace Intersect.Editor.Maps;

// Authoring operation only: creates a conventional PNG, rendered by the unchanged client.
internal static class SmartWaterShore
{
    internal static Bitmap Create(Bitmap source, SmartTile tile, int feather)
    {
        int tw = Options.Instance.Map.TileWidth, th = Options.Instance.Map.TileHeight;
        if (tile.Autotile is not (1 or 3 or 6 or 7) || !tile.Fits(source.Width, source.Height, tw, th) || feather is < 0 or > 8)
            throw new InvalidDataException("Smart.InvalidResource");
        var sample = new MapInstance(Guid.NewGuid());
        foreach (var layer in Options.Instance.Map.Layers.All) sample.Layers[layer] = new Tile[Options.Instance.Map.MapWidth, Options.Instance.Map.MapHeight];
        if (Options.Instance.Map.MapWidth < 3 || Options.Instance.Map.MapHeight < 3) throw new InvalidDataException("Smart.InvalidResource");
        var name = Options.Instance.Map.Layers.All[0];
        for (var y = 0; y < 3; y++) for (var x = 0; x < 3; x++) sample.Layers[name][x, y] = tile.Native;
        sample.InitAutotiles();
        int frames = tile.Autotile is 3 or 7 ? 3 : 1;
        int width = (tile.Autotile is 6 or 7 ? 3 : 2) * tw;
        int height = (tile.Autotile is 6 or 7 ? 4 : 3) * th;
        var result = source.Clone(new Rectangle(0, 0, source.Width, source.Height), PixelFormat.Format32bppArgb);
        try
        {
            for (var frame = 0; frame < frames; frame++)
            {
                // Learn water colors from the native connected interior, separately per animation frame.
                // Never guess sand by a fixed yellow/brown color or modify the original atlas.
                var colors = new HashSet<int>();
                var interior = new HashSet<(int X, int Y)>();
                for (var q = 1; q <= 4; q++)
                {
                    var point = sample.Autotiles.Layers[name][1, 1].QuarterTile[q];
                    for (var y = 0; y < th / 2; y++) for (var x = 0; x < tw / 2; x++)
                    {
                        var color = source.GetPixel(point.X + frame * width + x, point.Y + y);
                        interior.Add((point.X + frame * width + x, point.Y + y));
                        if (color.A > 0) colors.Add(color.ToArgb() & 0xffffff);
                    }
                }
                if (colors.Count == 0) throw new InvalidDataException("Smart.InvalidResource");
                var distance = new float[width, height];
                int ox = tile.X * tw + frame * width, oy = tile.Y * th;
                for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
                {
                    var color = source.GetPixel(ox + x, oy + y);
                    distance[x, y] = color.A > 0 && colors.Contains(color.ToArgb() & 0xffffff) ? 10000 : 0;
                }
                // Distance to non-water pixels; atlas boundaries are not artificial shoreline edges.
                for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
                {
                    if (x > 0) distance[x, y] = Math.Min(distance[x, y], distance[x - 1, y] + 1);
                    if (y > 0) distance[x, y] = Math.Min(distance[x, y], distance[x, y - 1] + 1);
                    if (x > 0 && y > 0) distance[x, y] = Math.Min(distance[x, y], distance[x - 1, y - 1] + 1.414214f);
                    if (x + 1 < width && y > 0) distance[x, y] = Math.Min(distance[x, y], distance[x + 1, y - 1] + 1.414214f);
                }
                for (var y = height - 1; y >= 0; y--) for (var x = width - 1; x >= 0; x--)
                {
                    if (x + 1 < width) distance[x, y] = Math.Min(distance[x, y], distance[x + 1, y] + 1);
                    if (y + 1 < height) distance[x, y] = Math.Min(distance[x, y], distance[x, y + 1] + 1);
                    if (x + 1 < width && y + 1 < height) distance[x, y] = Math.Min(distance[x, y], distance[x + 1, y + 1] + 1.414214f);
                    if (x > 0 && y + 1 < height) distance[x, y] = Math.Min(distance[x, y], distance[x - 1, y + 1] + 1.414214f);
                    var color = source.GetPixel(ox + x, oy + y);
                    // Interior template quarters are reused at every connected map cell.
                    // Feathering those source edges would create a grid of seams in open water.
                    var alpha = interior.Contains((ox + x, oy + y)) ? color.A
                        : (int)Math.Round(color.A * Math.Clamp(distance[x, y] / (feather + 1), 0, 1));
                    result.SetPixel(ox + x, oy + y, System.Drawing.Color.FromArgb(alpha, color.R, color.G, color.B));
                }
            }
            return result;
        }
        catch { result.Dispose(); throw; }
    }
}
