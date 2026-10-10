using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public partial class DungeonStatusEntry
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
public partial class DungeonPlayerStatEntry
{
    public DungeonPlayerStatEntry()
    {
    }

    public DungeonPlayerStatEntry(
        Guid dungeonId,
        long attempts,
        long completions,
        long failures,
        long deaths,
        long bestClearTimeMilliseconds,
        long lastCompletedUnixMilliseconds
    )
    {
        DungeonId = dungeonId;
        Attempts = attempts;
        Completions = completions;
        Failures = failures;
        Deaths = deaths;
        BestClearTimeMilliseconds = bestClearTimeMilliseconds;
        LastCompletedUnixMilliseconds = lastCompletedUnixMilliseconds;
    }

    [Key(0)]
    public Guid DungeonId { get; set; }

    [Key(1)]
    public long Attempts { get; set; }

    [Key(2)]
    public long Completions { get; set; }

    [Key(3)]
    public long Failures { get; set; }

    [Key(4)]
    public long Deaths { get; set; }

    [Key(5)]
    public long BestClearTimeMilliseconds { get; set; }

    [Key(6)]
    public long LastCompletedUnixMilliseconds { get; set; }
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
        long serverTimeUnixMilliseconds,
        DungeonPlayerStatEntry[]? playerStats = null
    )
    {
        ConfigurationJson = configurationJson;
        Statuses = statuses;
        OpenWindow = openWindow;
        ServerTimeUnixMilliseconds = serverTimeUnixMilliseconds;
        PlayerStats = playerStats ?? [];
    }

    [Key(0)]
    public string ConfigurationJson { get; set; } = string.Empty;

    [Key(1)]
    public DungeonStatusEntry[] Statuses { get; set; } = [];

    [Key(2)]
    public bool OpenWindow { get; set; }

    [Key(3)]
    public long ServerTimeUnixMilliseconds { get; set; }

    [Key(4)]
    public DungeonPlayerStatEntry[] PlayerStats { get; set; } = [];
}
