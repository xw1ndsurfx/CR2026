using MessagePack;
namespace Intersect.Network.Packets.Client;
public enum MountRaceAction { Open, Poll, Join, Practice, Ready, Leave, Claim, GetConfiguration, SaveConfiguration }
[MessagePackObject]
public partial class MountRaceRequestPacket : IntersectPacket
{
    [Key(0)] public MountRaceAction Action { get; set; }
    [Key(1)] public Guid CourseId { get; set; }
    [Key(2)] public Guid RaceId { get; set; }
    [Key(3)] public Guid RecordId { get; set; }
    [Key(4)] public string ConfigurationJson { get; set; } = "";
    [Key(5)] public Guid RequestId { get; set; }
}
