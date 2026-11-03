using Intersect.Network.Packets.Client;
using MessagePack;
namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public sealed partial class FishingStatePacket : IntersectPacket
{
    [Key(0)] public string StateJson { get; set; } = "";
    [Key(1)] public string ConfigurationJson { get; set; } = "";
    [Key(2)] public string Message { get; set; } = "";
    [Key(3)] public bool Open { get; set; }
    [Key(4)] public bool Editor { get; set; }
    [Key(5)] public bool Saved { get; set; }
    [Key(6)] public string NoticeSoundJson { get; set; } = "";
    [Key(7)] public bool Draft { get; set; }
    [Key(8)] public FishingAction? ConfigurationAction { get; set; }
}
