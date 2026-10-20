using MessagePack;
namespace Intersect.Framework.Core.Naval;

[MessagePackObject]
public sealed class NavalPresentation
{
    [Key(0)] public string PortIcon {get;set;}="";
    [Key(1)] public string StarboardIcon {get;set;}="";
    [Key(2)] public string AnchorIcon {get;set;}="";
    [Key(3)] public string DisembarkIcon {get;set;}="";
    [Key(4)] public string CannonFireSound {get;set;}="";
    [Key(5)] public string HullImpactSound {get;set;}="";
    [Key(6)] public string WaterImpactSound {get;set;}="";
    [Key(7)] public string ObstacleImpactSound {get;set;}="";
    [Key(8)] public string SinkingSound {get;set;}="";
    [Key(9)] public string AnchorSound {get;set;}="";
    [Key(10)] public string LaunchSound {get;set;}="";
    [Key(11)] public string DisembarkSound {get;set;}="";
    [Key(12)] public string LevelUpSound {get;set;}="";
    [Key(13)] public string CannonFireGraphic {get;set;}="Ship_Canon_Fire_Animation.png";
    [Key(14)] public string CannonProjectileGraphic {get;set;}="Ship_Canon_Fire_impact.png";
    [Key(15)] public string WaterImpactGraphic {get;set;}="Ship_Canon_Fire_impact.png";
    [Key(16)] public string HullImpactGraphic {get;set;}="Ship_Canon_Fire_impact.png";
    [Key(17)] public int EffectFrameMilliseconds {get;set;}=80;
    [Key(18)] public double EffectPixelsPerTile {get;set;}=128;
    public static bool SafeAsset(string? value)=>value!=null && value.Length<=120 && !value.Any(c=>char.IsControl(c)||c is '/' or '\\' or ':') &&
        value is not "." and not ".." && Path.GetFileName(value)==value;
    public bool IsValid()=>GetType().GetProperties().Where(p=>p.PropertyType==typeof(string)).All(p=>SafeAsset((string?)p.GetValue(this))) &&
        EffectFrameMilliseconds is >=20 and <=1000 && double.IsFinite(EffectPixelsPerTile) && EffectPixelsPerTile is >=16 and <=1024;
    public string Icon(ShipAction action)=>action switch {ShipAction.PortCannons=>PortIcon,ShipAction.StarboardCannons=>StarboardIcon,
        ShipAction.Anchor=>AnchorIcon,ShipAction.LeaveHelm=>DisembarkIcon,_=>""};
}

public enum NavalEffectKind : byte { Fire,WaterImpact,HullImpact,ObstacleImpact,Sinking,Anchor,Launch,Disembark,LevelUp }
