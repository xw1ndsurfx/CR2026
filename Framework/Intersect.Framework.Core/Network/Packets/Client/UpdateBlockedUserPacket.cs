using MessagePack;

namespace Intersect.Network.Packets.Client;

/// <summary>Block by character name, or unblock by stable character id.</summary>
[MessagePackObject]
public partial class UpdateBlockedUserPacket : IntersectPacket
{
    public UpdateBlockedUserPacket() { }

    public UpdateBlockedUserPacket(bool blocked, string characterName, Guid targetId = default)
    {
        Blocked = blocked;
        CharacterName = characterName ?? string.Empty;
        TargetId = targetId;
    }

    [Key(0)]
    public bool Blocked { get; set; }

    [Key(1)]
    public string CharacterName { get; set; } = string.Empty;

    [Key(2)]
    public Guid TargetId { get; set; }
}
