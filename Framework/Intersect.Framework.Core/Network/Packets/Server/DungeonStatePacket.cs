using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public sealed class DungeonStatusEntry
{
    public DungeonStatusEntry()
    {
    }

    public DungeonStatusEntry(
        Guid dungeonId,
        bool available,
        string statusText,
        long nextChangeUnixMilliseconds
    )
    {
        DungeonId = dungeonId;
        Available = available;
        StatusText = statusText;
        NextChangeUnixMilliseconds = nextChangeUnixMilliseconds;
    }

    [Key(0)]
    public Guid DungeonId { get; set; }

    [Key(1)]
    public bool Available { get; set; }

    [Key(2)]
    public string StatusText { get; set; } = string.Empty;

    [Key(3)]
    public long NextChangeUnixMilliseconds { get; set; }
}

[MessagePackObject]
public partial class DungeonStatePacket : IntersectPacket
{
    public DungeonStatePacket()
    {
    }

    public DungeonStatePacket(
        string configurationJson,
        DungeonStatusEntry[] statuses,
        bool openWindow,
        long serverTimeUnixMilliseconds
    )
    {
        ConfigurationJson = configurationJson;
        Statuses = statuses;
        OpenWindow = openWindow;
        ServerTimeUnixMilliseconds = serverTimeUnixMilliseconds;
    }

    [Key(0)]
    public string ConfigurationJson { get; set; } = string.Empty;

    [Key(1)]
    public DungeonStatusEntry[] Statuses { get; set; } = [];

    [Key(2)]
    public bool OpenWindow { get; set; }

    [Key(3)]
    public long ServerTimeUnixMilliseconds { get; set; }
}
