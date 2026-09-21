using System.ComponentModel;
using Intersect.Framework.Core.MiniGames;
using Intersect.Framework.Core.MiniGames.Blackjack;

namespace Intersect.Framework.Core.GameObjects.Events.Commands;

public enum MiniGameType { Poker = 0, Blackjack = 1 }

/// <summary>Empty currency selects isolated test chips. A currency item selects inventory-backed play.</summary>
public sealed class StartMiniGameCommand : EventCommand
{
    public override EventCommandType Type => EventCommandType.StartMiniGame;
    [DefaultValue(MiniGameType.Poker)] public MiniGameType Game { get; set; } = MiniGameType.Poker;
    [DefaultValue("poker-1")] public string TableId { get; set; } = "poker-1";
    [DefaultValue(6)] public int MaxPlayers { get; set; } = 6;
    [DefaultValue(1000L)] public long StartingChips { get; set; } = 1000;
    [DefaultValue(5L)] public long SmallBlind { get; set; } = 5;
    [DefaultValue(10L)] public long BigBlind { get; set; } = 10;
    [DefaultValue(30)] public int TurnSeconds { get; set; } = 30;
    [DefaultValue(false)] public bool DealerPlays { get; set; }
    [DefaultValue(0)] public int NpcPlayers { get; set; }
    [DefaultValue(false)] public bool AutoStart { get; set; }
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")]
    public Guid DealAnimationId { get; set; }
    [DefaultValue(false)] public bool AnnounceWins { get; set; }
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")]
    public Guid VictoryAnimationId { get; set; }
    [DefaultValue(0)] public int NpcCardBackId { get; set; }
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")]
    public Guid CurrencyItemId { get; set; }
    [DefaultValue(0L)] public long NpcReserve { get; set; }
    [DefaultValue(10L)] public long BlackjackMinimumBet { get; set; } = 10;
    [DefaultValue(100L)] public long BlackjackMaximumBet { get; set; } = 100;
    [DefaultValue(false)] public bool BlackjackHitSoft17 { get; set; }

    public bool HasValidSettings() =>
        !string.IsNullOrEmpty(TableId) && TableId.Length <= 64 &&
        TableId.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_') &&
        MaxPlayers is >= 2 and <= 6 && StartingChips is >= 1 and <= 1_000_000_000 &&
        NpcReserve is >= 0 and <= 1_000_000_000 && TurnSeconds is >= 5 and <= 300 &&
        NpcPlayers is >= 0 and <= 5 && MiniGameProgression.IsBack(NpcCardBackId) &&
        (Game == MiniGameType.Poker ? SmallBlind >= 1 && BigBlind >= SmallBlind &&
            BigBlind <= 1_000_000_000 && StartingChips >= BigBlind && NpcPlayers + (DealerPlays ? 1 : 0) < MaxPlayers :
         Game == MiniGameType.Blackjack && NpcPlayers < MaxPlayers - 1 &&
            new BlackjackRules(MaxPlayers-1,StartingChips,BlackjackMinimumBet,BlackjackMaximumBet,TurnSeconds,BlackjackHitSoft17).IsValid);
}
