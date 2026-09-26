using MessagePack;

namespace Intersect.Network.Packets.WorldEvents;

[MessagePackObject(AllowPrivate = true)]
public partial class StartInvasionNowPacket : EditorPacket
{
    public StartInvasionNowPacket() { }

    public StartInvasionNowPacket(Guid invasionId) => InvasionId = invasionId;

    [Key(0)]
    public Guid InvasionId { get; set; }
}
