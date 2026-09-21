namespace Intersect.Framework.Core.MiniGames;

public readonly record struct BlackjackQuestUpdate(bool PlayedHand, long NetWin, int BlackjackLevel)
{
    public bool WonHand => PlayedHand && NetWin > 0;
}
