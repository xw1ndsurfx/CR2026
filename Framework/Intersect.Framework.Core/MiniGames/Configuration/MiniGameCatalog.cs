using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.MiniGames;

namespace Intersect.Framework.Core.MiniGames.Configuration;

/// <summary>
/// Small shared registry for mini-game types exposed by this build. Adding a future game such as
/// Blackjack starts here so editor labels and common validation do not need Poker-specific forks.
/// </summary>
public sealed record MiniGameDefinition(
    MiniGameType Type,
    string DisplayName,
    string ProgressKey,
    int MinimumPlayers,
    int MaximumPlayers,
    string DefaultTableId,
    bool Playable);

public static class MiniGameCatalog
{
    private static readonly MiniGameDefinition[] Definitions =
    [
        new(MiniGameType.Poker, "Poker - Texas hold'em", MiniGameProgression.Poker, 2, 6, "poker-1", true),
        // Foundation registered now; exposed by All when the Blackjack runtime lands in Part 2.
        new(MiniGameType.Blackjack, "Blackjack", MiniGameProgression.Blackjack, 1, 6, "blackjack-1", false),
    ];

    public static IReadOnlyList<MiniGameDefinition> Registered => Definitions;
    public static IReadOnlyList<MiniGameDefinition> All => Definitions.Where(item => item.Playable).ToArray();

    public static bool TryGetRegistered(MiniGameType type, out MiniGameDefinition definition)
    {
        definition = Definitions.FirstOrDefault(item => item.Type == type)!;
        return definition != null;
    }

    public static bool TryGet(MiniGameType type, out MiniGameDefinition definition)
    {
        if (!TryGetRegistered(type, out definition)) return false;
        return definition.Playable;
    }

    public static MiniGameDefinition Get(MiniGameType type) =>
        TryGet(type, out var definition) ? definition : throw new ArgumentOutOfRangeException(nameof(type));

    public static bool IsValidTableId(string? value) =>
        !string.IsNullOrEmpty(value) && value.Length <= 64 &&
        value.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_');
}
