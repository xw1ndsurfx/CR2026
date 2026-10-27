using MessagePack;
namespace Intersect.Network.Packets.Client;
public enum JoustAction { Open, StartPve, Aim, Leave, GetConfiguration, SaveConfiguration, Redeem, PrepareWilliam, InviteDuel, AcceptDuel, DeclineDuel, BrowseDuels, BrowseSeats, ReserveSeat, LeaveSeat, SpectatorCamera }
[MessagePackObject]
public sealed partial class JoustRequestPacket : IntersectPacket
{
    [Key(0)] public JoustAction Action { get; set; }
    [Key(1)] public Guid ArenaId { get; set; }
    [Key(2)] public Guid MatchId { get; set; }
    [Key(3)] public int Difficulty { get; set; }
    [Key(4)] public int Pass { get; set; }
    [Key(5)] public long Sequence { get; set; }
    [Key(6)] public double X { get; set; }
    [Key(7)] public double Y { get; set; }
    [Key(8)] public bool Defending { get; set; }
    [Key(9)] public string ConfigurationJson { get; set; } = "";
    [Key(10)] public Guid RequestId { get; set; }
    [Key(11)] public Guid OfferId { get; set; }
    [Key(12)] public Guid TargetId { get; set; }
    [Key(13)] public Guid InvitationId { get; set; }
    [Key(14)] public Guid SeatId { get; set; }
    [Key(15)] public int Camera { get; set; }
    [Key(16)] public int AiOpponent { get; set; } = -1;
}
