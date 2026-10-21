using Intersect.Client.Core;
using Intersect.Client.Core.Controls;
using Intersect.Client.Framework.Input;
using Intersect.Client.Framework.Gwen.DragDrop;
using Intersect.Client.General;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Naval;

internal static partial class NavalWorld
{
    internal static bool CargoOpen {get;set;}
    private static Guid _actionShip;
    private static readonly HashSet<int> _pressedSlots = [];
    private sealed record BallView(CannonballSnapshot Previous,CannonballSnapshot Current,long Received);
    private static Dictionary<Guid,BallView> _balls = [];
    internal static ShipSnapshot? OwnShip => Fresh ? _ships.Values.FirstOrDefault(v=>HasMember(v.Current,_viewer))?.Current : null;
    private static void ClearActions() {CannonViewOpen=false;CargoOpen=false; _actionShip=Guid.Empty;_pressedSlots.Clear();_balls.Clear(); }

    private static void ReadActions(bool busy)
    {
        var own=OwnShip;
        if (own==null) return;
        if (_actionShip!=own.Id) { _actionShip=own.Id;_pressedSlots.Clear(); }
        var active=Enumerable.Range(0,Math.Min(10,Options.Instance.Player.HotbarSlotCount))
            .Select(slot=>Controls.IsControlPressed(Control.HotkeyOffset+slot+1,out _,out var binding) ? (Slot:slot,Binding:binding) : (Slot:-1,Binding:(ControlBinding?)null))
            .Where(hit=>hit.Slot>=0).OrderByDescending(hit=>hit.Binding!.Modifier)
            .GroupBy(hit=>hit.Binding!.Key).Select(g=>g.First().Slot).ToHashSet();
        foreach(var slot in active) if (!busy && !_pressedSlots.Contains(slot)) ActivateSlot(slot);
        _pressedSlots.Clear();_pressedSlots.UnionWith(active);
    }

    internal static bool CrewProgress => OwnShip is {} ship && ship.CaptainId!=Globals.Me?.Id;
    internal static bool GunneryBarActive => CrewProgress && Gunner.LeaseId!=Guid.Empty && Gunner.ShipId==OwnShip?.Id;
    internal static CannonAmmoView? SlotAmmunition(int slot) => slot is >=0 and <10
        ? Gunner.Ammunition.FirstOrDefault(a=>(int)a.Kind==slot) : null;
    internal static (int Level,long Experience,long Required,bool Gunnery) RoleProgress => CrewProgress
        ? (Gunner.Level,Gunner.Experience,Gunner.Required,true) : (SailingLevel,SailingExperience,SailingRequired,false);

    internal static int Cooldown(int slot)
    {
        var own=OwnShip;if(own==null) return 0;
        var view=_ships[own.Id];
        var milliseconds=slot==0?own.PortReloadMilliseconds:slot==1?own.StarboardReloadMilliseconds:0;
        return (int)Math.Max(0,milliseconds-(Environment.TickCount64-view.Received));
    }
    internal static void ActivateSlot(int slot)
    {
        var own=OwnShip;var action=ShipActions.ForSlot(slot);
        if(GunneryBarActive)
        {
            // Selection only: mouse fire still uses the authoritative station request and reload.
            if(!DragAndDrop.IsDragging && Globals.Me?.IsBusy==false && own is {Sinking:false} && SlotAmmunition(slot) is {Available:true} ammo)
                SelectedAmmo=ammo.Kind;
            return;
        }
        if(own==null || DragAndDrop.IsDragging || Globals.Me?.IsBusy!=false || own.Sinking || action==ShipAction.None ||
            (action is ShipAction.PortCannons or ShipAction.StarboardCannons && (!own.CannonsEnabled || Cooldown(slot)>0))) return;
        if(action==ShipAction.CannonView) {CannonViewOpen=!CannonViewOpen;return;}
        if(action==ShipAction.Crew) {Networking.Network.SendPacket(new HarbourRequestPacket {View=HarbourView.Crew});return;}
        if(own.CaptainId!=Globals.Me!.Id && action==ShipAction.LeaveHelm)
        {Networking.Network.SendPacket(new HarbourRequestPacket {View=HarbourView.Crew,Action=HarbourAction.LeaveCrew});return;}
        if(own.CaptainId!=Globals.Me!.Id && action!=ShipAction.Cargo) return;
        if(action==ShipAction.Cargo) {CargoOpen=!CargoOpen;return;}
        Networking.Network.SendPacket(new ShipActionPacket { ShipId=own.Id,Action=action });
    }

    private static void ApplyCannonballs(NavalSnapshotPacket packet,long now)
    {
        var next=new Dictionary<Guid,BallView>();
        foreach(var ball in (packet.Cannonballs??[]).Take(512))
            next[ball.Id]=new(Fresh&&_balls.TryGetValue(ball.Id,out var before)?before.Current:ball,ball,now);
        _balls=next;
        if(!packet.Ships.Any(s=>HasMember(s,Globals.Me?.Id??Guid.Empty))) {CannonViewOpen=false;CargoOpen=false; _actionShip=Guid.Empty;_pressedSlots.Clear(); }
    }
    private static void DrawCannonballs(int row)
    {
        var current=Globals.Me?.MapInstance;if(current==null) return;
        var tw=Options.Instance.Map.TileWidth;var th=Options.Instance.Map.TileHeight;
        foreach(var ball in _balls.Values)
        {
            if(!Maps.MapInstance.TryGet(ball.Current.MapId,out var map) || !Globals.GridMaps.ContainsKey(ball.Current.MapId)) continue;
            var x=map.X+(float)ball.Current.X*tw;var y=map.Y+(float)ball.Current.Y*th;
            if(ball.Previous.MapId==ball.Current.MapId)
            {
                var t=Math.Clamp((Environment.TickCount64-ball.Received)/100f,0,1);
                x=map.X+(float)(ball.Previous.X+(ball.Current.X-ball.Previous.X)*t)*tw;
                y=map.Y+(float)(ball.Previous.Y+(ball.Current.Y-ball.Previous.Y)*t)*th;
            }
            if(row!=(int)Math.Floor((y-current.Y)/th)+Options.Instance.Map.MapHeight*2) continue;
            var age=ball.Current.AgeMilliseconds+Math.Max(0,Environment.TickCount64-ball.Received);
            if(DrawEffectAsset(Presentation.CannonProjectileGraphic,"Projectile"+ShipMovement.DirectionRow(ball.Current.Heading),x,y,age,hold:true)) continue;
            Graphics.DrawGameTexture(Graphics.Renderer.WhitePixel,new FloatRect(0,0,1,1),new FloatRect(x-3,y-3,6,6),new Color(255,210,160,60));
            Graphics.DrawGameTexture(Graphics.Renderer.WhitePixel,new FloatRect(0,0,1,1),new FloatRect(x-2,y-2,4,4),new Color(255,30,30,35));
        }
    }
}
