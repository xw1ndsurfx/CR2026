using Intersect.Framework.Core.RoyalSeasons;
using MessagePack;
namespace Intersect.Network.Packets.RoyalSeasons;
[MessagePackObject]
public partial class RoyalSeasonRequestPacket : IntersectPacket
{
    [Key(0)] public SeasonAction Action { get; set; }
    [Key(1)] public Guid SeasonId { get; set; }
    [Key(2)] public Guid TierId { get; set; }
}
[MessagePackObject]
public partial class RoyalSeasonStatePacket : IntersectPacket
{
    [Key(0)] public string Json { get; set; } = "";
    [Key(1)] public bool Open { get; set; }
}
[MessagePackObject]
public partial class RoyalSeasonConfigurationRequestPacket : IntersectPacket { }
[MessagePackObject]
public partial class RoyalSeasonConfigurationSavePacket : IntersectPacket
{
    [Key(0)] public string Json { get; set; } = "";
    [Key(1)] public string Revision { get; set; } = "";
}
[MessagePackObject]
public partial class RoyalSeasonConfigurationPacket : IntersectPacket
{
    [Key(0)] public string Json { get; set; } = "";
    [Key(1)] public string Revision { get; set; } = "";
    [Key(2)] public bool Open { get; set; }
    [Key(3)] public string Error { get; set; } = "";
    [Key(4)] public Dictionary<Guid, string> ExpeditionNames { get; set; } = [];
}
