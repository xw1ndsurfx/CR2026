using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;

namespace Intersect.Server.Naval;
internal static partial class NavalSystem
{
    private static NavalNpcDeckMember[] NpcDeckSnapshot(Vessel ship)
    {
        if(ship.Enemy==null || ship.SunkUntil>0 || ship.Health<=0 || !ShipDeck.Supports(ship.Definition.Graphics))return [];
        var ids=ship.Enemy.DeckCrew();var result=new List<NavalNpcDeckMember>();
        for(int i=0;i<ids.Length;i++)
        {
            var starboard=i>3;var index=i==0?0:(i-1)%3;
            if(i>0 && index>=MountedCannons(ship,starboard))continue;
            var id=ids[i]==Guid.Empty?ship.Enemy.BoardingCrewNpcId:ids[i];
            if(NPCDescriptor.Get(id) is not {} npc || string.IsNullOrWhiteSpace(npc.Sprite))continue;
            var yaw=0d;
            if(i>0 && ship.EnemyHasTarget)
            {
                var heading=ship.State.Heading*Math.PI/180;var along=DeckKind(ship)==ShipDeckKind.Raft?0:GunneryAim.StationOffset(index,MountedCannons(ship,starboard),ship.Definition.Length);
                var beam=CannonBeam(ship,starboard);var radius=(DeckKind(ship)==ShipDeckKind.Raft?ship.Definition.Length:ship.Definition.Width)/2;
                var x=ship.State.X+Math.Sin(heading)*along+beam.X*radius;
                var y=ship.State.Y-Math.Cos(heading)*along+beam.Y*radius;
                var bearing=ShipEnemySteering.Bearing(ship.EnemyTargetX-x,ship.EnemyTargetY-y);
                yaw=Math.Clamp(ShipEnemySteering.Error(CannonHeading(ship,starboard),bearing),-_settings.Gunnery.AimArcDegrees,_settings.Gunnery.AimArcDegrees);
            }
            result.Add(new(){NpcId=id,Sprite=npc.Sprite,Role=i==0?ShipCrewRole.Helm:i<=3?ShipCrewRole.PortCannon:ShipCrewRole.StarboardCannon,Index=i==0?0:(i-1)%3,Yaw=yaw});
        }
        return result.ToArray();
    }
}
