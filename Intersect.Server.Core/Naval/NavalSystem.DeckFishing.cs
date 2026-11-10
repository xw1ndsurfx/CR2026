using Intersect.Framework.Core.Naval;
using Intersect.Server.Entities;
namespace Intersect.Server.Naval;
internal static partial class NavalSystem
{
    private sealed record DeckFishingRequest(Guid Ship,Guid Zone,Guid Lease);
    private static bool FishingBoatIdle(Vessel ship)=>DeckEnabled(ship) && ShipDeck.Activity(ship.Definition)==ShipDeckActivity.Fishing &&
        ship.SunkUntil==0 && ship.Health>0 && ship.EnemyBoardingEncounter==Guid.Empty && Math.Abs(ship.State.Speed)<.05 && Math.Abs(ship.State.Throttle)<.01;
    private static DeckFishingRequest? ReserveDeckFishing(Vessel ship,Player player,long now)
    {
        if(!FishingBoatIdle(ship)){player.GunneryMessage="Stop the boat before fishing.";return null;}
        var station=NearestDeckGun(ship,player.Id,null,now);
        if(station<0){player.GunneryMessage="Move closer to a free fishing station.";return null;}
        if(ship.Definition.FishingZoneId==Guid.Empty){player.GunneryMessage="No fishing zone is configured for this ship.";return null;}
        if(!ship.Guns.TryGetValue(station,out var seat))ship.Guns[station]=seat=new();
        seat.Owner=player.Id;seat.Lease=Guid.NewGuid();seat.Until=now+3500;seat.Yaw=0;seat.FishingStarted=false;
        ship.Deck[player.Id].Position=ShipDeck.Station(station>=_settings.Gunnery.CannonsPerSide,station%_settings.Gunnery.CannonsPerSide,DeckKind(ship));
        ship.Deck[player.Id].FacingX=DeckKind(ship)==ShipDeckKind.Raft?0:station>=_settings.Gunnery.CannonsPerSide?1:-1;
        ship.Deck[player.Id].FacingY=DeckKind(ship)==ShipDeckKind.Raft?1:0;
        return new(ship.State.Id,ship.Definition.FishingZoneId,seat.Lease);
    }
    internal static bool ValidFishingStation(Player player,Guid shipId)
    {
        lock(Gate)return player.NavalShipId==shipId && Ships.TryGetValue(shipId,out var ship) && FishingBoatIdle(ship) &&
            ship.State.MapId==player.MapId && (ship.State.CaptainId==player.Id || player.Party.Any(p=>p.Id==ship.State.CaptainId)) &&
            ship.Guns.Values.Any(g=>g.Owner==player.Id && g.Until>Intersect.Framework.Core.Timing.Global.Milliseconds);
    }
    private static void MaintainFishingStations(long now)
    {
        foreach(var ship in Ships.Values.Where(v=>ShipDeck.Activity(v.Definition)==ShipDeckActivity.Fishing))
            foreach(var seat in ship.Guns.Values.Where(g=>g.Owner!=Guid.Empty))
                if(FishingBoatIdle(ship) && Player.FindOnline(seat.Owner) is {IsFishingActive:true} player && player.NavalShipId==ship.State.Id)seat.Until=now+3500;
                else if(seat.FishingStarted || seat.Until<=now || !FishingBoatIdle(ship)){seat.Owner=seat.Lease=Guid.Empty;seat.Until=0;seat.FishingStarted=false;}
    }
}
