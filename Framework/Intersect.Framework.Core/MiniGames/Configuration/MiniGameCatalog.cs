using Intersect.Framework.Core.GameObjects.Events.Commands;

namespace Intersect.Framework.Core.MiniGames.Configuration;

/// <summary>
/// Small shared registry for mini-game types exposed by this build. Adding a future game such as
/// Blackjack starts here so editor labels and common validation do not need Poker-specific forks.
/// </summary>
public sealed record MiniGameDefinition(
    MiniGameType Type,
    string DisplayName,
    int MinimumPlayers,
    int MaximumPlayers,
    string DefaultTableId);

public static class MiniGameCatalog
{
    private static readonly MiniGameDefinition[] Definitions =
    [
        new(MiniGameType.Poker, "Poker - Texas hold'em", 2, 6, "poker-1"),
        new(MiniGameType.Blackjack, "Blackjack - versus dealer", 2, 6, "blackjack-1"),
    ];

    public static IReadOnlyList<MiniGameDefinition> All => Definitions;

    public static bool TryGet(MiniGameType type, out MiniGameDefinition definition)
    {
        definition = Definitions.FirstOrDefault(item => item.Type == type)!;
        return definition != null;
    }

    public static MiniGameDefinition Get(MiniGameType type) =>
        TryGet(type, out var definition) ? definition : throw new ArgumentOutOfRangeException(nameof(type));

    public static bool IsValidTableId(string? value) =>
        !string.IsNullOrEmpty(value) && value.Length <= 64 &&
        value.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_');
}
