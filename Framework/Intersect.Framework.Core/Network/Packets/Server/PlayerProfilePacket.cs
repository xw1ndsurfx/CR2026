using Intersect.Framework.Core.GameObjects.Items;
using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public partial class PlayerProfessionProfilePacket
{
    [Key(0)]
    public Guid ProfessionId { get; set; }

    [Key(1)]
    public string Name { get; set; } = string.Empty;

    [Key(2)]
    public int Level { get; set; }

    [Key(3)]
    public int MaximumLevel { get; set; }

    [Key(4)]
    public long Experience { get; set; }

    [Key(5)]
    public long ExperienceToNextLevel { get; set; }
}

[MessagePackObject]
public partial class PlayerEquipmentProfilePacket
{
    [Key(0)]
    public int SlotIndex { get; set; }

    [Key(1)]
    public string SlotName { get; set; } = string.Empty;

    [Key(2)]
    public Guid ItemId { get; set; }

    [Key(3)]
    public string ItemName { get; set; } = string.Empty;

    [Key(4)]
    public ItemProperties? Properties { get; set; }
}

[MessagePackObject]
public partial class PlayerProfilePacket : IntersectPacket
{
    [Key(0)]
    public bool Found { get; set; }

    [Key(1)]
    public string Message { get; set; } = string.Empty;

    [Key(2)]
    public bool OpenWindow { get; set; }

    [Key(3)]
    public Guid PlayerId { get; set; }

    [Key(4)]
    public string Name { get; set; } = string.Empty;

    [Key(5)]
    public int Level { get; set; }

    [Key(6)]
    public string ClassName { get; set; } = string.Empty;

    [Key(7)]
    public string GuildName { get; set; } = string.Empty;

    [Key(8)]
    public string MapName { get; set; } = string.Empty;

    [Key(9)]
    public long Experience { get; set; }

    [Key(10)]
    public long ExperienceToNextLevel { get; set; }

    [Key(11)]
    public int[] Stats { get; set; } = [];

    [Key(12)]
    public PlayerEquipmentProfilePacket[] Equipment { get; set; } = [];

    [Key(13)]
    public PlayerProfessionProfilePacket[] Professions { get; set; } = [];

    [Key(14)]
    public bool IsSelf { get; set; }
}
