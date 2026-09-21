using System.ComponentModel;
using Intersect.Framework.Core.MiniGames;
using Intersect.Framework.Core.MiniGames.Configuration;
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
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid CheckAnimationId { get; set; }
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid CallAnimationId { get; set; }
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid RaiseAnimationId { get; set; }
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid FoldAnimationId { get; set; }
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid AllInAnimationId { get; set; }
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid LoseAnimationId { get; set; }
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid LevelUpAnimationId { get; set; }
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid JoinAnimationId { get; set; }
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid LeaveAnimationId { get; set; }
    [DefaultValue("")] public string DealSound { get; set; } = "";
    [DefaultValue("")] public string CheckSound { get; set; } = "";
    [DefaultValue("")] public string CallSound { get; set; } = "";
    [DefaultValue("")] public string RaiseSound { get; set; } = "";
    [DefaultValue("")] public string FoldSound { get; set; } = "";
    [DefaultValue("")] public string AllInSound { get; set; } = "";
    [DefaultValue("")] public string WinSound { get; set; } = "";
    [DefaultValue("")] public string LoseSound { get; set; } = "";
    [DefaultValue("")] public string LevelUpSound { get; set; } = "";
    [DefaultValue("")] public string JoinSound { get; set; } = "";
    [DefaultValue("")] public string LeaveSound { get; set; } = "";
    [DefaultValue(0)] public int NpcCardBackId { get; set; }
    /// <summary>Stable object identity, not its name, icon or index.</summary>
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")]
    public Guid CurrencyItemId { get; set; }
    /// <summary>One-time authorized house seed, shared across instances; reopening never reseeds it.</summary>
    [DefaultValue(0L)] public long NpcReserve { get; set; }
    /// <summary>Explicit system-funded mode: NPC buy-ins are replenished as needed and can create currency.</summary>
    [DefaultValue(false)] public bool UnlimitedNpcBankroll { get; set; }
    [DefaultValue(10L)] public long BlackjackMinimumBet { get; set; } = 10;
    [DefaultValue(100L)] public long BlackjackMaximumBet { get; set; } = 100;
    [DefaultValue(false)] public bool BlackjackHitSoft17 { get; set; }
    [DefaultValue(PokerMotionSpeed.Normal)] public PokerMotionSpeed ProceduralAnimationSpeed { get; set; } = PokerMotionSpeed.Normal;
    [DefaultValue(true)] public bool AnimateDealCards { get; set; } = true;
    [DefaultValue(true)] public bool AnimateBoardCards { get; set; } = true;
    [DefaultValue(true)] public bool AnimateChips { get; set; } = true;
    [DefaultValue(true)] public bool AnimateShowdown { get; set; } = true;
    [DefaultValue(true)] public bool AnimateShuffle { get; set; } = true;
    [DefaultValue(true)] public bool AnimateAllIn { get; set; } = true;
    public PokerLevelReward[] LevelRewards { get; set; } = [];

    public PokerSoundSet CreateSoundSet() => new(
        DealSound ?? "", CheckSound ?? "", CallSound ?? "", RaiseSound ?? "", FoldSound ?? "",
        AllInSound ?? "", WinSound ?? "", LoseSound ?? "", LevelUpSound ?? "", JoinSound ?? "", LeaveSound ?? "");

    public PokerAnimationSet CreateAnimationSet() => new(
        DealAnimationId, CheckAnimationId, CallAnimationId, RaiseAnimationId, FoldAnimationId, AllInAnimationId,
        VictoryAnimationId, LoseAnimationId, LevelUpAnimationId, JoinAnimationId, LeaveAnimationId);

    public PokerLevelRewardSet CreateLevelRewardSet() => new(LevelRewards ?? []);
    public PokerMotionSet CreateMotionSet() => new(ProceduralAnimationSpeed, AnimateDealCards, AnimateBoardCards,
        AnimateChips, AnimateShowdown, AnimateShuffle, AnimateAllIn);

    public bool HasValidSettings() => MiniGameCatalog.TryGet(Game, out var definition) &&
        MiniGameCatalog.IsValidTableId(TableId) &&
        MaxPlayers >= definition.MinimumPlayers && MaxPlayers <= definition.MaximumPlayers &&
        StartingChips is >= 1 and <= 1_000_000_000 &&
        NpcReserve is >= 0 and <= 1_000_000_000 &&
        TurnSeconds is >= 5 and <= 300 && NpcPlayers is >= 0 and <= 5 &&
        MiniGameProgression.IsBack(NpcCardBackId) &&
        CreateSoundSet().IsValid && CreateLevelRewardSet().IsValid && CreateMotionSet().IsValid &&
        (Game switch
        {
            MiniGameType.Poker => SmallBlind >= 1 && BigBlind >= SmallBlind &&
                BigBlind <= 1_000_000_000 && StartingChips >= BigBlind &&
                NpcPlayers + (DealerPlays ? 1 : 0) < MaxPlayers,
            MiniGameType.Blackjack => NpcPlayers < MaxPlayers - 1 &&
                new BlackjackRules(MaxPlayers - 1, StartingChips, BlackjackMinimumBet, BlackjackMaximumBet,
                    TurnSeconds, BlackjackHitSoft17).IsValid,
            _ => false,
        });
}
