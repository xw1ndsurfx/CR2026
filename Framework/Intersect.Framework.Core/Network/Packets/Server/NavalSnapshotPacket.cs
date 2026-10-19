using MessagePack;
namespace Intersect.Network.Packets.Server;

[MessagePackObject(AllowPrivate = true)]
public partial class NavalSnapshotPacket : IntersectPacket
{
    [Key(0)] public ShipSnapshot[] Ships { get; set; } = [];
}

[MessagePackObject]
public sealed class ShipSnapshot
{
    [Key(0)] public Guid Id { get; set; }
    [Key(1)] public Guid CaptainId { get; set; }
    [Key(2)] public Guid MapId { get; set; }
    [Key(3)] public double X { get; set; }
    [Key(4)] public double Y { get; set; }
    [Key(5)] public double Heading { get; set; }
    [Key(6)] public double Speed { get; set; }
    [Key(7)] public string Graphics { get; set; } = "";
    [Key(8)] public int Frames { get; set; }
    [Key(9)] public int FrameMilliseconds { get; set; }
    [Key(10)] public double Width { get; set; }
    [Key(11)] public double Length { get; set; }
}
