using Intersect.Client.Framework.GenericClasses;
using Intersect.Framework.Core.Naval;

namespace Intersect.Client.Naval;

internal static class NavalSpriteGeometry
{
    // Source-only clipping bands isolate touching sprites without rewriting the artwork.
    internal static IEnumerable<ShipSpriteFrame> Parts(ShipSpriteFrame frame)
    {
        if(frame.Slices==null) {yield return frame;yield break;}
        foreach(var r in frame.Slices)
            yield return new() {X=frame.X+r[0],Y=frame.Y+r[1],Width=r[2],Height=r[3],
                AnchorX=frame.AnchorX-r[0],AnchorY=frame.AnchorY-r[1],FlipHorizontal=frame.FlipHorizontal};
    }
    // The renderer normalizes a signed width and mirrors UVs, preserving the waterline anchor.
    internal static FloatRect Destination(ShipSpriteFrame pose, float x, float y, float scale) =>
        new(x + (float)pose.AnchorX * scale * (pose.FlipHorizontal ? 1 : -1),
            y - (float)pose.AnchorY * scale, pose.Width * scale * (pose.FlipHorizontal ? -1 : 1), pose.Height * scale);
}
