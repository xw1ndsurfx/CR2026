using MessagePack;
using Intersect.Framework.Core.Fishing;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public sealed partial class FishingWorldPacket : IntersectPacket
{
    [Key(0)] public Guid EntityId { get; set; }
    [Key(1)] public Guid ToolItemId { get; set; }
    [Key(2)] public FishingWorldAction Action { get; set; }
    [Key(3)] public string ProfileJson { get; set; } = "";
    [Key(4)] public long Sequence { get; set; }
}
