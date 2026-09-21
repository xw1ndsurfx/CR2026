using Intersect.Server.MiniGames.Progression;

namespace Intersect.Server.MiniGames.Blackjack;

/// <summary>
/// Dedicated Blackjack progression facade. The persistence layer is shared infrastructure,
/// but the game key keeps Blackjack XP, wins and level completely separate from Poker.
/// Gameplay awards are wired by the Blackjack runtime in Part 2.
/// </summary>
public sealed class BlackjackProgressionService
{
    private readonly IMiniGameProgressStore _store;

    public BlackjackProgressionService(IMiniGameProgressStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public MiniGameProgress Load(Guid character) =>
        _store.Load(character, Framework.Core.MiniGames.MiniGameProgression.Blackjack);

    public MiniGameProgress AwardWin(Guid character, Guid tableInstance, long handId) =>
        _store.AwardWin(character, Framework.Core.MiniGames.MiniGameProgression.Blackjack, tableInstance, handId);
}
