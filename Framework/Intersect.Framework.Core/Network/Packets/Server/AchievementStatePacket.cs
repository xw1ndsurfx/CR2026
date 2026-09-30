using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public sealed class AchievementProgressEntry
{
    public AchievementProgressEntry()
    {
    }

    public AchievementProgressEntry(
        Guid achievementId,
        long progress,
        bool completed,
        bool claimed,
        long completedAtUnixSeconds
    )
    {
        AchievementId = achievementId;
        Progress = progress;
        Completed = completed;
        Claimed = claimed;
        CompletedAtUnixSeconds = completedAtUnixSeconds;
    }

    [Key(0)]
    public Guid AchievementId { get; set; }

    [Key(1)]
    public long Progress { get; set; }

    [Key(2)]
    public bool Completed { get; set; }

    [Key(3)]
    public bool Claimed { get; set; }

    [Key(4)]
    public long CompletedAtUnixSeconds { get; set; }
}

[MessagePackObject]
public partial class AchievementStatePacket : IntersectPacket
{
    public AchievementStatePacket()
    {
    }

    public AchievementStatePacket(
        string configurationJson,
        AchievementProgressEntry[] progress,
        bool openWindow = false
    )
    {
        ConfigurationJson = configurationJson;
        Progress = progress;
        OpenWindow = openWindow;
    }

    [Key(0)]
    public string ConfigurationJson { get; set; } = string.Empty;

    [Key(1)]
    public AchievementProgressEntry[] Progress { get; set; } = [];

    [Key(2)]
    public bool OpenWindow { get; set; }
}
