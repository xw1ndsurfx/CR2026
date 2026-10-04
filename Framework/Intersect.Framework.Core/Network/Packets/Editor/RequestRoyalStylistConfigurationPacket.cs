using MessagePack;

namespace Intersect.Network.Packets.Editor;

[MessagePackObject]
public partial class RequestRoyalStylistConfigurationPacket : IntersectPacket
{
    public RequestRoyalStylistConfigurationPacket()
    {
    }

    public RequestRoyalStylistConfigurationPacket(bool openEditor) =>
        OpenEditor = openEditor;

    [Key(0)]
    public bool OpenEditor { get; set; }
}
