using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.General;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;
using Graphics=Intersect.Client.Core.Graphics;
namespace Intersect.Client.Naval;
internal static partial class NavalWorld
{
    internal static bool NearDamagedArea(ShipSnapshot ship)=>
        (ship.DeckMembers??[]).FirstOrDefault(p=>p.PlayerId==Globals.Me?.Id) is {} me &&
        (ship.DamageZones??[]).Any(z=>z.Repairable && z.IsValid(ShipDeck.Kind(ship.Graphics)) && ShipDeck.Distance(new(me.X,me.Y),new(z.X,z.Y))<=ShipDeck.UseDistance);
    internal static bool NearFishingStation(ShipSnapshot ship)=>ship.DeckActivity==ShipDeckActivity.Fishing &&
        (ship.DeckMembers??[]).FirstOrDefault(p=>p.PlayerId==Globals.Me?.Id) is {} me &&
        Enumerable.Range(0,6).Where(i=>i%3<(NavalShipModels.Find(ship.Graphics)?.CannonCount(i>=3)??3))
            .Any(i=>ShipDeck.Distance(new(me.X,me.Y),ShipDeck.Station(i>=3,i%3,ShipDeck.Kind(ship.Graphics)))<=ShipDeck.UseDistance);
    internal static bool NearLowerBattery(ShipSnapshot ship)
    {
        if(!Gunner.Enabled || ShipDeck.Kind(ship.Graphics)!=ShipDeckKind.ShipOfLine ||
            (ship.DeckMembers??[]).FirstOrDefault(p=>p.PlayerId==Globals.Me?.Id) is not {} me)return false;
        var used=(ship.CrewStations??[]).Select(s=>(s.Starboard,s.Index)).ToHashSet();
        var nearest=(from side in new[]{false,true} from index in Enumerable.Range(0,6)
            where !used.Contains((side,index))
            let distance=ShipDeck.Distance(new(me.X,me.Y),ShipDeck.Station(side,index,ShipDeckKind.ShipOfLine))
            where distance<=ShipDeck.UseDistance orderby distance select index).FirstOrDefault(-1);
        return nearest>=3;
    }
    private static void DrawDeckDamage(ShipSnapshot ship,ShipSpriteFrame pose,float x,float y,float scale,bool animations)
    {
        if(ship.Sinking || ship.Health<=0 || pose.DeckMasts.Length!=3)return;
        foreach(var zone in (ship.DamageZones??[]).Take(16))
        {
            if(!zone.IsValid(ShipDeck.Kind(ship.Graphics)))continue;
            var source=ShipDeckProjection.Project(pose,new(zone.X,zone.Y));
            float px=x+(float)(source.X-pose.AnchorX)*scale*(pose.FlipHorizontal?-1:1),py=y+(float)(source.Y-pose.AnchorY)*scale;
            if(animations)
            {
                if(zone.DamageAnimationId==Guid.Empty)continue;
                foreach(var frame in NativeImpactFrames(zone.DamageAnimationId,Environment.TickCount64,true))
                    Graphics.DrawGameTexture(frame.Texture,frame.Source,new FloatRect(px-frame.Width/2+frame.Layer.OffsetX,py-frame.Height/2+frame.Layer.OffsetY,frame.Width,frame.Height),new Color(frame.Layer.Opacity,255,255,255));
            }
            else if(zone.Repairable)
            {
                if(Globals.ContentManager?.GetTexture(TextureType.Ocean,"Ship_Deck_Hole_v5.png") is {} hole)
                    Graphics.DrawGameTexture(hole,new FloatRect(0,0,hole.Width,hole.Height),new FloatRect(px-9*scale,py-6*scale,18*scale,12*scale),Color.White);
                else
                {
                    void Patch(float ox,float oy,float w,float h,Color colour)=>Graphics.DrawGameTexture(Graphics.Renderer.WhitePixel,new FloatRect(0,0,1,1),new FloatRect(px+ox*scale,py+oy*scale,w*scale,h*scale),colour);
                    Patch(-7,-3,14,6,new Color(255,63,36,22));Patch(-5,-4,10,8,new Color(255,63,36,22));
                    Patch(-5,-2,10,4,new Color(255,17,14,12));Patch(-3,-3,6,6,new Color(255,17,14,12));
                }
            }
        }
    }
}
