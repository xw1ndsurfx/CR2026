using MessagePack;
namespace Intersect.Network.Packets.Server;

public enum NavalSetupOutcome : byte { Created, Existing, NotOcean, NoSpace, ConfigurationError, ShipLimit }

[MessagePackObject(AllowPrivate = true)]
public partial class NavalSetupResultPacket : IntersectPacket
{
    [Key(0)] public NavalSetupOutcome Outcome { get; set; }
    [Key(1)] public string MapName { get; set; } = "";
    [Key(2)] public Guid ShipId { get; set; }
    [Key(3)] public double X { get; set; }
    [Key(4)] public double Y { get; set; }
    [Key(5)] public int DockX { get; set; }
    [Key(6)] public int DockY { get; set; }
}
