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
public sealed class DungeonPodiumPlayerEntry
{
    public DungeonPodiumPlayerEntry()
    {
    }

    public DungeonPodiumPlayerEntry(string playerName, long completions, long bestClearTimeMilliseconds)
    {
        PlayerName = playerName;
        Completions = completions;
        BestClearTimeMilliseconds = bestClearTimeMilliseconds;
    }

    [Key(0)]
    public string PlayerName { get; set; } = string.Empty;

    [Key(1)]
    public long Completions { get; set; }

    [Key(2)]
    public long BestClearTimeMilliseconds { get; set; }
}

[MessagePackObject]
public sealed class DungeonPodiumEntry
{
    public DungeonPodiumEntry()
    {
    }

    public DungeonPodiumEntry(
        Guid dungeonId,
        DungeonPodiumPlayerEntry[] topClears,
        DungeonPodiumPlayerEntry[] fastestClears
    )
    {
        DungeonId = dungeonId;
        TopClears = topClears;
        FastestClears = fastestClears;
    }

    [Key(0)]
    public Guid DungeonId { get; set; }

    [Key(1)]
    public DungeonPodiumPlayerEntry[] TopClears { get; set; } = [];

    [Key(2)]
    public DungeonPodiumPlayerEntry[] FastestClears { get; set; } = [];
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
        DungeonPlayerStatEntry[]? playerStats = null,
        DungeonPodiumEntry[]? podiums = null
    )
    {
        ConfigurationJson = configurationJson;
        Statuses = statuses;
        OpenWindow = openWindow;
        ServerTimeUnixMilliseconds = serverTimeUnixMilliseconds;
        PlayerStats = playerStats ?? [];
        Podiums = podiums;
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

    // Null means not refreshed (background state update); an empty array means
    // a valid but empty snapshot. Only explicit window opens fetch podium data.
    [Key(5)]
    public DungeonPodiumEntry[]? Podiums { get; set; }
}
