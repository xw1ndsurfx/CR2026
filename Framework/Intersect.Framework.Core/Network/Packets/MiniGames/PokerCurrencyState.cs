using MessagePack;

namespace Intersect.Network.Packets.MiniGames;

public sealed partial class PokerTableState
{
    // Empty = isolated test table. These are server-owned fields, never part of a request.
    [Key(26)] public Guid CurrencyItemId { get; set; }
    [Key(27)] public bool MoneyPending { get; set; }
}
