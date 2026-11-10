using Intersect.Framework.Core.Naval;
using MessagePack;
namespace Intersect.Network.Packets.Server;
[MessagePackObject]
public sealed class NavalMonsterSnapshot
{
    [Key(0)] public Guid Id {get;set;}
    [Key(1)] public Guid ShipId {get;set;}
    [Key(2)] public Guid MapId {get;set;}
    [Key(3)] public string Name {get;set;}="";
    [Key(4)] public string Graphics {get;set;}="";
    [Key(5)] public double X {get;set;}
    [Key(6)] public double Y {get;set;}
    [Key(7)] public NavalMonsterPhase Phase {get;set;}
    [Key(8)] public int AgeMilliseconds {get;set;}
    [Key(9)] public int LiveTentacles {get;set;}
    [Key(10)] public int TotalTentacles {get;set;}
    [Key(11)] public int RemainingSeconds {get;set;}
    [Key(12)] public Guid BossAnimationId {get;set;}
    [Key(13)] public int BossAnimationOffsetY {get;set;}
    [Key(14)] public double SizeTiles {get;set;}=12;
    [Key(15)] public NavalMonsterKind Kind {get;set;}
    [Key(16)] public int HullHealth {get;set;}=100;
}
