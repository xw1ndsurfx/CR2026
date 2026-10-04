using MessagePack;

namespace Intersect.Network.Packets.Editor;

[MessagePackObject]
public partial class RequestQuestShopConfigurationPacket : IntersectPacket
{
    public RequestQuestShopConfigurationPacket() { }
    public RequestQuestShopConfigurationPacket(bool openEditor) => OpenEditor = openEditor;

    [Key(0)]
    public bool OpenEditor { get; set; }
}
