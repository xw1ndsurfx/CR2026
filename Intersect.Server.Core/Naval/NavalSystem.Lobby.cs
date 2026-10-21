using Intersect.Enums;
using Intersect.Framework.Core.Naval;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Entities;
namespace Intersect.Server.Naval;

internal static partial class NavalSystem
{
    // Registry reads never take an entity lock. Lobby joins acquire the joining player's
    // entity lock only after PartyLobbyRuntime has released its directory lock.
    internal static (Guid ShipId,int Capacity)? LobbyShip(Player captain,bool atDock=false)
    {
        lock(Gate)
        {
            if(!IsEnabled() || !captain.InGame || captain.IsDead || captain.HasPendingDeath || captain.Z!=0 ||
                captain.InstanceType!=MapInstanceType.Overworld ||
                !Ships.TryGetValue(captain.CaptainedShipId,out var ship) || ship.OwnerId!=captain.Id ||
                ship.State.CaptainId!=captain.Id || captain.MapId!=ship.State.MapId || ship.Health<=0 || ship.SunkUntil>0 || atDock && !NearCrewDock(ship)) return null;
            return (ship.State.Id,ship.Definition.CrewCapacity);
        }
    }
    internal static string BoardFromLobby(Player player,Player captain,Guid shipId)
    {
        lock(player.EntityLock)
        {
            ShipState anchor;
            lock(Gate)
            {
                if(!IsEnabled() || !player.InGame || player.Client==null || player.User==null || !player.CanUseLobbyTravel ||
                    player.NavalShipId!=Guid.Empty || player.InstanceType!=MapInstanceType.Overworld || player.Z!=0 ||
                    !captain.InGame || captain.IsDead || captain.HasPendingDeath || captain.CaptainedShipId!=shipId ||
                    captain.InstanceType!=MapInstanceType.Overworld || captain.Z!=0 ||
                    !Ships.TryGetValue(shipId,out var ship) || ship.OwnerId!=captain.Id || ship.State.CaptainId!=captain.Id || captain.MapId!=ship.State.MapId ||
                    !player.Party.Contains(captain) || !NearCrewDock(ship) || ship.Crew.Count+1>=ship.Definition.CrewCapacity)
                    return "The captain must stop beside a Q dock with a free crew seat before you can board.";
                if(player.NavalCrewPassage is {Ship.Cargo.Count:>0} old && old.Ship.Id!=shipId)
                    return "Unload your previous crew cargo before joining another ship.";
                var passage=new Intersect.Framework.Core.Naval.ShipCrewPassage {
                    CaptainId=captain.Id,ShipName=ship.Name,Capacity=ship.Definition.CargoCapacity,CaptainSinkingSerial=ship.SinkingSerial,
                    Ship=new() {Id=shipId,AtSea=true,Cargo=player.NavalCrewPassage?.Ship.Cargo??[],
                        ReturnMapId=player.MapId,ReturnX=player.X,ReturnY=player.Y,ReturnDockX=player.X,ReturnDockY=player.Y}};
                if(!CommitCrewBoarding(player,passage,()=>player.User.Save(force:true)==UserSaveResult.Completed))
                    return "Crew data could not be saved. Try again.";
                ship.Crew[player.Id]=new(player.Name,Timing.Global.Milliseconds);player.BoardedShipId=shipId;
                player.CastTime=0;player.IsBlocking=false;anchor=CopyCrewAnchor(ship);
            }
            try
            {
                WarpAnchor(player,anchor);
                if(player.MapId!=anchor.MapId) {ReleaseCrew(player,true);return "Crew data could not be saved. Try again.";}
                lock(Gate) SendSnapshot(player);
                return "";
            }
            catch {ReleaseCrew(player,true);throw;}
        }
    }
}
