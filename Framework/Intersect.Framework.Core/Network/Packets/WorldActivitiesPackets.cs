using MessagePack;
using Intersect.Framework.Core.WorldEvents;
namespace Intersect.Network.Packets.WorldEvents;
[MessagePackObject] public partial class RequestWorldActivitiesPacket : IntersectPacket { [Key(0)] public WorldActivitiesSection Section { get; set; } }
[MessagePackObject] public partial class SaveWorldActivitiesPacket : IntersectPacket { [Key(0)] public string Json { get; set; } = ""; [Key(1)] public WorldActivitiesSection Section { get; set; } }
[MessagePackObject] public partial class WorldActivitiesPacket : IntersectPacket
{
    [Key(0)] public string Json { get; set; } = "";
    [Key(1)] public bool OpenEditor { get; set; }
    [Key(2)] public WorldActivitiesSection Section { get; set; }
}

[MessagePackObject] public partial class ExperienceEventAnnouncementPacket : IntersectPacket
{
    [Key(0)] public string[] Names { get; set; } = [];
    [Key(1)] public int BonusPercent { get; set; }
    [Key(2)] public long ServerTimeUnixMs { get; set; }
    [Key(3)] public long EndsAtUnixMs { get; set; }
    [Key(4)] public bool Silent { get; set; }
}

[MessagePackObject] public partial class LootEventAnnouncementPacket : IntersectPacket
{
    [Key(0)] public string[] Names {get;set;}=[];
    [Key(1)] public int BonusPercent {get;set;}
    [Key(2)] public long ServerTimeUnixMs {get;set;}
    [Key(3)] public long EndsAtUnixMs {get;set;}
    [Key(4)] public bool Silent {get;set;}
}
[MessagePackObject] public partial class ExperienceBoostStatusPacket : IntersectPacket
{
    [Key(0)] public int BonusPercent {get;set;}
    [Key(1)] public long RemainingMs {get;set;}
}
