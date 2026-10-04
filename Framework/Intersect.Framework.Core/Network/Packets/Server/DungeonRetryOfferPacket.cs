using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public partial class DungeonRetryOfferPacket : IntersectPacket
{
    public DungeonRetryOfferPacket()
    {
    }

    public DungeonRetryOfferPacket(Guid retryId, Guid dungeonId, string configurationJson)
    {
        RetryId = retryId;
        DungeonId = dungeonId;
        ConfigurationJson = configurationJson;
    }

    [Key(0)]
    public Guid RetryId { get; set; }

    [Key(1)]
    public Guid DungeonId { get; set; }

    [Key(2)]
    public string ConfigurationJson { get; set; } = string.Empty;
}
