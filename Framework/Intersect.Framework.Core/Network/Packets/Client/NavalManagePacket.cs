using MessagePack;
using Intersect.Framework.Core.Naval;
namespace Intersect.Network.Packets.Client;
[MessagePackObject]
public partial class NavalManagePacket : IntersectPacket
{
    [Key(0)] public NavalManageAction Action {get;set;}
    [Key(1)] public CannonAmmoKind Ammo {get;set;}
    [Key(2)] public int Quantity {get;set;}=1;
    [Key(3)] public NavalSkillKind Skill {get;set;}
    [Key(4)] public NavalRepairKind Repair {get;set;}
    [Key(5)] public Guid TargetId {get;set;}
    [Key(6)] public Guid Token {get;set;}
    [Key(7)] public int Value {get;set;}
}
