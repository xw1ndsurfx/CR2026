using MessagePack;

namespace Intersect.Network.Packets.Client;

[MessagePackObject]
public partial class SteamLoginPacket : IntersectPacket
{
    public SteamLoginPacket()
    {
    }

    public SteamLoginPacket(string ticket)
    {
        Ticket = ticket;
    }

    [Key(0)]
    public string Ticket { get; set; } = string.Empty;
}
