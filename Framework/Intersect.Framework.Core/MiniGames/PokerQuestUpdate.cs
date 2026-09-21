namespace Intersect.Framework.Core.MiniGames;

public readonly record struct PokerQuestUpdate(bool PlayedHand, long NetWin, int PokerLevel)
{
    public bool WonHand => PlayedHand && NetWin > 0;
}
