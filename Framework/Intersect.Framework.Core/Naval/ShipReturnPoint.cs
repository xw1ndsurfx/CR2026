namespace Intersect.Framework.Core.Naval;

public static class ShipReturnPoint
{
    // Legacy fleets have no exact boarding coordinates; keep their existing quay fallback.
    public static (int X,int Y)? Exact(OwnedShip ship,int width,int height,Func<int,int,bool> clear) =>
        ship.ReturnX is { } x && ship.ReturnY is { } y && x>=0 && y>=0 && x<width && y<height && clear(x,y)
            ? (x,y) : null;
}
