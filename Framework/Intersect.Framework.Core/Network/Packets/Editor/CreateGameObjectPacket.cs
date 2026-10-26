using Intersect.Enums;
using MessagePack;

namespace Intersect.Network.Packets.Editor;

[MessagePackObject]
public partial class CreateGameObjectPacket : EditorPacket
{
    //Parameterless Constructor for MessagePack
    public CreateGameObjectPacket()
    {
    }

    public CreateGameObjectPacket(GameObjectType type, Guid requestedId = default)
    {
        Type = type;
        RequestedId = requestedId;
    }

    [Key(0)]
    public GameObjectType Type { get; set; }

    // Optional identity for editor workflows that must await this exact creation.
    // Empty preserves the historical server-assigned identity.
    [Key(1)]
    public Guid RequestedId { get; set; }

}
