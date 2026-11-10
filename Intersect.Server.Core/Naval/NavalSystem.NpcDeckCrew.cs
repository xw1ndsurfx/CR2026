using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;

namespace Intersect.Server.Naval;
internal static partial class NavalSystem
{
    private static NavalNpcDeckMember[] NpcDeckSnapshot(Vessel ship)
    {
        if(ship.Enemy==null || ship.SunkUntil>0 || ship.Health<=0 || !ShipDeck.Supports(ship.Definition.Graphics))return [];
        var result=new List<NavalNpcDeckMember>();
        foreach(var station in ship.Enemy.DeckStations())
        {
            var helm=station.Role==ShipCrewRole.Helm;var starboard=station.Role==ShipCrewRole.StarboardCannon;var index=station.Index;
            if(!helm && index>=MountedCannons(ship,starboard))continue;
            var id=station.NpcId==Guid.Empty?ship.Enemy.BoardingCrewNpcId:station.NpcId;
            if(NPCDescriptor.Get(id) is not {} npc || string.IsNullOrWhiteSpace(npc.Sprite))continue;
            var yaw=0d;
            if(!helm && ship.EnemyHasTarget)
            {
                var heading=ship.State.Heading*Math.PI/180;var along=StationOffset(ship,index,starboard);
                var beam=CannonBeam(ship,starboard);var radius=(DeckKind(ship)==ShipDeckKind.Raft?ship.Definition.Length:ship.Definition.Width)/2;
                var x=ship.State.X+Math.Sin(heading)*along+beam.X*radius;
                var y=ship.State.Y-Math.Cos(heading)*along+beam.Y*radius;
                var bearing=ShipEnemySteering.Bearing(ship.EnemyTargetX-x,ship.EnemyTargetY-y);
                yaw=Math.Clamp(ShipEnemySteering.Error(CannonHeading(ship,starboard),bearing),-_settings.Gunnery.AimArcDegrees,_settings.Gunnery.AimArcDegrees);
            }
            result.Add(new(){NpcId=id,Sprite=npc.Sprite,Role=station.Role,Index=index,Yaw=yaw});
        }
        return result.ToArray();
    }
}
