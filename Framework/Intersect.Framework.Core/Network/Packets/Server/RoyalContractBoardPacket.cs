using MessagePack;
using Intersect.Framework.Core.RoyalContracts;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public partial class RoyalContractBoardPacket : IntersectPacket
{
    [Key(0)] public string StateJson { get; set; } = "";
    [Key(1)] public bool OpenWindow { get; set; }
}
