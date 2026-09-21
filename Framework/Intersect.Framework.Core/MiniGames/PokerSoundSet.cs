namespace Intersect.Framework.Core.MiniGames;

/// <summary>Optional one-shot poker sounds. Files are resolved from resources/sounds on the client.</summary>
public sealed record PokerSoundSet(
    string Deal = "", string Check = "", string Call = "", string Raise = "", string Fold = "",
    string AllIn = "", string Win = "", string Lose = "", string LevelUp = "", string Join = "", string Leave = "")
{
    public const int MaximumFileLength = 128;
    public static PokerSoundSet Empty { get; } = new();
    public bool IsValid => new[] { Deal, Check, Call, Raise, Fold, AllIn, Win, Lose, LevelUp, Join, Leave }
        .All(Valid);
    private static bool Valid(string? value) => value != null && value.Length <= MaximumFileLength &&
        value.All(c => !char.IsControl(c));
}
