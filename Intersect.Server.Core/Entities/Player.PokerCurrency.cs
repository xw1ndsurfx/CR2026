namespace Intersect.Server.Entities;

public partial class Player
{
    internal static Player[] PokerOnlineSnapshot() => OnlinePlayersById.Values.ToArray();
}
