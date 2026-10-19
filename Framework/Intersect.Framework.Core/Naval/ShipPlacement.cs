namespace Intersect.Framework.Core.Naval;

/// <summary>Finds a conservative, fully local placement from authoritative collision data.</summary>
public static class ShipPlacement
{
    public static ShipDefinition? Find(Guid mapId, int width, int height, Func<int, int, bool> blocked)
    {
        if (mapId == Guid.Empty || width < 7 || height < 7 || width > 256 || height > 256) return null;
        var candidates = from y in Enumerable.Range(3, height - 6)
                         from x in Enumerable.Range(3, width - 6)
                         orderby Math.Pow(x + .5 - width / 2d, 2) + Math.Pow(y + .5 - height / 2d, 2), y, x
                         select (X: x, Y: y);
        foreach (var center in candidates)
        {
            var definition = new ShipDefinition { MapId = mapId, X = center.X + .5, Y = center.Y + .5 };
            var hull = ShipFootprint.Tiles(definition.Width, definition.Length, definition.X, definition.Y).ToHashSet();
            if (hull.Any(t => t.X < 0 || t.Y < 0 || t.X >= width || t.Y >= height || blocked(t.X, t.Y))) continue;
            var docks = from dy in Enumerable.Range(-5, 11)
                        from dx in Enumerable.Range(-5, 11)
                        let distance = dx * dx + dy * dy
                        let x = center.X + dx
                        let y = center.Y + dy
                        where distance > 0 && distance <= 25 && x >= 0 && y >= 0 && x < width && y < height
                        orderby distance, y, x
                        select (X: x, Y: y);
            foreach (var dock in docks)
            {
                if (hull.Contains(dock) || blocked(dock.X, dock.Y)) continue;
                definition.DockX = dock.X; definition.DockY = dock.Y;
                return definition;
            }
        }
        return null;
    }
}
