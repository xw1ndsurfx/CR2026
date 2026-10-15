namespace Intersect.Editor.Maps;

internal static class MapEditorViewport
{
    internal static (float Zoom, Rectangle View) ZoomAt(float zoom, Rectangle view, System.Drawing.Point cursor, Size size, int wheelDelta)
    {
        var next = Math.Clamp(zoom * MathF.Pow(1.25f, wheelDelta / 120f), 0.25f, 4f);
        var worldX = cursor.X / zoom - view.X;
        var worldY = cursor.Y / zoom - view.Y;
        return (next, new Rectangle((int)Math.Round(cursor.X / next - worldX), (int)Math.Round(cursor.Y / next - worldY),
            (int)Math.Ceiling(size.Width / next), (int)Math.Ceiling(size.Height / next)));
    }
}
