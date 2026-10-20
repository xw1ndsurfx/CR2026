namespace Intersect.Framework.Core.Naval;

/// <summary>One server-controlled enemy, with a home and four local patrol waypoints.</summary>
public sealed class HerboniaPatrol
{
    public bool Enabled { get; set; }
    public ShipDefinition Ship { get; set; } = new() { Graphics="Ship_Herbonia_001.png",
        DamagedGraphics="Ship_Herbonia_001.png", SinkingGraphics="Ship_Herbonia_001_sink.png" };
    public double DetectionRange { get; set; } = 12;
    public double LeashRadius { get; set; } = 24;
    public double PatrolRadius { get; set; } = 5;
    public double DockSafeRange { get; set; } = 6;
    public int RespawnSeconds { get; set; } = 60;
    public bool IsValid() => Ship?.IsValid()==true &&
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
