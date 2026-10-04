using MessagePack;

namespace Intersect.Network.Packets.Server;

public enum DungeonRunStatus
{
    None = 0,
    Active = 1,
    Completed = 2,
    Failed = 3,
}

[MessagePackObject]
public partial class DungeonRunStatePacket : IntersectPacket
{
    public DungeonRunStatePacket()
    {
    }

    public DungeonRunStatePacket(
        Guid dungeonId,
        string dungeonName,
        string dungeonRank,
        DungeonRunStatus status,
        long endTimeUnixMilliseconds,
        string message,
        int livesRemaining = 0,
        int maxLives = 0,
        int monstersRemaining = -1
    )
    {
        DungeonId = dungeonId;
        DungeonName = dungeonName;
        DungeonRank = dungeonRank;
        Status = status;
        EndTimeUnixMilliseconds = endTimeUnixMilliseconds;
        Message = message;
        LivesRemaining = livesRemaining;
        MaxLives = maxLives;
        MonstersRemaining = monstersRemaining;
    }

    [Key(0)] public Guid DungeonId { get; set; }
    [Key(1)] public string DungeonName { get; set; } = string.Empty;
    [Key(2)] public string DungeonRank { get; set; } = string.Empty;
    [Key(3)] public DungeonRunStatus Status { get; set; }
    [Key(4)] public long EndTimeUnixMilliseconds { get; set; }
    [Key(5)] public string Message { get; set; } = string.Empty;
    [Key(6)] public int LivesRemaining { get; set; }
    [Key(7)] public int MaxLives { get; set; }
    [Key(8)] public int MonstersRemaining { get; set; } = -1;
}
