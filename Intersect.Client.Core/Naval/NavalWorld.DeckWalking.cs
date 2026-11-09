using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.General;
using Intersect.Enums;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Naval;
internal static partial class NavalWorld
{
    internal static int PartyCapacity=>Math.Max(Math.Max(Intersect.Options.Instance.Party.MaximumMembers,Globals.Me?.PartyMemberCapacity??0),
        Math.Max(Globals.Me?.Party?.Count??0,Fresh && _ships.Values.Any(v=>v.Current.WalkableDeck &&
            Globals.Me?.Party?.Any(p=>p.Id==v.Current.CaptainId)==true)?7:0));
    private static bool _deckInteractDown;
    private static long _deckInputTime;
    private static Guid _deckLease;
    private static void ClearDeck(){_deckInteractDown=false;_deckInputTime=0;_deckLease=Guid.Empty;}
    internal static Direction DeckFacing(ShipSpriteFrame pose,DeckPoint point,double side,double forward)
    {
        var a=ShipDeckProjection.Project(pose,point);var b=ShipDeckProjection.Project(pose,new(point.X+side*.1,point.Y+forward*.1));
        var dx=b.X-a.X;var dy=b.Y-a.Y;
        return Math.Abs(dx)>Math.Abs(dy)?dx<0?Direction.Left:Direction.Right:dy<0?Direction.Up:Direction.Down;
    }
    private static NavalDeckMember InterpolatedDeckMember(ShipSnapshot ship,NavalDeckMember member)
    {
        if(!_ships.TryGetValue(ship.Id,out var view))return member;
        var old=(view.Previous.DeckMembers??[]).FirstOrDefault(p=>p.PlayerId==member.PlayerId);
        if(old==null || !ShipDeck.Contains(new(old.X,old.Y)) || ShipDeck.Distance(new(old.X,old.Y),new(member.X,member.Y))>.8)return member;
        var t=Math.Clamp((Environment.TickCount64-view.Received)/100d,0,1);
        return new(){PlayerId=member.PlayerId,X=old.X+(member.X-old.X)*t,Y=old.Y+(member.Y-old.Y)*t,
            FacingX=member.FacingX,FacingY=member.FacingY,Walking=member.Walking};
    }
    internal static (double Side,double Forward) DeckInputDirection(ShipSpriteFrame pose,DeckPoint point,double screenX,double screenY)
    {
        var a=ShipDeckProjection.Project(pose,point);var x=ShipDeckProjection.Project(pose,new(point.X+.01,point.Y));
        var y=ShipDeckProjection.Project(pose,new(point.X,point.Y+.01));
        var xx=(x.X-a.X)*(pose.FlipHorizontal?-1:1);var xy=x.Y-a.Y;
        var yx=(y.X-a.X)*(pose.FlipHorizontal?-1:1);var yy=y.Y-a.Y;
        var det=xx*yy-xy*yx;if(!double.IsFinite(det) || Math.Abs(det)<.000001)return (0,0);
        var side=(screenX*yy-screenY*yx)/det;var forward=(xx*screenY-xy*screenX)/det;
        var length=Math.Sqrt(side*side+forward*forward);return length<.00001?(0,0):(side/length,forward/length);
    }
    private static bool InputDeck(ShipSnapshot ship,bool busy)
    {
        bool Down(Keys key)=>!busy && Globals.InputManager.IsKeyDown(key);
        var interact=Down(Keys.E);
        if(interact && !_deckInteractDown)InteractDeck();
        _deckInteractDown=interact;
        if(Driver(ship)==Globals.Me?.Id)return false;
        var now=Environment.TickCount64;if(now-_deckInputTime<50)return true;_deckInputTime=now;
        double side=0,forward=0;
        if(!CannonViewOpen && Gunner.LeaseId==Guid.Empty && !busy &&
            (ship.DeckMembers??[]).FirstOrDefault(p=>p.PlayerId==Globals.Me?.Id) is {} me &&
            Globals.ContentManager?.GetTexture(TextureType.Ocean,ship.Graphics) is {} image &&
            NavalSpriteCatalog.Get(ship.Graphics,image.Width,image.Height,ship.Frames) is {} atlas)
        {
            var heading=ship.Heading;if(_ships.TryGetValue(ship.Id,out var view))Position(view,out _,out _,out heading);
            var pose=atlas.Frame(heading,0,ship.FrameMilliseconds);
            (side,forward)=DeckInputDirection(pose,new(me.X,me.Y),
                (Down(Keys.D)||Down(Keys.Right)?1:0)-(Down(Keys.A)||Down(Keys.Left)?1:0),
                (Down(Keys.S)||Down(Keys.Down)?1:0)-(Down(Keys.W)||Down(Keys.Up)?1:0));
        }
        Intersect.Client.Networking.Network.SendPacket(new DeckInputPacket {ShipId=ship.Id,Side=(sbyte)Math.Round(side*127),Forward=(sbyte)Math.Round(forward*127)});
        return true;
    }
    internal static void InteractDeck()
    {
        if(OwnShip is not {WalkableDeck:true,Sinking:false} ship || Globals.Me?.IsBusy!=false)return;
        if(Gunner.LeaseId!=Guid.Empty)CannonViewOpen=false;
        Intersect.Client.Networking.Network.SendPacket(new DeckInputPacket {ShipId=ship.Id,Action=DeckAction.Interact});
    }
    private static void ApplyDeckMode(ShipSnapshot? ship)
    {
        if(ship?.WalkableDeck!=true){_deckLease=Guid.Empty;return;}
        if(Gunner.LeaseId!=Guid.Empty && Gunner.LeaseId!=_deckLease)
        {
            CannonViewStarboard=Gunner.Starboard;CannonViewOpen=true;AutoPickCannonSide=false;
            GunYaw=(ship.CrewStations??[]).FirstOrDefault(s=>s.PlayerId==Globals.Me?.Id)?.Yaw??0;
            GunElevation=-Math.Atan(.12)*180/Math.PI;
        }
        else if(Gunner.LeaseId==Guid.Empty && _deckLease!=Guid.Empty)CannonViewOpen=false;
        _deckLease=Gunner.LeaseId;
    }
}
