using Intersect.Client.Framework.GenericClasses;
using Intersect.Framework.Core.Naval;

namespace Intersect.Client.Naval;

internal static class NavalSpriteGeometry
{
    // The renderer normalizes a signed width and mirrors UVs, preserving the waterline anchor.
    internal static FloatRect Destination(ShipSpriteFrame pose, float x, float y, float scale) =>
        new(x + (float)pose.AnchorX * scale * (pose.FlipHorizontal ? 1 : -1),
            y - (float)pose.AnchorY * scale, pose.Width * scale * (pose.FlipHorizontal ? -1 : 1), pose.Height * scale);
}
