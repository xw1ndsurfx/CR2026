using Intersect.Client.Framework.GenericClasses;
using Intersect.Framework.Core.Naval;

namespace Intersect.Client.Naval;

internal static class NavalSpriteGeometry
{
    // Source-only clipping bands isolate touching sprites without rewriting the artwork.
    internal static IEnumerable<ShipSpriteFrame> Parts(ShipSpriteFrame frame) => Intersect.Rendering.ShipAtlasResources.Parts(frame);
    // The renderer normalizes signed widths and mirrors UVs around the waterline.
    internal static FloatRect Destination(ShipSpriteFrame pose,float x,float y,float scale)
    {
        var bounds=Intersect.Rendering.ShipAtlasResources.Bounds(pose,x,y,scale);
        return new(bounds.X,bounds.Y,bounds.Width,bounds.Height);
    }
}
