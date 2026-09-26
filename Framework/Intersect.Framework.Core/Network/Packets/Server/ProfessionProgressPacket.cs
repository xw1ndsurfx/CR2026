using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public partial class ProfessionProgressPacket : IntersectPacket
{
    [Key(0)]
    public Guid ProfessionId { get; set; }

    [Key(1)]
    public string ProfessionName { get; set; } = string.Empty;

    [Key(2)]
    public int Level { get; set; }

    [Key(3)]
    public int MaximumLevel { get; set; }

    [Key(4)]
    public long ExperienceGained { get; set; }

    [Key(5)]
    public long ExperienceIntoLevel { get; set; }

    [Key(6)]
    public long ExperienceRequiredForLevel { get; set; }

    [Key(7)]
    public long ExperienceToNextLevel { get; set; }

    [Key(8)]
    public int Percentage { get; set; }

    [Key(9)]
    public bool LeveledUp { get; set; }

    [Key(10)]
    public bool MaximumLevelReached { get; set; }
}
