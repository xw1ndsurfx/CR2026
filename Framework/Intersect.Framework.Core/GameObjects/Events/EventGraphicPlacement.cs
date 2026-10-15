using System.Numerics;

namespace Intersect.Framework.Core.GameObjects.Events;

public static class EventGraphicPlacement
{
    // The event tile is always the bottom-center anchor, including graphics smaller than a tile.
    public static Vector2 TopLeft(float tileX, float tileY, float width, float height, int tileWidth, int tileHeight) =>
        new(tileX + (tileWidth - width) / 2f, tileY + tileHeight - height);
}
