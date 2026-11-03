using MessagePack;
using Intersect.Framework.Core.RoyalContracts;

namespace Intersect.Network.Packets.Editor;

[MessagePackObject]
public partial class RoyalContractsConfigurationRequestPacket : IntersectPacket
{
    [Key(0)] public bool OpenEditor { get; set; }
    [Key(1)] public bool Save { get; set; }
    [Key(2)] public string ConfigurationJson { get; set; } = "";
}
