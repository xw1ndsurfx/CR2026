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
        if(!string.IsNullOrEmpty(atlas.CannonOcclusionGraphics) &&
            Globals.ContentManager?.GetTexture(TextureType.Ocean,atlas.CannonOcclusionGraphics) is {} cover &&
            cover.Width==atlas.ImageWidth && cover.Height==atlas.ImageHeight)
        {
            // Rails hide feet, but must not repaint themselves over moving barrels.
            // Operators on the far broadside stand in front of their own cannon.
            var people=DeckCrew(ship,pose).OrderBy(c=>c.Anchor.Y).ToArray();
            foreach(var person in people.Where(c=>!BehindDeckCannon(pose,c.Anchor))) DrawPerson(person);
            DrawLayer(foreground);
            DrawDeckCannons(ship,atlas,pose,x,y,scale,true);
            DrawDeckCannons(ship,atlas,pose,x,y,scale,false);
            foreach(var person in people.Where(c=>BehindDeckCannon(pose,c.Anchor))) DrawPerson(person);
            DrawLayer(cover);
            return;
        }
        DrawDeckCannons(ship,atlas,pose,x,y,scale,true);
        foreach(var (id,anchor) in DeckCrew(ship,pose).OrderBy(c=>c.Anchor.Y))
            if(Globals.Entities.TryGetValue(id,out var entity) && entity.MapId==ship.MapId && !entity.IsDisposed)
            {
                var point=DeckPoint(pose,anchor,x,y,scale);
                entity.DrawOnShip(point.X,point.Y,point.Facing);
            }
        DrawDeckCannons(ship,atlas,pose,x,y,scale,false);
        foreach(var part in NavalSpriteGeometry.Parts(pose))
            Graphics.DrawGameTexture(foreground,new FloatRect(part.X,part.Y,part.Width,part.Height),
                NavalSpriteGeometry.Destination(part,x,y,scale),Color.White);

        void DrawPerson((Guid PlayerId,ShipCrewAnchor Anchor) person)
        {
            if(Globals.Entities.TryGetValue(person.PlayerId,out var entity) && entity.MapId==ship.MapId && !entity.IsDisposed)
            {
                var point=DeckPoint(pose,person.Anchor,x,y,scale);
                entity.DrawOnShip(point.X,point.Y,point.Facing);
            }
        }
        void DrawLayer(IGameTexture texture)
        {
            foreach(var part in NavalSpriteGeometry.Parts(pose))
                Graphics.DrawGameTexture(texture,new FloatRect(part.X,part.Y,part.Width,part.Height),
                    NavalSpriteGeometry.Destination(part,x,y,scale),Color.White);
        }
    }

    internal static bool BehindDeckCannon(ShipSpriteFrame pose,ShipCrewAnchor crew)=>
        (crew.Role is ShipCrewRole.PortCannon or ShipCrewRole.StarboardCannon) &&
        pose.CannonAnchors.Any(a=>a.Index==crew.Index && a.Starboard==(crew.Role==ShipCrewRole.StarboardCannon) && a.BehindCrew);
}
