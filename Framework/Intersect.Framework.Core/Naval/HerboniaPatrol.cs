namespace Intersect.Framework.Core.Naval;

/// <summary>One server-controlled enemy, with a home and four local patrol waypoints.</summary>
public sealed class HerboniaPatrol
{
    public bool CanBoardPlayerShips {get;set;}
    public int PlayerBoardingHealthPercent {get;set;}=25;
    public Guid DeckCaptainNpcId {get;set;}
    public Guid PortGunner1NpcId {get;set;}
    public Guid PortGunner2NpcId {get;set;}
    public Guid PortGunner3NpcId {get;set;}
    public Guid StarboardGunner1NpcId {get;set;}
    public Guid StarboardGunner2NpcId {get;set;}
    public Guid StarboardGunner3NpcId {get;set;}
    public Guid PortGunner4NpcId {get;set;}
    public Guid PortGunner5NpcId {get;set;}
    public Guid PortGunner6NpcId {get;set;}
    public Guid StarboardGunner4NpcId {get;set;}
    public Guid StarboardGunner5NpcId {get;set;}
    public Guid StarboardGunner6NpcId {get;set;}
    public Guid[] DeckCrew()=>[DeckCaptainNpcId,PortGunner1NpcId,PortGunner2NpcId,PortGunner3NpcId,StarboardGunner1NpcId,StarboardGunner2NpcId,StarboardGunner3NpcId];
    public IEnumerable<(ShipCrewRole Role,int Index,Guid NpcId)> DeckStations()
    {
        yield return(ShipCrewRole.Helm,0,DeckCaptainNpcId);
        var model=NavalShipModels.Find(Ship.Graphics);
        Guid[][] sides=[[PortGunner1NpcId,PortGunner2NpcId,PortGunner3NpcId,PortGunner4NpcId,PortGunner5NpcId,PortGunner6NpcId],
            [StarboardGunner1NpcId,StarboardGunner2NpcId,StarboardGunner3NpcId,StarboardGunner4NpcId,StarboardGunner5NpcId,StarboardGunner6NpcId]];
        for(int side=0;side<2;side++)for(int i=0;i<(model?.CannonCount(side==1)??3);i++)
            yield return(side==0?ShipCrewRole.PortCannon:ShipCrewRole.StarboardCannon,i,sides[side][i]);
    }
    public bool AutomaticBoardingDeck {get;set;}=true;
    public Guid BoardingCrewNpcId {get;set;}
    public int BoardingCrewCount {get;set;}=4;
    public Guid BoardingDungeonId {get;set;}
    public Guid BoardingEntryMapId {get;set;}
    public int BoardingEntryX {get;set;}
    public int BoardingEntryY {get;set;}
    public int BoardingHealthPercent {get;set;}=25;
    public double BoardingRange {get;set;}=4;
    public bool Enabled { get; set; }
    public ShipDefinition Ship { get; set; } = new() { Graphics=NavalShipModels.ThreeMasts[1].Graphics,IdleGraphics=NavalShipModels.ThreeMasts[1].IdleGraphics,
        DamagedGraphics=NavalShipModels.ThreeMasts[1].DamagedGraphics,SinkingGraphics=NavalShipModels.ThreeMasts[1].SinkingGraphics,CrewCapacity=7 };
    public NavalLootEntry[] Loot {get;set;}=[];
    public int Level { get; set; } = 1;
    public int MaximumHealth { get; set; } = 100;
    public int CannonDamage { get; set; } = 10;
    public int SailingExperienceReward { get; set; } = 100;
    public double DetectionRange { get; set; } = 12;
    public double LeashRadius { get; set; } = 24;
    public double PatrolRadius { get; set; } = 5;
    public double DockSafeRange { get; set; } = 6;
    public int RespawnSeconds { get; set; } = 60;
    public bool IsValid() => PlayerBoardingHealthPercent is >=1 and <=50 && BoardingCrewCount is >=1 and <=12 && BoardingHealthPercent is >=1 and <=50 && double.IsFinite(BoardingRange) && BoardingRange is >=2 and <=8 && BoardingEntryX is >=0 and <=255 && BoardingEntryY is >=0 and <=255 && (BoardingDungeonId==Guid.Empty || BoardingEntryMapId!=Guid.Empty) && Loot!=null && Loot.Length<=32 && Loot.All(e=>e?.IsValid()==true) && Ship?.IsValid()==true && Level is >=1 and <=200 && MaximumHealth is >=1 and <=100000 &&
        CannonDamage is >=1 and <=100 && SailingExperienceReward is >=0 and <=1000000000 &&
        new[]{DetectionRange,LeashRadius,PatrolRadius,DockSafeRange}.All(double.IsFinite) &&
        DetectionRange is >=3 and <=40 && LeashRadius>=DetectionRange && LeashRadius<=80 &&
        PatrolRadius>=0 && PatrolRadius<=LeashRadius && DockSafeRange is >=3 and <=20 && RespawnSeconds is >=10 and <=3600;
}

public static class ShipEnemySteering
{
    public static double Bearing(double x,double y) => ShipMovement.Normalize(Math.Atan2(x,-y)*180/Math.PI);
    public static double Error(double heading,double desired) => (desired-heading+540)%360-180;
    public static ShipInput Towards(double heading,double desired,bool stop=false) =>
        new(stop?(sbyte)0:(sbyte)1,Math.Abs(Error(heading,desired))<3?(sbyte)0:(sbyte)Math.Sign(Error(heading,desired)),stop);
    public static bool BroadsideAligned(double heading,double bearing) => Math.Abs(Math.Abs(Error(heading,bearing))-90)<=12;
    public static bool CanDamage(bool shooterEnemy,bool targetEnemy,bool targetPlayer,bool sameOwner,bool playerDamage) =>
        !sameOwner && !(shooterEnemy && targetEnemy) && (!targetPlayer || shooterEnemy || playerDamage);
}
