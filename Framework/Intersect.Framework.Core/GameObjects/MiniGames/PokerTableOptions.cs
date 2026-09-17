using System.ComponentModel;

namespace Intersect.Framework.Core.GameObjects.MiniGames;

/// <summary>
/// Configuration for a future server-owned poker table, using play chips only.
/// This model does not debit inventory, currency, or any persistent balance.
/// </summary>
public sealed class PokerTableOptions
{
    public const int MinimumPlayers = 2;
    public const int MaximumPlayers = 6;
    public const long MaximumStartingChips = 1_000_000;
    public const int MinimumTurnTimeoutSeconds = 5;
    public const int MaximumTurnTimeoutSeconds = 300;

    // Explicit defaults also preserve values with IgnoreAndPopulate JSON settings.
    [DefaultValue(6)]
    public int MaxPlayers { get; set; } = 6;

    [DefaultValue(1000L)]
    public long StartingChips { get; set; } = 1000;

    [DefaultValue(10L)]
    public long SmallBlind { get; set; } = 10;

    [DefaultValue(20L)]
    public long BigBlind { get; set; } = 20;

    [DefaultValue(30)]
    public int TurnTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Returns all configuration errors without silently changing saved values.
    /// Callers must reject invalid options before creating or joining a table.
    /// </summary>
    public IReadOnlyList<string> GetValidationErrors()
    {
        var errors = new List<string>();

        if (MaxPlayers < MinimumPlayers || MaxPlayers > MaximumPlayers)
        {
            errors.Add($"MaxPlayers must be between {MinimumPlayers} and {MaximumPlayers}.");
        }

        if (StartingChips < 1 || StartingChips > MaximumStartingChips)
        {
            errors.Add($"StartingChips must be between 1 and {MaximumStartingChips}.");
        }

        if (SmallBlind < 1)
        {
            errors.Add("SmallBlind must be positive.");
        }

        if (BigBlind < 1 || BigBlind <= SmallBlind)
        {
            errors.Add("BigBlind must be positive and greater than SmallBlind.");
        }

        if (BigBlind > StartingChips)
        {
            errors.Add("BigBlind cannot exceed StartingChips.");
        }

        if (TurnTimeoutSeconds < MinimumTurnTimeoutSeconds ||
            TurnTimeoutSeconds > MaximumTurnTimeoutSeconds)
        {
            errors.Add(
                $"TurnTimeoutSeconds must be between {MinimumTurnTimeoutSeconds} and {MaximumTurnTimeoutSeconds}."
            );
        }

        return errors;
    }
}
