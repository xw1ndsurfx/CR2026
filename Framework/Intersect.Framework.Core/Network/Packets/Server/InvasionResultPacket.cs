using MessagePack;

namespace Intersect.Network.Packets.WorldEvents;

[MessagePackObject]
public partial class InvasionResultPacket : IntersectPacket
{
    public InvasionResultPacket() { }

    [Key(0)] public Guid InvasionId { get; set; }
    [Key(1)] public string Name { get; set; } = string.Empty;
    [Key(2)] public bool Victory { get; set; }
    [Key(3)] public long ExperienceAwarded { get; set; }
    [Key(4)] public int WavesCompleted { get; set; }
    [Key(5)] public int WaveCount { get; set; }
    [Key(6)] public int ObjectiveHealthRemaining { get; set; }
    [Key(7)] public int ParticipantCount { get; set; }
}
