using MessagePack;

namespace Intersect.Network.Packets.Editor;

[MessagePackObject]
public partial class RequestProfessionConfigurationPacket : EditorPacket
{
    public RequestProfessionConfigurationPacket() { }
    public RequestProfessionConfigurationPacket(bool openEditor) => OpenEditor = openEditor;
    [Key(0)] public bool OpenEditor { get; set; }
}
