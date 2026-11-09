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
            var id=ids[i]==Guid.Empty?ship.Enemy.BoardingCrewNpcId:ids[i];
            if(NPCDescriptor.Get(id) is not {} npc || string.IsNullOrWhiteSpace(npc.Sprite))continue;
            var yaw=0d;
            if(i>0 && ship.EnemyHasTarget)
            {
                var heading=ship.State.Heading*Math.PI/180;var along=((i-1)%3-1)*ship.Definition.Length/3;
                var side=(i<=3?-1:1)*ship.Definition.Width/2;
                var x=ship.State.X+Math.Sin(heading)*along+Math.Cos(heading)*side;
                var y=ship.State.Y-Math.Cos(heading)*along+Math.Sin(heading)*side;
                var bearing=ShipEnemySteering.Bearing(ship.EnemyTargetX-x,ship.EnemyTargetY-y);
                yaw=Math.Clamp(ShipEnemySteering.Error(ship.State.Heading+(i<=3?-90:90),bearing),-_settings.Gunnery.AimArcDegrees,_settings.Gunnery.AimArcDegrees);
            }
            result.Add(new(){NpcId=id,Sprite=npc.Sprite,Role=i==0?ShipCrewRole.Helm:i<=3?ShipCrewRole.PortCannon:ShipCrewRole.StarboardCannon,Index=i==0?0:(i-1)%3,Yaw=yaw});
        }
        return result.ToArray();
    }
}
