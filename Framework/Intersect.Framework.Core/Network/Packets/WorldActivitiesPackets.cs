using MessagePack;
namespace Intersect.Network.Packets.WorldEvents;
[MessagePackObject] public partial class RequestWorldActivitiesPacket : IntersectPacket { }
[MessagePackObject] public partial class SaveWorldActivitiesPacket : IntersectPacket { [Key(0)] public string Json { get; set; } = ""; }
[MessagePackObject] public partial class WorldActivitiesPacket : IntersectPacket
{
    [Key(0)] public string Json { get; set; } = "";
    [Key(1)] public bool OpenEditor { get; set; }
}

[MessagePackObject] public partial class ExperienceEventAnnouncementPacket : IntersectPacket
{
    [Key(0)] public string[] Names { get; set; } = [];
    [Key(1)] public int BonusPercent { get; set; }
}
