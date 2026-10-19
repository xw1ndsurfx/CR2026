using MessagePack;
namespace Intersect.Network.Packets.Editor;

[MessagePackObject(AllowPrivate = true)]
public partial class PrepareNavalMapPacket : EditorPacket
{
    [Key(0)] public Guid MapId { get; set; }
}
