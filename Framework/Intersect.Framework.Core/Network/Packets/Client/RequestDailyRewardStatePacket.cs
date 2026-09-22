using MessagePack;

namespace Intersect.Network.Packets.Client;

[MessagePackObject(AllowPrivate = true)]
public partial class RequestDailyRewardStatePacket : IntersectPacket
{
    public RequestDailyRewardStatePacket() { }
    public RequestDailyRewardStatePacket(bool autoOpen) => AutoOpen = autoOpen;

    [Key(0)]
    public bool AutoOpen { get; set; }
}
