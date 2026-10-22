namespace Intersect.Framework.Core.Naval;

/// <summary>One server-controlled enemy, with a home and four local patrol waypoints.</summary>
public sealed class HerboniaPatrol
{
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
    public ShipDefinition Ship { get; set; } = new() { Graphics="Ship_Herbonia_001.png",
        DamagedGraphics="Ship_Herbonia_001.png", SinkingGraphics="Ship_Herbonia_001_sink.png" };
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
    public bool IsValid() => BoardingCrewCount is >=1 and <=12 && BoardingHealthPercent is >=1 and <=50 && double.IsFinite(BoardingRange) && BoardingRange is >=2 and <=8 && BoardingEntryX is >=0 and <=255 && BoardingEntryY is >=0 and <=255 && (BoardingDungeonId==Guid.Empty || BoardingEntryMapId!=Guid.Empty) && Loot!=null && Loot.Length<=32 && Loot.All(e=>e?.IsValid()==true) && Ship?.IsValid()==true && Level is >=1 and <=200 && MaximumHealth is >=1 and <=100000 &&
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
