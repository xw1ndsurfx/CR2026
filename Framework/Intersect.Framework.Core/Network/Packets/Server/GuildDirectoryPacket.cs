using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public sealed class GuildDirectoryEntry
{
    public GuildDirectoryEntry() { }
    public GuildDirectoryEntry(Guid id, string name, int memberCount)
    {
        Id = id;
        Name = name ?? string.Empty;
        MemberCount = memberCount;
    }

    [Key(0)]
    public Guid Id { get; set; }

    [Key(1)]
    public string Name { get; set; } = string.Empty;

    [Key(2)]
    public int MemberCount { get; set; }
}

[MessagePackObject]
public partial class GuildDirectoryPacket : IntersectPacket
{
    public GuildDirectoryPacket() { }
    public GuildDirectoryPacket(string search, int page, int total, GuildDirectoryEntry[] entries)
    {
        Search = search ?? string.Empty;
        Page = page;
        Total = total;
        Entries = entries ?? [];
    }

    [Key(0)]
    public string Search { get; set; } = string.Empty;

    [Key(1)]
    public int Page { get; set; }

    [Key(2)]
    public int Total { get; set; }

    [Key(3)]
    public GuildDirectoryEntry[] Entries { get; set; } = [];
}
