using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public partial class InvasionStatusPacket : IntersectPacket
{
    public InvasionStatusPacket() { }

    [Key(0)] public bool Active { get; set; }
    [Key(1)] public Guid InvasionId { get; set; }
    [Key(2)] public string Name { get; set; } = string.Empty;
    [Key(3)] public int Wave { get; set; }
    [Key(4)] public int WaveCount { get; set; }
    [Key(5)] public int ObjectiveHealth { get; set; }
    [Key(6)] public int ObjectiveMaxHealth { get; set; }
    [Key(7)] public bool BossWave { get; set; }
    [Key(8)] public string Message { get; set; } = string.Empty;
}
