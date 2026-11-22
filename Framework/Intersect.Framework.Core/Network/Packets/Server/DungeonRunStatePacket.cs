using MessagePack;

namespace Intersect.Network.Packets.Server;

public enum DungeonRunStatus
{
    None = 0,
    Active = 1,
    Completed = 2,
    Failed = 3,
    Abandoned = 4,
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
    [Key(9)] public Guid RunId { get; set; }
    [Key(10)] public string[] ModifierNames { get; set; } = [];
    [Key(11)] public int EnemyHealthPercent { get; set; } = 100;
    [Key(12)] public int EnemyDamagePercent { get; set; } = 100;
    [Key(13)] public int CompletionExperiencePercent { get; set; } = 100;
}
