using Intersect.Framework.Core.GuildHeadquarters;
using MessagePack;
namespace Intersect.Network.Packets.GuildHeadquarters;
[MessagePackObject]
public partial class HeadquartersRequestPacket : IntersectPacket
{
    [Key(0)] public HeadquartersAction Action { get; set; }
    [Key(1)] public Guid TierId { get; set; }
    [Key(2)] public long Revision { get; set; }
    [Key(3)] public string Name { get; set; } = "";
    [Key(4)] public Guid InvitationId { get; set; }
    [Key(5)] public string ConfigurationRevision { get; set; } = "";
}
[MessagePackObject]
public partial class HeadquartersStatePacket : IntersectPacket
{
    [Key(0)] public string Json { get; set; } = "";
    [Key(1)] public bool Open { get; set; }
}
[MessagePackObject]
public partial class HeadquartersConfigurationRequestPacket : IntersectPacket { }
[MessagePackObject]
public partial class HeadquartersInvitationPacket : IntersectPacket
{
    [Key(0)] public Guid Id { get; set; }
    [Key(1)] public string HostName { get; set; } = "";
    [Key(2)] public string GuildName { get; set; } = "";
    [Key(3)] public long ExpiresUtc { get; set; }
    [Key(4)] public int VisitMinutes { get; set; }
    [Key(5)] public string Sound { get; set; } = "";
    [Key(6)] public string Icon { get; set; } = "";
    [Key(7)] public int Volume { get; set; }
}
[MessagePackObject]
public partial class HeadquartersConfigurationSavePacket : IntersectPacket
{
    [Key(0)] public string Json { get; set; } = "";
    [Key(1)] public string Revision { get; set; } = "";
}
[MessagePackObject]
public partial class HeadquartersConfigurationPacket : IntersectPacket
{
    [Key(0)] public string Json { get; set; } = "";
    [Key(1)] public string Revision { get; set; } = "";
    [Key(2)] public bool Open { get; set; }
    [Key(3)] public string Error { get; set; } = "";
}
