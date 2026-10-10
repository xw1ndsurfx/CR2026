using MessagePack;

namespace Intersect.Network.Packets.WorldEvents;

[MessagePackObject(AllowPrivate = true)]
public partial class RequestWorldBossConfigurationPacket : EditorPacket
{
}
