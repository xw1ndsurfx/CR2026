using Intersect.Framework.Core.Exportation;
using MessagePack;

namespace Intersect.Network.Packets.Client;

public enum ExportationAction : byte
{
    Refresh = 0,
    Pack = 1,
    Seal = 2,
    Deliver = 3,
    Leave = 4,
    Restart = 5,
    GetConfiguration = 10,
    SaveConfiguration = 11,
}

[MessagePackObject]
public sealed partial class ExportationRequestPacket : IntersectPacket
{
    [Key(0)] public ExportationAction Action { get; set; }
    [Key(1)] public Guid RunId { get; set; }
    [Key(2)] public long Sequence { get; set; }
    [Key(3)] public Guid ItemId { get; set; }
    [Key(4)] public CargoGroup Group { get; set; }
    [Key(5)] public int QuantityChange { get; set; }
    [Key(6)] public string ConfigurationJson { get; set; } = "";
    [Key(7)] public bool OpenEditor { get; set; }
}
