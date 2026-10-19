using MessagePack;
namespace Intersect.Network.Packets.Pvp;

public enum PvpAction
{
    Browse, Queue, Leave
}
[MessagePackObject]
public partial class PvpRequestPacket : IntersectPacket
{
    [Key(0)]
    public PvpAction Action
    {
        get; set;
    }
    [Key(1)]
    public Guid ArenaId
    {
        get; set;
    }
}
[MessagePackObject]
public sealed class PvpRankEntry
{
    [Key(0)] public string Name { get; set; } = ""; [Key(1)]
    public long Points
    {
        get; set;
    }
    [Key(2)] public string Grade { get; set; } = "";
}
[MessagePackObject]
public sealed class PvpArenaEntry
{
    [Key(0)]
    public Guid Id
    {
        get; set;
    }
    [Key(1)] public string Name { get; set; } = ""; [Key(2)]
    public int Queued
    {
        get; set;
    }
    [Key(3)]
    public int Players
    {
        get; set;
    }
    [Key(4)]
    public int Minutes
    {
        get; set;
    }
    [Key(5)]
    public int MinimumLevel
    {
        get; set;
    }
}
[MessagePackObject]
public sealed class PvpChest
{
    [Key(0)]
    public float X
    {
        get; set;
    }
    [Key(1)]
    public float Y
    {
        get; set;
    }
    [Key(2)]
    public Guid Carrier
    {
        get; set;
    }
    [Key(3)]
    public bool Home
    {
        get; set;
    }
}
[MessagePackObject]
public sealed class CaptureChestState
{
    [Key(0)]
    public Guid Id
    {
        get; set;
    }
    [Key(1)]
    public Guid MapId
    {
        get; set;
    }
    [Key(2)]
    public int Team
    {
        get; set;
    }
    [Key(3)]
    public int RedScore
    {
        get; set;
    }
    [Key(4)]
    public int BlueScore
    {
        get; set;
    }
    [Key(5)]
    public int RemainingSeconds
    {
        get; set;
    }
    [Key(6)] public PvpChest[] Chests { get; set; } = []; [Key(7)]
    public Guid ChestItemId
    {
        get; set;
    }
    [Key(8)]
    public int RespawnSeconds
    {
        get; set;
    }
    [Key(9)]
    public int ScoreToWin
    {
        get; set;
    }
}
[MessagePackObject]
public partial class PvpStatePacket : IntersectPacket
{
    [Key(0)]
    public long Points
    {
        get; set;
    }
    [Key(1)] public string Grade { get; set; } = ""; [Key(2)]
    public PvpRankEntry[]? Rankings
    {
        get; set;
    }
    [Key(3)] public PvpArenaEntry[] Arenas { get; set; } = []; [Key(4)]
    public Guid QueuedArenaId
    {
        get; set;
    }
    [Key(5)]
    public CaptureChestState? Match
    {
        get; set;
    }
    [Key(6)] public string Message { get; set; } = "";
}
