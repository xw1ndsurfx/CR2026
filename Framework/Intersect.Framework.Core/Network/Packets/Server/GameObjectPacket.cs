using Intersect.Enums;
using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public partial class GameObjectPacket : IntersectPacket
{
    //Parameterless Constructor for MessagePack
    public GameObjectPacket()
    {

    }

    public GameObjectPacket(Guid id, GameObjectType type, string data, bool deleted, bool another)
    {
        Id = id;
        Type = type;
        Data = data;
        Deleted = deleted;
        AnotherFollowing = another;
    }

    [Key(0)]
    public Guid Id { get; set; }

    [Key(1)]
    public GameObjectType Type { get; set; }

    [Key(2)]
    public bool AnotherFollowing { get; set; }

    [Key(3)]
    public bool Deleted { get; set; }

    [Key(4)]
    public string Data { get; set; }

    // A failed requested-ID creation is delivered only to its requesting editor.
    // This is a localization key, never authored content or an object update.
    [Key(5)]
    public string CreationError { get; set; } = "";

    // Only the requesting editor receives this receipt. Ordinary broadcasts must
    // never be mistaken for ownership of a locally requested new identity.
    [Key(6)]
    public bool CreationSucceeded { get; set; }

}
