using MessagePack;

namespace Intersect.Network.Packets.Editor;

[MessagePackObject(AllowPrivate = true)]
public partial class RequestRewardConfigurationPacket : EditorPacket
{
}
