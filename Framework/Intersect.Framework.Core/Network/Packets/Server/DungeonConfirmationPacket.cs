using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public partial class DungeonConfirmationPacket : IntersectPacket
{
    public DungeonConfirmationPacket()
    {
    }

    public DungeonConfirmationPacket(
        Guid eventId,
        Guid dungeonId,
        string configurationJson
    )
    {
        EventId = eventId;
        DungeonId = dungeonId;
        ConfigurationJson = configurationJson;
    }

    [Key(0)]
    public Guid EventId { get; set; }

    [Key(1)]
    public Guid DungeonId { get; set; }

    [Key(2)]
    public string ConfigurationJson { get; set; } = string.Empty;
}
