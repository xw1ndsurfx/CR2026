using MessagePack;
using Intersect.Framework.Core.RoyalContracts;

namespace Intersect.Network.Packets.Client;

[MessagePackObject]
public partial class RoyalContractRequestPacket : IntersectPacket
{
    [Key(0)] public RoyalContractAction Action { get; set; }
    [Key(1)] public Guid ContractId { get; set; }
}
