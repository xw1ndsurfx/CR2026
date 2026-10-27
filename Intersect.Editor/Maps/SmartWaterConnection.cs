namespace Intersect.Editor.Maps;

internal static class SmartWaterConnection
{
    // Relocate the chosen native block into the reference block's coordinates.
    // Its connection filename can then use the existing shared shore resolver;
    // no map format change, global terrain rule or lost animation frame.
    internal static Bitmap Create(Bitmap reference, SmartTile water, Bitmap appearance, SmartTile selected)
    {
        var tw = Options.Instance.Map.TileWidth; var th = Options.Instance.Map.TileHeight;
        if (water.Autotile is not (1 or 3 or 6 or 7) || selected.Autotile != water.Autotile
            || !water.Fits(reference.Width, reference.Height, tw, th)
            || !selected.Fits(appearance.Width, appearance.Height, tw, th))
            throw new InvalidDataException("Smart.WaterConnectionRequired");
        var result = new Bitmap(reference);
        var size = selected.Footprint;
        for (var y = 0; y < size.Height * th; y++) for (var x = 0; x < size.Width * tw; x++)
            result.SetPixel(water.X * tw + x, water.Y * th + y, appearance.GetPixel(selected.X * tw + x, selected.Y * th + y));
        return result;
    }
}
