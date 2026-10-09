using MessagePack;

namespace Intersect.Network.Packets.Client;

public enum PetActionKind : byte
{
    Summon = 1,
    Dismiss = 2,
    ToggleAutoLoot = 3,
    Rename = 4,
}

[MessagePackObject]
public partial class PetActionPacket : IntersectPacket
{
    public PetActionPacket()
    {
    }

    public PetActionPacket(PetActionKind action, Guid petId, string name = "")
    {
        Action = action;
        PetId = petId;
        Name = name ?? string.Empty;
    }

    [Key(0)]
    public PetActionKind Action { get; set; }

    [Key(1)]
    public Guid PetId { get; set; }

    [Key(2)]
    public string Name { get; set; } = string.Empty;
}
