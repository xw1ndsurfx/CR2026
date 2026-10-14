using MessagePack;

namespace Intersect.Network.Packets.Client;

[MessagePackObject]
public partial class RequestGuildDirectoryPacket : IntersectPacket
{
    public RequestGuildDirectoryPacket() { }

    public RequestGuildDirectoryPacket(string search, int page = 0)
    {
        Search = search ?? string.Empty;
        Page = page;
    }

    [Key(0)]
    public string Search { get; set; } = string.Empty;

    [Key(1)]
    public int Page { get; set; }
}
