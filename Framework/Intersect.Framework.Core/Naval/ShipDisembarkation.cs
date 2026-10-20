namespace Intersect.Framework.Core.Naval;

/// <summary>Finds a nearby exit at the configured dock, including when the hull covers the dock tile.</summary>
public static class ShipDisembarkation
{
    public static (int X, int Y)? Find(ShipState state, ShipDefinition definition, int width, int height,
        Func<int, int, bool> clear)
    {
        if (state.MapId == Guid.Empty || state.MapId != definition.MapId ||
            !double.IsFinite(state.X) || !double.IsFinite(state.Y) || !double.IsFinite(state.Speed) ||
            width<=0 || height<=0 || Math.Abs(state.Speed) > .1 ||
            Math.Pow(state.X-definition.DockX-.5,2)+Math.Pow(state.Y-definition.DockY-.5,2)>25) return null;
        var hull = ShipFootprint.Tiles(definition.Width,definition.Length,state.X,state.Y).ToHashSet();
        var candidates = from dy in Enumerable.Range(-3,7)
                         from dx in Enumerable.Range(-3,7)
                         let x = definition.DockX+dx
                         let y = definition.DockY+dy
                         let dockDistance = dx*dx+dy*dy
                         let shipDistance = Math.Pow(x+.5-state.X,2)+Math.Pow(y+.5-state.Y,2)
                         where dockDistance<=9 && shipDistance<=25 && x>=0 && y>=0 && x<width && y<height
                         orderby dockDistance, shipDistance, y, x
                         select (X:x,Y:y);
        foreach (var tile in candidates)
            if (!hull.Contains(tile) && clear(tile.X,tile.Y)) return tile;
        return null;
    }
}
