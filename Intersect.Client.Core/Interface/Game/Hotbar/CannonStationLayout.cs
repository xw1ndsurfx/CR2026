using Intersect.Client.Framework.GenericClasses;
using Intersect.Framework.Core.Naval;

namespace Intersect.Client.Interface.Game.Hotbar;

internal readonly record struct CannonStationPlacement(Rectangle Destination, double SourceY, double SourceHeight);

internal static class CannonStationLayout
{
    // Width always reaches both edges. On ultrawide/short viewports, crop only
    // the upper part of the foreground instead of stretching the cannon itself.
    internal static CannonStationPlacement Panoramic(int width,int height,ShipSpriteFrame frame)
    {
        width=Math.Max(1,width);height=Math.Max(1,height);
        var naturalHeight=width*frame.Height/(double)Math.Max(1,frame.Width);
        var displayed=Math.Max(1,Math.Min((int)Math.Ceiling(naturalHeight),(int)(height*.62)));
        var sourceHeight=Math.Min(frame.Height,displayed*frame.Width/(double)width);
        return new(new Rectangle(0,height-displayed,width,displayed),frame.Y+frame.Height-sourceHeight,sourceHeight);
    }
}
