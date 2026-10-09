using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public sealed class PetAbilityEntry
{
    [Key(0)]
    public string Name { get; set; } = string.Empty;

    [Key(1)]
    public string Icon { get; set; } = string.Empty;

    [Key(2)]
    public int RequiredLevel { get; set; }
}

[MessagePackObject]
public sealed class PetProfileEntry
{
    [Key(0)]
    public Guid PetId { get; set; }

    [Key(1)]
    public string Name { get; set; } = string.Empty;

    [Key(2)]
    public string Sprite { get; set; } = string.Empty;

    [Key(3)]
    public int Level { get; set; }

    [Key(4)]
    public int MaximumLevel { get; set; }

    [Key(5)]
    public long Experience { get; set; }

    [Key(6)]
    public long ExperienceToNextLevel { get; set; }

    [Key(7)]
    public bool IsActive { get; set; }

    [Key(8)]
    public bool AutoLoot { get; set; }

    [Key(9)]
    public int LootRadius { get; set; }

    [Key(10)]
    public int[] Stats { get; set; } = [];

    [Key(11)]
    public long Health { get; set; }

    [Key(12)]
    public long MaximumHealth { get; set; }

    [Key(13)]
    public long Mana { get; set; }

    [Key(14)]
    public long MaximumMana { get; set; }

    [Key(15)]
    public PetAbilityEntry[] Abilities { get; set; } = [];

    [Key(16)]
    public string SpeciesName { get; set; } = string.Empty;
}

[MessagePackObject]
public partial class PetStatePacket : IntersectPacket
{
    [Key(0)]
    public PetProfileEntry[] Pets { get; set; } = [];

    [Key(1)]
    public Guid ActivePetId { get; set; }

    [Key(2)]
    public bool OpenWindow { get; set; }
}
