using MessagePack;
using Intersect.Framework.Core.RoyalContracts;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public partial class RoyalContractsConfigurationPacket : IntersectPacket
{
    [Key(0)] public bool OpenEditor { get; set; }
    [Key(1)] public string ConfigurationJson { get; set; } = "";
    [Key(2)] public string Message { get; set; } = "";
}
