using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.General;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;
using Graphics = Intersect.Client.Core.Graphics;

namespace Intersect.Client.Naval;

internal static partial class NavalWorld
{
    internal static IEnumerable<(Guid PlayerId, ShipCrewAnchor Anchor)> DeckCrew(ShipSnapshot ship, ShipSpriteFrame pose)
    {
        var drawn = new HashSet<Guid>();
        var members = (ship.CrewIds ?? []).Prepend(ship.CaptainId).Where(id=>id!=Guid.Empty).ToHashSet();
        var driver = Driver(ship);
        var helm = pose.CrewAnchors.FirstOrDefault(a=>a.Role==ShipCrewRole.Helm);
        if (helm!=null && members.Contains(driver) && drawn.Add(driver)) yield return (driver,helm);
        foreach (var seat in ship.CrewStations ?? [])
        {
            if (!members.Contains(seat.PlayerId) || seat.PlayerId==driver || drawn.Contains(seat.PlayerId)) continue;
            var role = seat.Starboard ? ShipCrewRole.StarboardCannon : ShipCrewRole.PortCannon;
            var anchor = pose.CrewAnchors.FirstOrDefault(a=>a.Role==role && a.Index==seat.Index);
            if(anchor!=null && drawn.Add(seat.PlayerId)) yield return (seat.PlayerId,anchor);
        }
        using var passengers = pose.CrewAnchors.Where(a=>a.Role==ShipCrewRole.Passenger).OrderBy(a=>a.Index).GetEnumerator();
        foreach(var id in members.OrderBy(id=>id))
            if(!drawn.Contains(id) && passengers.MoveNext() && drawn.Add(id)) yield return (id,passengers.Current);
    }

    internal static (float X,float Y,Intersect.Enums.Direction Facing) DeckPoint(ShipSpriteFrame pose,ShipCrewAnchor anchor,float x,float y,float scale)
    {
        var facing=anchor.Facing;
        if(pose.FlipHorizontal) facing=facing switch {Intersect.Enums.Direction.Left=>Intersect.Enums.Direction.Right,
            Intersect.Enums.Direction.Right=>Intersect.Enums.Direction.Left,_=>facing};
        return (x+(float)(anchor.X-pose.AnchorX)*scale*(pose.FlipHorizontal?-1:1),
            y+(float)(anchor.Y-pose.AnchorY)*scale,facing);
    }

    private static void DrawDeckCrew(ShipSnapshot ship, ShipSpriteAtlas atlas, ShipSpriteFrame pose,
        IGameTexture foreground, float x,float y,float scale)
    {
        if(ship.Sinking || ship.Health<=0) return;
        foreach(var (id,anchor) in DeckCrew(ship,pose).OrderBy(c=>c.Anchor.Y))
            if(Globals.Entities.TryGetValue(id,out var entity) && entity.MapId==ship.MapId && !entity.IsDisposed)
            {
                var point=DeckPoint(pose,anchor,x,y,scale);
                entity.DrawOnShip(point.X,point.Y,point.Facing);
            }
        foreach(var part in NavalSpriteGeometry.Parts(pose))
            Graphics.DrawGameTexture(foreground,new FloatRect(part.X,part.Y,part.Width,part.Height),
                NavalSpriteGeometry.Destination(part,x,y,scale),Color.White);
    }
}
