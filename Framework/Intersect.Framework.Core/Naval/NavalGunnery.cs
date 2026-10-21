using MessagePack;
namespace Intersect.Framework.Core.Naval;

public enum CannonAmmoKind : byte { Standard, Heavy, LongRange }
public enum CannonStationAction : byte { Acquire, Release, Renew, Fire }
public sealed class GunnerySettings
{
    public bool Enabled {get;set;}=true;
    public int CannonsPerSide {get;set;}=3;
    public int GunReloadMilliseconds {get;set;}=2500;
    public double AimArcDegrees {get;set;}=45;
    public int ExperiencePerDamage {get;set;}=1;
    public int MaximumLevel {get;set;}=100;
    public int BaseExperience {get;set;}=100;
    public double ExperienceGrowth {get;set;}=1.12;
    public SailingSettings Curve()=>new(){MaximumLevel=MaximumLevel,BaseExperience=BaseExperience,ExperienceGrowth=ExperienceGrowth};
    public bool IsValid()=>CannonsPerSide is >=1 and <=4 && GunReloadMilliseconds is >=500 and <=60000 &&
        double.IsFinite(AimArcDegrees) && AimArcDegrees is >=10 and <=60 && ExperiencePerDamage is >=0 and <=1000 && Curve().IsValid();
}
public sealed class CannonAmmunition
{
    public CannonAmmoKind Kind {get;set;}
    public int RequiredLevel {get;set;}=1;
    public bool RequiresItem {get;set;}
    public Guid ItemId {get;set;}
    public Guid ImpactAnimationId {get;set;}
    public int UnitsPerShot {get;set;}=1;
    public int DamagePercent {get;set;}=100;
    public int RangePercent {get;set;}=100;
    public int ReloadPercent {get;set;}=100;
    public bool IsValid()=>Enum.IsDefined(Kind) && RequiredLevel is >=1 and <=200 && UnitsPerShot is >=1 and <=100 &&
        DamagePercent is >=10 and <=300 && RangePercent is >=25 and <=200 && ReloadPercent is >=25 and <=300;
    public static CannonAmmunition[] Defaults()=>[new(),new(){Kind=CannonAmmoKind.Heavy,RequiredLevel=3,RequiresItem=true,DamagePercent=150,RangePercent=70,ReloadPercent=150},
        new(){Kind=CannonAmmoKind.LongRange,RequiredLevel=5,RequiresItem=true,DamagePercent=80,RangePercent=140,ReloadPercent=120}];
    public static string Caption(CannonAmmoKind kind)=>kind switch {CannonAmmoKind.Heavy=>"Heavy cannonball",CannonAmmoKind.LongRange=>"Long-range cannonball",_=>"Standard cannonball"};
}
[MessagePackObject]
public sealed class CannonAmmoView
{
    [Key(0)] public CannonAmmoKind Kind {get;set;}
    [Key(1)] public int RequiredLevel {get;set;}
    [Key(2)] public int Quantity {get;set;}
    [Key(3)] public bool Available {get;set;}
    [Key(4)] public int DamagePercent {get;set;}
    [Key(5)] public int RangePercent {get;set;}
    [Key(6)] public int ReloadPercent {get;set;}
}
[MessagePackObject]
public sealed class GunneryState
{
    [Key(0)] public Guid ShipId {get;set;}
    [Key(1)] public Guid LeaseId {get;set;}
    [Key(2)] public int Station {get;set;}=-1;
    [Key(3)] public bool Starboard {get;set;}
    [Key(4)] public int ReloadMilliseconds {get;set;}
    [Key(5)] public int Level {get;set;}=1;
    [Key(6)] public long Experience {get;set;}
    [Key(7)] public long Required {get;set;}
    [Key(8)] public CannonAmmoView[] Ammunition {get;set;}=[];
    [Key(9)] public double AimArcDegrees {get;set;}=45;
    [Key(10)] public int CannonsPerSide {get;set;}=3;
    [Key(11)] public string Message {get;set;}="";
    [Key(12)] public bool Enabled {get;set;}
}
public static class GunneryAim
{
    public const double EyeHeight=1.6;
    public static bool Valid(double yaw,double elevation,double arc)=>double.IsFinite(yaw) && double.IsFinite(elevation) &&
        Math.Abs(yaw)<=arc+.00001 && elevation is >=-75 and <=35;
    // Projection is identical to the perspective preview: vertical FOV 65 degrees, camera tilted down atan(.12).
    public static (double Yaw,double Elevation) FromViewport(double x,double y,double aspect,double arc)
    {
        var tangent=Math.Tan(65*Math.PI/360);var pitch=-Math.Atan(.12);
        var horizontal=Math.Clamp(x,-1,1)*tangent*aspect;
        var vertical=Math.Clamp(y,-1,1)*tangent;
        var forward=Math.Cos(pitch)-vertical*Math.Sin(pitch);
        var up=Math.Sin(pitch)+vertical*Math.Cos(pitch);
        return (Math.Clamp(Math.Atan2(horizontal,forward)*180/Math.PI,-arc,arc),
            Math.Clamp(Math.Atan2(up,Math.Sqrt(forward*forward+horizontal*horizontal))*180/Math.PI,-75,35));
    }
    public static (double X,double Y) ToViewport(double yaw,double elevation,double aspect)
    {
        var yr=yaw*Math.PI/180;var er=elevation*Math.PI/180;var pitch=-Math.Atan(.12);
        var horizontal=Math.Sin(yr)*Math.Cos(er);var forward=Math.Cos(yr)*Math.Cos(er);var up=Math.Sin(er);
        var depth=forward*Math.Cos(pitch)+up*Math.Sin(pitch);
        var vertical=-forward*Math.Sin(pitch)+up*Math.Cos(pitch);var tangent=Math.Tan(65*Math.PI/360);
        return (horizontal/(depth*tangent*aspect),vertical/(depth*tangent));
    }
    public static double Range(double elevation,double maximum)=>elevation>=0?maximum:
        Math.Clamp(EyeHeight/Math.Tan(-elevation*Math.PI/180),.25,maximum);
    public static double StationOffset(int station,int perSide,double length)=>((station+.5)/perSide-.5)*length*.75;
}

/// <summary>Explicit foreground poses. The supplied picture is not assumed to be a regular sprite grid.</summary>
public sealed class CannonStationArtwork
{
    public int Version {get;set;}
    public int ImageWidth {get;set;}
    public int ImageHeight {get;set;}
    public ShipSpriteFrame? Frame {get;set;}
    public ShipSpriteFrame? LeftFrame {get;set;}
    public ShipSpriteFrame? RightFrame {get;set;}
    public bool IsValid(int width,int height)=>Version==1 && ImageWidth==width && ImageHeight==height &&
        width is >0 and <=16384 && height is >0 and <=16384 && Fits(Frame,width,height) &&
        (LeftFrame==null || Fits(LeftFrame,width,height)) && (RightFrame==null || Fits(RightFrame,width,height));
    private static bool Fits(ShipSpriteFrame? f,int width,int height)=>f!=null &&
        f.X>=0 && f.Y>=0 && f.Width>0 && f.Height>0 && f.X<=width-f.Width && f.Y<=height-f.Height;
    public ShipSpriteFrame Pose(double yaw)=>yaw< -12?LeftFrame??Frame!:yaw>12?RightFrame??Frame!:Frame!;
}


/// <summary>Server-computed flight in map tiles; configured range remains a strict upper bound.</summary>
public sealed record CannonFlight(double HorizontalSpeed,double VerticalSpeed,double Gravity,double Range)
{
    public double HeightAtDistance(double distance)
    {
        var t=Math.Max(0,distance)/HorizontalSpeed;
        return Math.Max(0,GunneryAim.EyeHeight+VerticalSpeed*t-Gravity*t*t/2);
    }
    public static CannonFlight Plan(double elevation,double speed,double range)
    {
        if(!double.IsFinite(elevation) || !double.IsFinite(speed) || !double.IsFinite(range) || speed<=0 || range<=0)
            throw new ArgumentOutOfRangeException(nameof(elevation));
        var angle=Math.Clamp(elevation,-75,35)*Math.PI/180;
        var horizontal=speed*Math.Cos(angle);var vertical=speed*Math.Sin(angle);
        var gravity=Math.Max(.05,speed*speed*.7/range);
        // Steeper shots may rise higher, but cannot extend the configured ammunition range.
        gravity=Math.Max(gravity,2*(GunneryAim.EyeHeight+range*Math.Tan(angle))*horizontal*horizontal/(range*range));
        var duration=(vertical+Math.Sqrt(vertical*vertical+2*gravity*GunneryAim.EyeHeight))/gravity;
        return new(horizontal,vertical,gravity,Math.Min(range,horizontal*duration));
    }
}
