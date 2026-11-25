using Intersect.Framework.Core.EquipmentLoadouts;
using MessagePack;
namespace Intersect.Network.Packets.EquipmentLoadouts;

[MessagePackObject]
public partial class EquipmentLoadoutRequestPacket : IntersectPacket
{
    [Key(0)] public EquipmentLoadoutAction Action { get; set; }
    [Key(1)] public int Slot { get; set; }
    [Key(2)] public string Name { get; set; } = "";
    [Key(3)] public long Revision { get; set; }
    [Key(4)] public Guid RequestId { get; set; }
}

[MessagePackObject]
public partial class EquipmentLoadoutStatePacket : IntersectPacket
{
    [Key(0)] public string Json { get; set; } = "";
    [Key(1)] public bool Open { get; set; }
    [Key(2)] public Guid RequestId { get; set; }
}

[MessagePackObject]
public partial class UseEquipmentSpellPacket : IntersectPacket
{
    [Key(0)] public Guid TargetId { get; set; }
}

[MessagePackObject]
public partial class EquipmentFeedbackPacket : IntersectPacket
{
    [Key(0)] public string Cue { get; set; } = "";
}
