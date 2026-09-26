using MessagePack;

namespace Intersect.Network.Packets.WorldEvents;

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

    [Key(9)] public long StartedAtUnixMilliseconds { get; set; }
    [Key(10)] public string Music { get; set; } = string.Empty;
    [Key(11)] public int NightBrightness { get; set; } = 20;
    [Key(12)] public int OverlayAlpha { get; set; } = 120;
    [Key(13)] public int OverlayRed { get; set; } = 50;
    [Key(14)] public int OverlayGreen { get; set; } = 255;
    [Key(15)] public int OverlayBlue { get; set; } = 50;
    [Key(16)] public string Fog { get; set; } = "brume2.png";
    [Key(17)] public int FogAlpha { get; set; } = 150;
    [Key(18)] public int FogXSpeed { get; set; } = 3;
    [Key(19)] public int FogYSpeed { get; set; } = -3;
    [Key(20)] public bool EnvironmentOutdoorsOnly { get; set; } = true;
}
