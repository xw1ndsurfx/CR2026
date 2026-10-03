using MessagePack;

namespace Intersect.Network.Packets.Editor;

[MessagePackObject]
public partial class RequestDungeonConfigurationPacket : EditorPacket
{
    public RequestDungeonConfigurationPacket()
    {
    }

    public RequestDungeonConfigurationPacket(bool openEditor)
    {
        OpenEditor = openEditor;
    }

    [Key(0)]
    public bool OpenEditor { get; set; }
}
