using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public sealed class BlockedUserEntry
{
    public BlockedUserEntry() { }
    public BlockedUserEntry(Guid id, string name)
    {
        Id = id;
        Name = name ?? string.Empty;
    }

    [Key(0)]
    public Guid Id { get; set; }

    [Key(1)]
    public string Name { get; set; } = string.Empty;
}

[MessagePackObject]
public partial class BlockedUsersPacket : IntersectPacket
{
    public BlockedUsersPacket() { }
    public BlockedUsersPacket(BlockedUserEntry[] users) => Users = users ?? [];

    [Key(0)]
    public BlockedUserEntry[] Users { get; set; } = [];
}
