using MessagePack;

namespace Intersect.Network.Packets.Client;

/// <summary>Ask guild officers to send an invitation. Never bypasses guild permissions.</summary>
[MessagePackObject]
public partial class RequestGuildJoinPacket : IntersectPacket
{
    public RequestGuildJoinPacket() { }
    public RequestGuildJoinPacket(Guid guildId) => GuildId = guildId;

    [Key(0)]
    public Guid GuildId { get; set; }
}
