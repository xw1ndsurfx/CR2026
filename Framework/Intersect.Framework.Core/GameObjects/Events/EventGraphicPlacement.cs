using System.Numerics;

namespace Intersect.Framework.Core.GameObjects.Events;

public static class EventGraphicPlacement
{
    public static Vector2 VisibleTopLeft(float tileX, float tileY, GraphicBounds visible, int tileWidth, int tileHeight) =>
        new(tileX + tileWidth / 2f - visible.X - visible.Width / 2f,
            tileY + tileHeight - visible.Y - visible.Height);

    // The event tile is always the bottom-center anchor, including graphics smaller than a tile.
    public static Vector2 TopLeft(float tileX, float tileY, float width, float height, int tileWidth, int tileHeight) =>
        new(tileX + (tileWidth - width) / 2f, tileY + tileHeight - height);
}
