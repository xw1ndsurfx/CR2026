using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;

namespace Intersect.Server.Naval;
internal static partial class NavalSystem
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid,int> DeckPartyCapacities=new();
    internal static int DeckPartyCapacity(Player leader)
    {
        // Called from naval snapshots under Gate and from invitations under the
        // party lock: never acquire either lock here, or reverse their order.
        var capacity=DeckPartyCapacities.GetValueOrDefault(leader.Id);var party=leader.Party;
        for(var i=0;i<party.Count;i++)
        {
            Player member;try{member=party[i];}catch(ArgumentOutOfRangeException){break;}
            capacity=Math.Max(capacity,DeckPartyCapacities.GetValueOrDefault(member.Id));
        }
        return capacity;
    }
    internal static int PartyCapacity(Player leader)=>Math.Max(Options.Instance.Party.MaximumMembers,Math.Max(leader.Party.Count,DeckPartyCapacity(leader)));
    private sealed class DeckWalker
    {
        internal DeckPoint Position;
        internal double FacingX,FacingY=1;
        internal long LastMove,LastInteraction,MovedAt;
    }
    private static bool DeckEnabled(Vessel v)=>v.Enemy==null && ShipDeck.Supports(v.Definition.Graphics);
    private static int VesselCapacity(Vessel v)=>DeckEnabled(v)?NavalShipModels.Find(v.Definition.Graphics)!.CrewCapacity:v.Definition.CrewCapacity;
    private static string DeckGraphic(Vessel v)
    {
        var family=NavalShipModels.Find(v.Definition.Graphics);
        if(v.State.DamageState>0 && family!=null)return NavalShipModels.Find(v.Definition.DamagedGraphics)?.Id==family.Id?v.Definition.DamagedGraphics:family.DamagedGraphics;
        var idle=IdleGraphic(v.Definition);
        if(family!=null && NavalShipModels.Find(idle)?.Id!=family.Id)idle=family.IdleGraphics;
        return Math.Abs(v.State.Speed)<.05 && v.Definition.Frames==4 && family!=null && NavalShipModels.Find(idle)?.Id==family.Id?idle:v.Definition.Graphics;
    }
    private static string SinkingGraphic(Vessel ship)=>NavalShipModels.Find(ship.Definition.Graphics)?.SinkingGraphics??ship.SinkingGraphics;
    private static void EnsureDeck(Vessel v,long now)
    {
        if(!DeckEnabled(v))return;
        if(v.State.CaptainId!=Guid.Empty)DeckPartyCapacities[v.State.CaptainId]=VesselCapacity(v);
        var members=v.Crew.Where(c=>now-c.Value.Seen<=1500).Select(c=>c.Key).Append(v.State.CaptainId).Where(id=>id!=Guid.Empty).ToHashSet();
        foreach(var id in v.Deck.Keys.Where(id=>!members.Contains(id)).ToArray())v.Deck.Remove(id);
        foreach(var id in members.OrderBy(id=>id))
        {
            if(!v.Deck.TryGetValue(id,out var walker))
            {
                walker=new(){Position=SpawnDeck(v),LastMove=now,LastInteraction=now-250};v.Deck[id]=walker;
            }
            if(!ShipDeck.Contains(walker.Position,DeckKind(v)))walker.Position=SpawnDeck(v);
            if(id==Driver(v))walker.Position=ShipDeck.Helm;
            else
            {
                var seat=v.Guns.FirstOrDefault(g=>g.Value.Owner==id && g.Value.Until>now);
                if(seat.Value!=null)walker.Position=ShipDeck.Station(seat.Key>=_settings.Gunnery.CannonsPerSide,seat.Key%_settings.Gunnery.CannonsPerSide,DeckKind(v));
            }
        }
        // A seat/helm transfer may snap its new operator onto a walking player's
        // feet. Move only the unseated player aside so neither actor gets trapped.
        foreach(var pair in v.Deck)
        {
            if(pair.Key==Driver(v) || v.Guns.Any(g=>g.Value.Owner==pair.Key && g.Value.Until>now) ||
                !v.Deck.Any(other=>other.Key!=pair.Key && ShipDeck.Distance(pair.Value.Position,other.Value.Position)<ShipDeck.Radius*2-.001))continue;
            var origin=pair.Value.Position;DeckPoint? free=null;
            foreach(var radius in new[]{.32,.48,.64})
            {
                for(int y=-1;y<=1 && free==null;y++)for(int x=-1;x<=1 && free==null;x++)
                {
                    if(x==0 && y==0)continue;var length=Math.Sqrt(x*x+y*y);
                    var candidate=new DeckPoint(origin.X+x*radius/length,origin.Y+y*radius/length);
                    if(ShipDeck.Contains(candidate,DeckKind(v)) && !v.Deck.Any(other=>other.Key!=pair.Key && ShipDeck.Distance(candidate,other.Value.Position)<ShipDeck.Radius*2+.001))free=candidate;
                }
                if(free.HasValue)break;
            }
            pair.Value.Position=free??SpawnDeck(v);pair.Value.MovedAt=0;
        }
    }
    private static DeckPoint SpawnDeck(Vessel v)
    {
        foreach(var y in new[]{-2.9,-1.3,1.3,.7,-.7,2.9})
            foreach(var x in new[]{-.5,.5,-.25,.25})
            {
                var p=new DeckPoint(x,y);
                if(ShipDeck.Contains(p,DeckKind(v)) && !v.Deck.Values.Any(w=>ShipDeck.Distance(w.Position,p)<ShipDeck.Radius*2.1))return p;
            }
        return new(-.5,-1.3);
    }
    private static NavalDeckMember[] DeckSnapshot(Vessel v,long now)
    {
        if(!DeckEnabled(v) || v.SunkUntil>0 || v.Health<=0){DeckPartyCapacities.TryRemove(v.State.CaptainId,out _);return [];}
        EnsureDeck(v,now);
        return v.Deck.Select(p=>new NavalDeckMember {PlayerId=p.Key,X=p.Value.Position.X,Y=p.Value.Position.Y,
            FacingX=p.Value.FacingX,FacingY=p.Value.FacingY,Walking=now-p.Value.MovedAt<180 && p.Value.MovedAt>0 &&
            p.Key!=Driver(v) && !v.Guns.Any(g=>g.Value.Owner==p.Key && g.Value.Until>now)}).ToArray();
    }
    private static int NearestDeckGun(Vessel v,Guid player,bool? side,long now)
    {
        EnsureDeck(v,now);
        if(!v.Deck.TryGetValue(player,out var walker))return -1;
        return Enumerable.Range(0,_settings.Gunnery.CannonsPerSide*2)
            .Where(i=>ValidGunKey(v,i) && (!side.HasValue || side.Value==(i>=_settings.Gunnery.CannonsPerSide)) &&
                (!v.Guns.TryGetValue(i,out var gun) || gun.Owner==Guid.Empty || gun.Until<=now || gun.Owner==player))
            .Select(i=>(Index:i,Distance:ShipDeck.Distance(walker.Position,ShipDeck.Station(i>=_settings.Gunnery.CannonsPerSide,i%_settings.Gunnery.CannonsPerSide,DeckKind(v)))))
            .Where(p=>p.Distance<=ShipDeck.UseDistance).OrderBy(p=>p.Distance).Select(p=>p.Index).FirstOrDefault(-1);
    }
    internal static void DeckInput(Player player,DeckInputPacket packet,long now)
    {
        lock(player.EntityLock)
        {
            var fishing=DeckInputLocked(player,packet,now);if(fishing==null)return;
            var opened=false;
            try{opened=player.OpenFishingFromShip(fishing.Zone,fishing.Ship);}
            finally
            {
                lock(Gate)
                    if(Ships.TryGetValue(fishing.Ship,out var ship))
                        foreach(var seat in ship.Guns.Values.Where(s=>s.Owner==player.Id && s.Lease==fishing.Lease))
                            if(opened)seat.FishingStarted=true;
                            else{seat.Owner=seat.Lease=Guid.Empty;seat.Until=0;}
            }
        }
    }
    private static DeckFishingRequest? DeckInputLocked(Player player,DeckInputPacket packet,long now)
    {
        lock(player.EntityLock)lock(Gate)
        {
            if(!IsEnabled() || !Enum.IsDefined(packet.Action) || packet.Side is <-127 or >127 || packet.Forward is <-127 or >127 ||
                player.NavalPartyExitRequested || !player.InGame || player.IsDead || player.HasPendingDeath || player.NavalBusy || player.Z!=0 ||
                player.InstanceType!=MapInstanceType.Overworld || player.NavalShipId!=packet.ShipId ||
                !Ships.TryGetValue(packet.ShipId,out var v) || !DeckEnabled(v) || v.State.CaptainId==Guid.Empty || v.State.MapId!=player.MapId ||
                v.SunkUntil>0 || v.Health<=0 || v.EnemyBoardingEncounter!=Guid.Empty || Repairs.ContainsKey(player.Id) ||
                player.Id!=v.State.CaptainId && (!player.Party.Any(p=>p.Id==v.State.CaptainId) || !v.Crew.TryGetValue(player.Id,out var crew) || now-crew.Seen>1500))return null;
            if(packet.Action==DeckAction.Walk && v.Deck.TryGetValue(player.Id,out var recent) && now-recent.LastMove<40)return null;
            PruneGuns(v,now);EnsureDeck(v,now);
            if(!v.Deck.TryGetValue(player.Id,out var walker))return null;
            var seat=v.Guns.FirstOrDefault(g=>g.Value.Owner==player.Id && g.Value.Until>now);
            if(packet.Action==DeckAction.Interact)
            {
                if(now-walker.LastInteraction<250)return null;walker.LastInteraction=now;
                player.GunneryMessage="";
                if(seat.Value!=null){seat.Value.Owner=seat.Value.Lease=Guid.Empty;seat.Value.Until=0;}
                else if(Driver(v)==player.Id){v.HelmVacant=true;v.HelmsmanId=Guid.Empty;v.Controls.Reset();}
                else if(v.HelmVacant && ShipDeck.Distance(walker.Position,ShipDeck.Helm)<=ShipDeck.UseDistance)
                {v.HelmVacant=false;v.HelmsmanId=player.Id==v.State.CaptainId?Guid.Empty:player.Id;v.Controls.Reset();walker.Position=ShipDeck.Helm;}
                else if(TryStartDeckRepair(v,player,now)){walker.LastMove=now;return null;}
                else if(ShipDeck.Activity(v.Definition)==ShipDeckActivity.Fishing)
                {
                    var fishing=ReserveDeckFishing(v,player,now);walker.LastMove=now;player.RegisterAfkActivity();SendSnapshot(player);return fishing;
                }
                else
                {
                    var index=NearestDeckGun(v,player.Id,null,now);
                    if(index>=0 && _settings.CannonsEnabled && _settings.Gunnery.Enabled)
                    {
                        if(!v.Guns.TryGetValue(index,out var gun))v.Guns[index]=gun=new();
                        gun.Owner=player.Id;gun.Lease=Guid.NewGuid();gun.Until=now+3500;gun.Yaw=0;gun.AimAt=now-100;
                        walker.Position=ShipDeck.Station(index>=_settings.Gunnery.CannonsPerSide,index%_settings.Gunnery.CannonsPerSide,DeckKind(v));
                        player.GunneryMessage="";
                    }
                    else player.GunneryMessage="Move closer to a free cannon or the helm.";
                }
                walker.LastMove=now;player.RegisterAfkActivity();SendSnapshot(player);return null;
            }
            var elapsed=now-walker.LastMove;if(elapsed<40)return null;walker.LastMove=now;
            if(Driver(v)==player.Id || seat.Value!=null)return null;
            var before=walker.Position;
            walker.Position=ShipDeck.Move(before,packet.Side/127d,packet.Forward/127d,Math.Min(100,elapsed)/1000d,
                p=>v.Deck.Any(other=>other.Key!=player.Id && ShipDeck.Distance(p,other.Value.Position)<ShipDeck.Radius*2),DeckKind(v));
            if(ShipDeck.Distance(before,walker.Position)>.0001)
            {walker.FacingX=packet.Side/127d;walker.FacingY=packet.Forward/127d;walker.MovedAt=now;player.RegisterAfkActivity();}
        }
        return null;
    }
}
