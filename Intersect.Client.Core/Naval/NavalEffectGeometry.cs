using Intersect.Client.Framework.GenericClasses;
using Intersect.Framework.Core.Naval;
namespace Intersect.Client.Naval;

internal static class NavalEffectGeometry
{
    // The native renderer rotates about the rectangle centre, so compensate the authored anchor before rotating.
    internal static FloatRect Destination(ShipSpriteFrame frame,float x,float y,double scale,double degrees)
    {
        var radians=degrees*Math.PI/180;var ox=frame.Width/2d-frame.AnchorX;var oy=frame.Height/2d-frame.AnchorY;
        var cx=x+(Math.Cos(radians)*ox-Math.Sin(radians)*oy)*scale;
        var cy=y+(Math.Sin(radians)*ox+Math.Cos(radians)*oy)*scale;
        return new((float)(cx-frame.Width*scale/2),(float)(cy-frame.Height*scale/2),(float)(frame.Width*scale),(float)(frame.Height*scale));
    }
}
