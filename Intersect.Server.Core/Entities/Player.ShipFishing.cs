using Intersect.Framework.Core.Fishing;
using Intersect.Server.Fishing;

namespace Intersect.Server.Entities;
public partial class Player
{
    internal bool OpenFishingFromShip(Guid zoneId,Guid shipId)
    {
        lock(EntityLock)
        {
            if(!InGame || Client is not {IsEditor:false} || !Client.IsConnected() || Client.Entity!=this || _fishing!=null ||
                !Naval.NavalSystem.ValidFishingStation(this,shipId) || FishingOtherActivity())return false;
            var config=FishingRuntime.Current;
            var zone=config.Zones.FirstOrDefault(z=>z.Id==zoneId && FishingRules.WithinZone(z,MapId,X,Y));
            if(zone==null){FishingError("Fishing is unavailable here.","zone_unavailable");return false;}
            var tool=EquippedFishingTool(config);
            if(tool==null){FishingError("Equip a compatible fishing tool.","tool_missing",zone);return false;}
            var now=Environment.TickCount64;var language=Client.LanguageCode??"en";
            _fishing=new FishingSession
            {
                Configuration=config,Zone=zone,Tool=tool,StartMapId=MapId,InstanceId=MapInstanceId,StartX=X,StartY=Y,ShipId=shipId,PhaseStarted=now,
                State=new FishingState {SessionId=Guid.NewGuid(),ZoneId=zone.Id,ZoneName=zone.LocalizedNames.GetValueOrDefault(language)??Localization.ContentTranslationRuntime.Translate(zone.Name,language),
                    Phase=FishingPhase.Ready,AllowAutomatic=zone.AllowAutomatic,ToolItemId=tool.ItemId,ToolType=tool.Type,BaitItemId=tool.BaitItemId}
            };
            FishingCue("session_start");FishingWorld(FishingWorldAction.Idle,now);SendFishing(now,open:true);
            return true;
        }
    }
}
