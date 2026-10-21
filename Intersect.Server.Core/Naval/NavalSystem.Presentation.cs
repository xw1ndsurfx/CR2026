using Intersect.Framework.Core;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
namespace Intersect.Server.Naval;

internal static partial class NavalSystem
{
    private sealed record Effect(NavalEffectSnapshot Snapshot,long Born);
    private static readonly List<Effect> Effects=[];
    private static void Emit(NavalEffectKind kind,Guid map,double x,double y,double heading,long now,bool sound=true,Guid animationId=default,string graphic=null)
    {
        Effects.RemoveAll(e=>now-e.Born>1500);
        if(Effects.Count>=512) Effects.RemoveAt(0);
        Effects.Add(new(new() {Id=Guid.NewGuid(),Kind=kind,MapId=map,X=x,Y=y,Heading=heading,PlaySound=sound,AnimationId=animationId,Graphic=graphic??""},now));
    }
    // Only committed level changes call this helper. Gate protects both membership and effect publication.
    private static void EmitLevelUp(Player player,int before,int after,long now)
    {
        if(after<=before) return;
        lock(Gate)
        {
            if(!Ships.TryGetValue(player.NavalShipId,out var ship) || ship.Enemy!=null || ship.Health<=0 || ship.SunkUntil!=0 ||
                ship.State.CaptainId!=player.Id && !ship.Crew.ContainsKey(player.Id)) return;
            Emit(NavalEffectKind.LevelUp,ship.State.MapId,ship.State.X,ship.State.Y,ship.State.Heading,now,
                animationId:_settings.Presentation.LevelUpAnimationId);
        }
    }
    internal static bool RenameOwnedShip(OwnedShip ship,string name,Func<bool> save)
    {
        var normalized=ShipNames.Normalize(name);if(normalized==null) return false;
        var previous=ship.Name;var committed=false;
        try {ship.Name=normalized;committed=save();return committed;}
        finally {if(!committed) ship.Name=previous;}
    }
}
