namespace Intersect.Client.MiniGames;

public readonly record struct PotionAtlasCell(int X, int Y, int Width, int Height);

/// <summary>Ignore transparent sprite gutters when fitting an ingredient into its board cell.</summary>
public static class PotionAtlasLayout
{
    public static PotionAtlasCell[] FindCells(int width, int height, byte[]? alpha)
    {
        var result = new PotionAtlasCell[16];
        var hasAlpha = alpha != null && (long)width * height == alpha.Length;
        for (var row = 0; row < 4; row++)
        for (var col = 0; col < 4; col++)
        {
            var x0 = col * width / 4; var x1 = (col + 1) * width / 4;
            var y0 = row * height / 4; var y1 = (row + 1) * height / 4;
            var left = x1; var top = y1; var right = x0 - 1; var bottom = y0 - 1;
            if (hasAlpha)
            {
                var islands = FindIslands(alpha!, width, x0, y0, x1, y1);
                var largest = islands.Count == 0 ? 0 : islands.Max(island => island.Area);
                foreach (var island in islands)
                {
                    // A small fragment touching the cell edge belongs to an adjacent
                    // sprite in imperfect atlases; it must not shrink the main ingredient.
                    if (island.Edge && island.Area < largest / 2) continue;
                    left = Math.Min(left, island.Left); top = Math.Min(top, island.Top);
                    right = Math.Max(right, island.Right); bottom = Math.Max(bottom, island.Bottom);
                }
            }
            result[row * 4 + col] = right < left
                ? new(x0, y0, Math.Max(1, x1 - x0), Math.Max(1, y1 - y0))
                : new(left, top, right - left + 1, bottom - top + 1);
        }
        return result;
    }

    private readonly record struct Island(int Left, int Top, int Right, int Bottom, int Area, bool Edge);

    private static List<Island> FindIslands(byte[] alpha, int imageWidth, int x0, int y0, int x1, int y1)
    {
        var width = x1 - x0; var height = y1 - y0;
        var visited = new bool[width * height];
        var queue = new Queue<int>();
        var islands = new List<Island>();
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var start = y * width + x;
            if (visited[start] || alpha[(y0 + y) * imageWidth + x0 + x] < 64) continue;
            visited[start] = true; queue.Enqueue(start);
            var left = x; var right = x; var top = y; var bottom = y; var area = 0; var edge = false;
            while (queue.TryDequeue(out var pixel))
            {
                var px = pixel % width; var py = pixel / width; area++;
                left = Math.Min(left, px); right = Math.Max(right, px); top = Math.Min(top, py); bottom = Math.Max(bottom, py);
                edge |= px == 0 || py == 0 || px == width - 1 || py == height - 1;
                for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    var nx = px + dx; var ny = py + dy;
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                    var index = ny * width + nx;
                    if (visited[index] || alpha[(y0 + ny) * imageWidth + x0 + nx] < 64) continue;
                    visited[index] = true; queue.Enqueue(index);
                }
            }
            islands.Add(new(x0 + left, y0 + top, x0 + right, y0 + bottom, area, edge));
        }
        return islands;
    }

    public static PokerSceneRect Fit(PotionAtlasCell source, PokerSceneRect cell, int tier)
    {
        var fill = .80 + Math.Clamp(tier, 1, 4) * .035;
        var scale = Math.Min(cell.Width * fill / source.Width, cell.Height * fill / source.Height);
        var width = Math.Max(1, (int)(source.Width * scale));
        var height = Math.Max(1, (int)(source.Height * scale));
        return new(cell.X + (cell.Width - width) / 2, cell.Y + (cell.Height - height) / 2, width, height);
    }
}
