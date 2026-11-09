using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.General;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;
using Graphics = Intersect.Client.Core.Graphics;

namespace Intersect.Client.Naval;

internal static partial class NavalWorld
{
    internal static double DeckCannonYaw(ShipSnapshot previous,ShipSnapshot current,ShipCannonAnchor anchor,double amount)
    {
        var seat=(current.CrewStations??[]).FirstOrDefault(s=>s.Starboard==anchor.Starboard && s.Index==anchor.Index);
        if(seat==null || seat.PlayerId==Guid.Empty || !HasMember(current,seat.PlayerId) || seat.PlayerId==Driver(current)) return 0;
        var target=double.IsFinite(seat.Yaw)?Math.Clamp(seat.Yaw,-90,90):0;
        var old=(previous.CrewStations??[]).FirstOrDefault(s=>s.PlayerId==seat.PlayerId && s.Starboard==seat.Starboard && s.Index==seat.Index);
        var start=old!=null && double.IsFinite(old.Yaw)?Math.Clamp(old.Yaw,-90,90):target;
        return start+(target-start)*Math.Clamp(amount,0,1);
    }

    private static void DrawDeckCannons(ShipSnapshot ship,ShipSpriteAtlas atlas,ShipSpriteFrame pose,float x,float y,float scale,bool behindCrew)
    {
        if(string.IsNullOrEmpty(atlas.CannonGraphics) ||
            Globals.ContentManager?.GetTexture(TextureType.Ocean,atlas.CannonGraphics) is not {} texture) return;
        const int cell=ShipCannonAnchor.CellSize,columns=ShipCannonAnchor.Columns;
        _ships.TryGetValue(ship.Id,out var view);
        foreach(var anchor in pose.CannonAnchors.Where(a=>a.BehindCrew==behindCrew).OrderBy(a=>a.Y))
        {
            var sx=anchor.Sprite%columns*cell;var sy=anchor.Sprite/columns*cell;
            if(sx+cell>texture.Width || sy+cell>texture.Height) continue;
            var yaw=DeckCannonYaw(view?.Previous??ship,ship,anchor,(Environment.TickCount64-(view?.Received??0))/100d);
            if(Gunner.ShipId==ship.Id && Gunner.LeaseId!=Guid.Empty && Gunner.Station==anchor.Index && Gunner.Starboard==anchor.Starboard &&
                (ship.CrewStations??[]).Any(s=>s.PlayerId==Globals.Me?.Id && s.Index==anchor.Index && s.Starboard==anchor.Starboard) && double.IsFinite(GunYaw))
                yaw=Math.Clamp(GunYaw,-Gunner.AimArcDegrees,Gunner.AimArcDegrees);
            var px=x+(float)(anchor.X-pose.AnchorX)*scale*(pose.FlipHorizontal?-1:1);
            var py=y+(float)(anchor.Y-pose.AnchorY)*scale;
            var size=cell*scale;
            Graphics.DrawGameTexture(texture,new FloatRect(sx,sy,cell,cell),
                new FloatRect(px+(pose.FlipHorizontal?size/2:-size/2),py-size/2,pose.FlipHorizontal?-size:size,size),
                Color.White,rotationDegrees:(float)(yaw*(pose.FlipHorizontal?-1:1)));
        }
    }

    internal static (float X,float Y) CannonPerspectiveAnchor(double offsetX,double offsetY,double size,double yaw)
    {
        var angle=yaw*Math.PI/180;var c=Math.Cos(angle);var s=Math.Sin(angle);
        // Inverse-rotate the offset: the cannon pivots at its own breech,
        // while retaining the ship centre for stable perspective painter sorting.
        return ((float)(size/2-offsetX*c+offsetY*s),(float)(size/2+offsetX*s+offsetY*c));
    }

    private static void AddDeckCannonsToScene(List<MapPerspectiveSprite> sprites,View view,ShipSpriteAtlas atlas,ShipSpriteFrame pose,float x,float y)
    {
        if(string.IsNullOrEmpty(atlas.CannonGraphics) ||
            Globals.ContentManager?.GetTexture(TextureType.Ocean,atlas.CannonGraphics) is not {} texture) return;
        const int cell=ShipCannonAnchor.CellSize,columns=ShipCannonAnchor.Columns;
        var size=(float)(cell/atlas.PixelsPerTile);
        var cover=string.IsNullOrEmpty(atlas.CannonOcclusionGraphics)?null:
            Globals.ContentManager?.GetTexture(TextureType.Ocean,atlas.CannonOcclusionGraphics);
        if(cover!=null && (cover.Width!=atlas.ImageWidth || cover.Height!=atlas.ImageHeight)) cover=null;
        var foreground=string.IsNullOrEmpty(atlas.ForegroundGraphics)?null:
            Globals.ContentManager?.GetTexture(TextureType.Ocean,atlas.ForegroundGraphics);
        if(foreground!=null && (foreground.Width!=atlas.ImageWidth || foreground.Height!=atlas.ImageHeight))foreground=null;
        if(cover!=null && foreground!=null) AddLayer(foreground);
        foreach(var a in pose.CannonAnchors.OrderBy(a=>a.Y))
        {
            var sx=a.Sprite%columns*cell;var sy=a.Sprite/columns*cell;
            if(sx+cell>texture.Width || sy+cell>texture.Height) continue;
            var yaw=DeckCannonYaw(view.Previous,view.Current,a,(Environment.TickCount64-view.Received)/100d)*(pose.FlipHorizontal?-1:1);
            var offsetX=(a.X-pose.AnchorX)/atlas.PixelsPerTile*(pose.FlipHorizontal?-1:1);
            var offsetY=(pose.AnchorY-a.Y)/atlas.PixelsPerTile;
            var anchor=CannonPerspectiveAnchor(offsetX,offsetY,size,yaw);
            sprites.Add(new(texture,new FloatRect(sx,sy,cell,cell),new(x,y),size,size,anchor.X,anchor.Y,pose.FlipHorizontal,RotationDegrees:(float)yaw));
        }
        if((cover??foreground) is {} top) AddLayer(top);

        void AddLayer(IGameTexture layer)
        {
            foreach(var part in NavalSpriteGeometry.Parts(pose))
                sprites.Add(new(layer,new FloatRect(part.X,part.Y,part.Width,part.Height),new(x,y),
                    (float)(part.Width/atlas.PixelsPerTile),(float)(part.Height/atlas.PixelsPerTile),
                    (float)(part.AnchorX/atlas.PixelsPerTile),(float)(part.AnchorY/atlas.PixelsPerTile),part.FlipHorizontal));
        }
    }
}
