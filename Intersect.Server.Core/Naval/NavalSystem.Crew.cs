using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Entities;
using Intersect.Server.Maps;
using Intersect.Utilities;
namespace Intersect.Server.Naval;

internal static partial class NavalSystem
{
    private sealed record CrewPresence(string Name,long Seen);
    private static bool NearCrewDock(Vessel vessel)=>Math.Abs(vessel.State.Speed)<=.1 && vessel.SunkUntil==0 &&
        ShipDock.Tiles(MapController.Get(vessel.State.MapId)?.Attributes).Any(t=>Distance(t.X+.5,t.Y+.5,vessel.State.X,vessel.State.Y)<=6);
    private static bool BesideCrewDock(Player player)=>OceanMapResolver.Maps().Contains(player.MapId) &&
        ShipDock.Nearest(MapController.Get(player.MapId)?.Attributes,player.X+.5,player.Y+.5,_settings.DockInteractionRange)!=null;
    private static bool EligibleCrewShip(Player player,Vessel vessel)=>vessel.OwnerId!=Guid.Empty && vessel.State.CaptainId!=Guid.Empty &&
        vessel.State.MapId==player.MapId && vessel.Enemy==null && ShipCrewRules.CanBoard(BesideCrewDock(player),player.Party.Any(p=>p.Id==vessel.State.CaptainId),
            vessel.BoardingOpen,NearCrewDock(vessel),vessel.SunkUntil==0,vessel.Crew.Count+1,vessel.Definition.CrewCapacity) &&
        Distance(player.X+.5,player.Y+.5,vessel.State.X,vessel.State.Y)<=8;

    private static ShipCrewState CrewState(Player player)
    {
        Ships.TryGetValue(player.NavalShipId,out var own);
        var members=own==null?[]:new[]{new ShipCrewMember {PlayerId=own.State.CaptainId,Name=own.CaptainName,Captain=true}}
            .Concat(own.Crew.Select(m=>new ShipCrewMember {PlayerId=m.Key,Name=m.Value.Name})).Where(m=>m.PlayerId!=Guid.Empty).ToArray();
        var dock=own!=null?NearCrewDock(own):BesideCrewDock(player);
        return new() {ShipId=own?.State.Id??Guid.Empty,CaptainId=own?.State.CaptainId??Guid.Empty,ShipName=own?.Name??"",
            Capacity=own?.Definition.CrewCapacity??0,BoardingOpen=own?.BoardingOpen??false,Members=members,AtDock=dock,
            PersonalCargoCount=player.NavalCrewPassage?.Ship.Cargo.Count??0,
            Offers=own!=null || !dock?[]:Ships.Values.Where(v=>EligibleCrewShip(player,v)).Take(32).Select(v=>new ShipCrewOffer {
                ShipId=v.State.Id,Name=v.Name,CaptainName=v.CaptainName,Occupied=v.Crew.Count+1,Capacity=v.Definition.CrewCapacity}).ToArray()};
    }
    internal static void Crew(Player player,HarbourRequestPacket packet)
    {
        lock(player.EntityLock)
        {
            var now=Timing.Global.Milliseconds;
            if(!HarbourAccessRules.Allows(HarbourView.Crew,packet.Action) || now-player.LastHarbourRequest<500) return;
            player.LastHarbourRequest=now;
            if(packet.Action!=HarbourAction.Open && !ApplyPendingExperience(player,now,force:true))
            {lock(Gate) player.SendPacket(new HarbourStatePacket {View=HarbourView.Crew,Crew=CrewState(player),Message="Your Sailing reward could not be saved. Try again shortly."});return;}
            ApplyCharacterExperience(player,now);
            string message="";ShipState anchor=null;bool leave=false,claim=false;
            lock(Gate)
            {
                if(!IsEnabled() || !player.InGame || player.Client==null || player.User==null || player.IsDead || player.HasPendingDeath ||
                    player.Z!=0 || player.InstanceType!=MapInstanceType.Overworld || player.NavalBusy) return;
                Ships.TryGetValue(player.NavalShipId,out var own);
                if(packet.Action==HarbourAction.ToggleCrewBoarding)
                {
                    if(own==null || own.OwnerId!=player.Id || own.State.CaptainId!=player.Id) message="Only the captain of a personal ship can open boarding.";
                    else if(!NearCrewDock(own)) message="Stop beside a Q dock before opening boarding.";
                    else {own.BoardingOpen=!own.BoardingOpen;own.CaptainName=player.Name;}
                }
                else if(packet.Action==HarbourAction.JoinCrew)
                {
                    if(player.NavalShipId!=Guid.Empty || !BesideCrewDock(player) || !Ships.TryGetValue(packet.ShipId,out var ship) || !EligibleCrewShip(player,ship))
                        message="Boarding is unavailable. Join the captain's party and stand beside the Q dock.";
                    else if(player.NavalCrewPassage is {Ship.Cargo.Count:>0} old && old.Ship.Id!=packet.ShipId)
                        message="Unload your previous crew cargo before joining another ship.";
                    else
                    {
                        var cargo=player.NavalCrewPassage?.Ship.Cargo??[];
                        var passage=new ShipCrewPassage() {CaptainId=ship.State.CaptainId,ShipName=ship.Name,Capacity=ship.Definition.CargoCapacity,CaptainSinkingSerial=ship.SinkingSerial,
                            Ship=new() {Id=ship.State.Id,AtSea=true,Cargo=cargo,ReturnMapId=player.MapId,ReturnX=player.X,ReturnY=player.Y,
                                ReturnDockX=player.X,ReturnDockY=player.Y}};
                        if(!CommitCrewBoarding(player,passage,()=>player.User.Save(force:true)==UserSaveResult.Completed)) message="Crew data could not be saved. Try again.";
                        else
                        {
                            ship.Crew[player.Id]=new(player.Name,now);player.BoardedShipId=ship.State.Id;
                            player.CastTime=0;player.IsBlocking=false;
                            anchor=CopyCrewAnchor(ship);
                        }
                    }
                }
                else if(packet.Action==HarbourAction.LeaveCrew)
                {
                    if(player.BoardedShipId==Guid.Empty) message="You are not aboard as crew.";
                    else if(own!=null && own.State.CaptainId!=Guid.Empty && !NearCrewDock(own)) message="Stop beside a Q dock before disembarking.";
                    else leave=true;
                }
                else if(packet.Action==HarbourAction.ClaimCargo)
                {
                    if(player.NavalShipId!=Guid.Empty || !BesideCrewDock(player)) message="Stand beside a Q dock to unload your crew cargo.";
                    else claim=true;
                }
            }
            if(claim) message=ClaimCargo(player,player.NavalCrewPassage?.Ship);
            if(leave) ReleaseCrew(player,true);
            if(anchor!=null)
            {
                WarpAnchor(player,anchor);
                if(player.MapId!=anchor.MapId) ReleaseCrew(player,true);
            }
            lock(Gate) player.SendPacket(new HarbourStatePacket {View=HarbourView.Crew,Crew=CrewState(player),Message=message});
        }
    }
    private static ShipState CopyCrewAnchor(Vessel vessel)=>new() {Id=vessel.State.Id,MapId=vessel.State.MapId,X=vessel.State.X,Y=vessel.State.Y};
    private static void UpdateCrewPassenger(Player player,long now)
    {
        if(player.BoardedShipId==Guid.Empty) return;
        ShipState anchor=null;bool returnToShore=false,sunk=false,wait=false;
        lock(Gate)
        {
            if(!Ships.TryGetValue(player.BoardedShipId,out var ship) || !ship.Crew.ContainsKey(player.Id)) returnToShore=true;
            else
            {
                sunk=ship.Health<=0 || ship.SunkUntil>0;
                wait=sunk && now<ship.SunkUntil;
                returnToShore=ship.State.CaptainId==Guid.Empty || sunk || !player.InGame || player.IsDead || player.HasPendingDeath ||
                    player.Client==null || player.InstanceType!=MapInstanceType.Overworld ||
                    !OceanMapResolver.Maps().Contains(ship.State.MapId) || MapController.Get(ship.State.MapId)==null;
                if(!returnToShore) {ship.Crew[player.Id]=new(player.Name,now);anchor=CopyCrewAnchor(ship);}
            }
        }
        if(sunk && player.NavalCrewPassage is {} passage && passage.Ship.Health>0 &&
            !CommitOwnedHullDamage(passage.Ship,100,()=>player.User?.Save(force:true)==UserSaveResult.Completed)) return;
        if(wait) return;
        if(returnToShore) {ReleaseCrew(player,true);return;}
        if(anchor!=null)
        {
            if(player.MapId!=anchor.MapId) WarpAnchor(player,anchor);
            else {player.X=(int)Math.Floor(anchor.X);player.Y=(int)Math.Floor(anchor.Y);}
        }
    }
    internal static bool CommitCrewBoarding(Player player,ShipCrewPassage passage,Func<bool> save)
    {
        var before=player.NavalFleetJson;var committed=false;
        try {player.NavalCrewPassage=passage;committed=save();return committed;}
        finally {if(!committed) player.NavalFleetJson=before;}
    }
    internal static void ReconcileCrewCargo(ShipCrewPassage passage,string captainFleetJson)
    {
        var source=NavalPlayerData.Parse(captainFleetJson).Ships.FirstOrDefault(s=>s.Id==passage.Ship.Id);
        if(source==null || source.SinkingSerial!=passage.CaptainSinkingSerial || source.Sunk)
        {passage.Ship.Health=0;passage.Ship.Cargo.Clear();}
    }
    private static void ReleaseCrew(Player player,bool returnToHarbour)
    {
        lock(player.EntityLock)
        {
            ApplyPendingExperience(player,Timing.Global.Milliseconds,force:true);ApplyCharacterExperience(player,Timing.Global.Milliseconds);
            var passage=player.NavalCrewPassage;
            lock(Gate)
            {
                if(Ships.TryGetValue(player.BoardedShipId,out var ship))
                {
                    if((ship.Health<=0 || ship.SunkUntil>0) && passage!=null) {passage.Ship.Health=0;passage.Ship.Cargo.Clear();}
                    ship.Crew.Remove(player.Id);
                    if(ship.Crew.Count==0 && ship.State.CaptainId==Guid.Empty && ship.OwnerId!=Guid.Empty) Ships.Remove(ship.State.Id);
                }
                player.BoardedShipId=Guid.Empty;
            }
            if(returnToHarbour && passage!=null) ReturnToHarbour(player,passage.Ship);
            if(passage!=null) passage.Ship.AtSea=false;
            player.User?.Save(force:true);
        }
    }
}
