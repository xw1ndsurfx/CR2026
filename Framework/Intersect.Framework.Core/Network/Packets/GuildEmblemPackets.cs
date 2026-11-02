using MessagePack;
namespace Intersect.Network.Packets.Guilds;
public enum GuildEmblemAction { Read, Manage, Submit, Approve, Reject, Remove }
[MessagePackObject]
public partial class GuildEmblemRequestPacket : IntersectPacket
{
    [Key(0)] public GuildEmblemAction Action { get; set; }
    [Key(1)] public string GuildName { get; set; } = "";
    [Key(2)] public Guid GuildId { get; set; }
    [Key(3)] public Guid Revision { get; set; }
    [Key(4)] public byte[] Image { get; set; } = [];
    [Key(5)] public int Page { get; set; }
}
[MessagePackObject]
public partial class GuildEmblemEntry
{
    [Key(0)] public Guid GuildId { get; set; }
    [Key(1)] public string GuildName { get; set; } = "";
    [Key(2)] public Guid Revision { get; set; }
    [Key(3)] public byte[] Image { get; set; } = [];
}
[MessagePackObject]
public partial class GuildEmblemStatePacket : IntersectPacket
{
    [Key(0)] public GuildEmblemEntry[] Approved { get; set; } = [];
    [Key(1)] public GuildEmblemEntry[] Pending { get; set; } = [];
    [Key(2)] public bool CanSubmit { get; set; }
    [Key(3)] public bool CanModerate { get; set; }
    [Key(4)] public bool OpenWindow { get; set; }
    [Key(5)] public string Message { get; set; } = "";
    [Key(6)] public int Page { get; set; }
    [Key(7)] public bool HasMore { get; set; }
}
