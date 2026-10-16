using MessagePack;
namespace Intersect.Network.Packets.ChatEmotes;
[MessagePackObject] public partial class ChatEmoteRequestPacket : IntersectPacket
{
    // Empty requests the menu; nonempty requests execution of an explicitly permitted event.
    [Key(0)] public Guid EventId { get; set; }
}
[MessagePackObject] public sealed class ChatEmoteEntry
{
    [Key(0)] public Guid Id { get; set; }
    [Key(1)] public string Name { get; set; } = "";
}
[MessagePackObject] public partial class ChatEmoteMenuPacket : IntersectPacket
{
    [Key(0)] public ChatEmoteEntry[] Entries { get; set; } = [];
}
