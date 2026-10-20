using Intersect.Framework.Core;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
namespace Intersect.Server.Naval;

internal static partial class NavalSystem
{
    private sealed record Effect(NavalEffectSnapshot Snapshot,long Born);
    private static readonly List<Effect> Effects=[];
    private static void Emit(NavalEffectKind kind,Guid map,double x,double y,double heading,long now,bool sound=true)
    {
        Effects.RemoveAll(e=>now-e.Born>1500);
        if(Effects.Count>=512) Effects.RemoveAt(0);
        Effects.Add(new(new() {Id=Guid.NewGuid(),Kind=kind,MapId=map,X=x,Y=y,Heading=heading,PlaySound=sound},now));
    }
    internal static bool RenameOwnedShip(OwnedShip ship,string name,Func<bool> save)
    {
        var normalized=ShipNames.Normalize(name);if(normalized==null) return false;
        var previous=ship.Name;var committed=false;
        try {ship.Name=normalized;committed=save();return committed;}
        finally {if(!committed) ship.Name=previous;}
    }
    private static void AwardSailing(Player player,Vessel vessel,double distance,long now)
    {
        if(distance<=0 || !double.IsFinite(distance) || player.SailingExperience>=_settings.Sailing.Cap) return;
        vessel.SailingRemainder+=distance*_settings.Sailing.ExperiencePerTile;
        var earned=(long)Math.Floor(vessel.SailingRemainder);if(earned<=0) return;
        vessel.SailingRemainder-=earned;
        var before=_settings.Sailing.Progress(player.SailingExperience).Level;
        player.SailingExperience=Math.Min(_settings.Sailing.Cap,player.SailingExperience+earned);
        if(_settings.Sailing.Progress(player.SailingExperience).Level>before)
            Emit(NavalEffectKind.LevelUp,vessel.State.MapId,vessel.State.X,vessel.State.Y,0,now);
    }
}
