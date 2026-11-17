using Intersect.Framework.Core.MiniGames.Potions;

namespace Intersect.Client.MiniGames;

/// <summary>Only newly accepted results produce feedback; initial snapshots and repeats stay silent.</summary>
public static class PotionResultSound
{
    public static string Select(PotionSessionState? previous, PotionSessionState next)
    {
        if (previous == null || previous.Revision >= next.Revision) return "";
        if (next.Complete && !previous.Complete) return next.Assets.CompleteSound;
        if (next.GameOver && !previous.GameOver) return next.Assets.GameOverSound;
        if (next.LastScoreGain > 0 && !next.Board.SequenceEqual(previous.Board)) return next.Assets.MergeSound;
        return "";
    }
}
