using MessagePack;
using Intersect.Framework.Core.Naval;
namespace Intersect.Network.Packets.Client;
[MessagePackObject(AllowPrivate = true)]
public partial class CannonStationPacket : IntersectPacket
{
    [Key(0)] public Guid ShipId {get;set;}
    [Key(1)] public Guid LeaseId {get;set;}
    [Key(2)] public CannonStationAction Action {get;set;}
    [Key(3)] public bool Starboard {get;set;}
    [Key(4)] public CannonAmmoKind Ammunition {get;set;}
    [Key(5)] public double Yaw {get;set;}
    [Key(6)] public double Elevation {get;set;}
}
