using MessagePack;
using Intersect.Framework.Core.Naval;
namespace Intersect.Network.Packets.Server;

[MessagePackObject(AllowPrivate = true)]
public partial class NavalSnapshotPacket : IntersectPacket
{
    [Key(7)] public Guid CombatTargetId {get;set;}
    [Key(0)] public ShipSnapshot[] Ships { get; set; } = [];
    [Key(2)] public NavalEffectSnapshot[] Effects {get;set;}=[];
    [Key(3)] public NavalPresentation Presentation {get;set;}=new();
    [Key(4)] public int SailingLevel {get;set;}=1;
    [Key(5)] public long SailingExperience {get;set;}
    [Key(6)] public long SailingRequired {get;set;}
    [Key(1)] public CannonballSnapshot[] Cannonballs { get; set; } = [];
}

[MessagePackObject]
public sealed class ShipSnapshot
{
    [Key(0)] public Guid Id { get; set; }
    [Key(1)] public Guid CaptainId { get; set; }
    [Key(2)] public Guid MapId { get; set; }
    [Key(3)] public double X { get; set; }
    [Key(4)] public double Y { get; set; }
    [Key(5)] public double Heading { get; set; }
    [Key(6)] public double Speed { get; set; }
    [Key(7)] public string Graphics { get; set; } = "";
    [Key(8)] public int Frames { get; set; }
    [Key(9)] public int FrameMilliseconds { get; set; }
    [Key(10)] public double Width { get; set; }
    [Key(11)] public double Length { get; set; }
    [Key(12)] public int Health { get; set; } = 100;
    [Key(13)] public int PortReloadMilliseconds { get; set; }
    [Key(14)] public int StarboardReloadMilliseconds { get; set; }
    [Key(15)] public bool CannonsEnabled { get; set; }
    [Key(16)] public bool Sinking { get; set; }
    [Key(17)] public bool PortFlash { get; set; }
    [Key(18)] public bool StarboardFlash { get; set; }
    [Key(19)] public bool Anchored { get; set; }
    [Key(24)] public NavalCargoParcel[] Cargo {get;set;}=[];
    [Key(25)] public int CargoCapacity {get;set;}
    [Key(21)] public bool Enemy {get;set;}
    [Key(22)] public int Level {get;set;}=1;
    [Key(23)] public int MaximumHealth {get;set;}=100;
    [Key(20)] public string Name {get;set;}="";
}

[MessagePackObject]
public sealed class CannonballSnapshot
{
    [Key(0)] public Guid Id { get; set; }
    [Key(1)] public Guid MapId { get; set; }
    [Key(2)] public double X { get; set; }
    [Key(3)] public double Y { get; set; }
    [Key(4)] public double Heading {get;set;}
    [Key(5)] public int AgeMilliseconds {get;set;}
}

[MessagePackObject]
public sealed class NavalEffectSnapshot
{
    [Key(0)] public Guid Id {get;set;}
    [Key(1)] public Guid MapId {get;set;}
    [Key(2)] public double X {get;set;}
    [Key(3)] public double Y {get;set;}
    [Key(4)] public double Heading {get;set;}
    [Key(5)] public NavalEffectKind Kind {get;set;}
    [Key(6)] public int AgeMilliseconds {get;set;}
    [Key(7)] public bool PlaySound {get;set;}=true;
}
