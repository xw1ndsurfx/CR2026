using MessagePack;

namespace Intersect.Network.Packets.WorldEvents;

[MessagePackObject(AllowPrivate = true)]
public partial class StartWorldBossNowPacket : EditorPacket
{
    public StartWorldBossNowPacket() { }

    public StartWorldBossNowPacket(Guid bossId) => BossId = bossId;

    [Key(0)]
    public Guid BossId { get; set; }
}
