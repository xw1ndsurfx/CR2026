using MessagePack;

namespace Intersect.Network.Packets.Client;

[MessagePackObject]
public partial class DungeonRetryResponsePacket : IntersectPacket
{
    public DungeonRetryResponsePacket()
    {
    }

    public DungeonRetryResponsePacket(Guid retryId, bool accept)
    {
        RetryId = retryId;
        Accept = accept;
    }

    [Key(0)]
    public Guid RetryId { get; set; }

    [Key(1)]
    public bool Accept { get; set; }
}
