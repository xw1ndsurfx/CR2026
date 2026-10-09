using MessagePack;

namespace Intersect.Network.Packets.Client;

public enum PetActionKind : byte
{
    Summon = 1,
    Dismiss = 2,
    ToggleAutoLoot = 3,
}

[MessagePackObject]
public partial class PetActionPacket : IntersectPacket
{
    public PetActionPacket()
    {
    }

    public PetActionPacket(PetActionKind action, Guid petId)
    {
        Action = action;
        PetId = petId;
    }

    [Key(0)]
    public PetActionKind Action { get; set; }

    [Key(1)]
    public Guid PetId { get; set; }
}
