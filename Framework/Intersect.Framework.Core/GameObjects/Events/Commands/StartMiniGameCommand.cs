using System.ComponentModel;

namespace Intersect.Framework.Core.GameObjects.Events.Commands;

public enum MiniGameType { Poker = 0 }

/// <summary>
/// Opens or rejoins a server-owned lobby. Tables are scoped to the player's current map and
/// map instance. Reuse TableId on multiple events to address the same table in that scope.
/// Starting chips are temporary test chips, never inventory items or persistent currency.
/// </summary>
public sealed class StartMiniGameCommand : EventCommand
{
    public override EventCommandType Type => EventCommandType.StartMiniGame;

    [DefaultValue(MiniGameType.Poker)]
    public MiniGameType Game { get; set; } = MiniGameType.Poker;

    [DefaultValue("poker-1")]
    public string TableId { get; set; } = "poker-1";

    [DefaultValue(6)]
    public int MaxPlayers { get; set; } = 6;

    [DefaultValue(1000L)]
    public long StartingChips { get; set; } = 1000;

    [DefaultValue(5L)]
    public long SmallBlind { get; set; } = 5;

    [DefaultValue(10L)]
    public long BigBlind { get; set; } = 10;

    [DefaultValue(30)]
    public int TurnSeconds { get; set; } = 30;

    // DefaultValue attributes are required by the event serializer's IgnoreAndPopulate mode.
    public bool HasValidSettings() => Game == MiniGameType.Poker &&
        !string.IsNullOrEmpty(TableId) && TableId.Length <= 64 &&
        TableId.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or
            >= '0' and <= '9' or '-' or '_') &&
        MaxPlayers is >= 2 and <= 6 && SmallBlind >= 1 && BigBlind >= SmallBlind &&
        BigBlind <= 1_000_000_000 && StartingChips >= BigBlind && StartingChips <= 1_000_000_000 &&
        TurnSeconds is >= 5 and <= 300;
}
