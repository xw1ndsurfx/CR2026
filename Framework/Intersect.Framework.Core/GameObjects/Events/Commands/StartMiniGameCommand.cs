using System.ComponentModel;
using Intersect.Framework.Core.MiniGames;
using Intersect.Framework.Core.MiniGames.Configuration;
using Intersect.Framework.Core.MiniGames.Blackjack;
using Intersect.Framework.Core.MiniGames.Lockpicking;
using Intersect.Framework.Core.MiniGames.Roulette;

namespace Intersect.Framework.Core.GameObjects.Events.Commands;

public enum MiniGameType { Poker = 0, Blackjack = 1, Potions = 2, Roulette = 3, Cooking = 4, Lockpicking = 5 }

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
    [DefaultValue(10L)] public long RouletteMinimumBet { get; set; } = 10;
    [DefaultValue(100L)] public long RouletteMaximumBet { get; set; } = 100;
    [DefaultValue(2)] public int LockpickDifficulty { get; set; } = 2;
    [DefaultValue(4)] public int LockpickMaxMistakes { get; set; } = 4;
    [DefaultValue(30)] public int LockpickTimeSeconds { get; set; } = 30;
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")]
    public Guid LockpickProfessionId { get; set; }
    [DefaultValue(25L)] public long LockpickProfessionBaseExperience { get; set; } = 25;
    [DefaultValue(0)] public int LockpickRequiredProfessionLevel { get; set; }
    [DefaultValue(50)] public int LockpickPerfectExperienceBonusPercent { get; set; } = 50;
    [DefaultValue(25)] public int LockpickFastExperienceBonusPercent { get; set; } = 25;
    [DefaultValue(10)] public int LockpickFastThresholdSeconds { get; set; } = 10;
    [DefaultValue(LockpickLockType.Standard)] public LockpickLockType LockpickType { get; set; } = LockpickLockType.Standard;
    [DefaultValue(LockpickUnlockScope.Crew)] public LockpickUnlockScope LockpickUnlockScope { get; set; } = LockpickUnlockScope.Crew;
    [DefaultValue(LockpickToolQuality.None)] public LockpickToolQuality LockpickMinimumToolQuality { get; set; } = LockpickToolQuality.None;
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid LockpickBasicToolItemId { get; set; }
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid LockpickReinforcedToolItemId { get; set; }
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid LockpickRoyalToolItemId { get; set; }
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid LockpickMasterToolItemId { get; set; }
    [DefaultValue(25)] public int LockpickBasicBreakChancePercent { get; set; } = 25;
    [DefaultValue(15)] public int LockpickReinforcedBreakChancePercent { get; set; } = 15;
    [DefaultValue(8)] public int LockpickRoyalBreakChancePercent { get; set; } = 8;
    [DefaultValue(3)] public int LockpickMasterBreakChancePercent { get; set; } = 3;
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid LockpickKeyItemId { get; set; }
    [DefaultValue(false)] public bool LockpickConsumeKey { get; set; }
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid LockpickMistakeCommonEventId { get; set; }
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid LockpickFailureCommonEventId { get; set; }
    [DefaultValue(30)] public int LockpickFailureCooldownSeconds { get; set; } = 30;
    [DefaultValue(0)] public int LockpickInvasionDifficultyBonus { get; set; }
    [DefaultValue(true)] public bool LockpickUseProfessionSkillBonus { get; set; } = true;
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
        CreateSoundSet().IsValid && CreateMotionSet().IsValid &&
        (Game switch
        {
            MiniGameType.Poker => SmallBlind >= 1 && BigBlind >= SmallBlind &&
                BigBlind <= 1_000_000_000 && StartingChips >= BigBlind &&
                NpcPlayers + (DealerPlays ? 1 : 0) < MaxPlayers,
            MiniGameType.Blackjack => NpcPlayers < MaxPlayers - 1 &&
                new BlackjackRules(MaxPlayers - 1, StartingChips, BlackjackMinimumBet, BlackjackMaximumBet,
                    TurnSeconds, BlackjackHitSoft17).IsValid,
            MiniGameType.Potions => true,
            MiniGameType.Cooking =>
                MaxPlayers is >= 1 and <= 2 &&
                CurrencyItemId == Guid.Empty &&
                NpcPlayers == 0 &&
                !DealerPlays,
            MiniGameType.Lockpicking =>
                MaxPlayers == 1 &&
                CurrencyItemId == Guid.Empty &&
                NpcPlayers == 0 &&
                !DealerPlays &&
                LockpickDifficulty is >= 1 and <= 5 &&
                LockpickMaxMistakes is >= 1 and <= 10 &&
                LockpickTimeSeconds is >= 10 and <= 180 &&
                LockpickProfessionId != Guid.Empty &&
                LockpickProfessionBaseExperience is >= 1 and <= 2_000_000_000 &&
                LockpickRequiredProfessionLevel is >= 0 and <= 500 &&
                LockpickPerfectExperienceBonusPercent is >= 0 and <= 500 &&
                LockpickFastExperienceBonusPercent is >= 0 and <= 500 &&
                LockpickFastThresholdSeconds is >= 1 and <= 180 &&
                Enum.IsDefined(LockpickType) &&
                Enum.IsDefined(LockpickUnlockScope) &&
                Enum.IsDefined(LockpickMinimumToolQuality) &&
                LockpickBasicBreakChancePercent is >= 0 and <= 100 &&
                LockpickReinforcedBreakChancePercent is >= 0 and <= 100 &&
                LockpickRoyalBreakChancePercent is >= 0 and <= 100 &&
                LockpickMasterBreakChancePercent is >= 0 and <= 100 &&
                LockpickFailureCooldownSeconds is >= 0 and <= 86_400 &&
                LockpickInvasionDifficultyBonus is >= 0 and <= 4,
            MiniGameType.Roulette =>
                MaxPlayers == 1 &&
                NpcPlayers == 0 &&
                !DealerPlays &&
                RouletteMinimumBet >= 1 &&
                RouletteMaximumBet >= RouletteMinimumBet &&
                RouletteMaximumBet <= 20_000_000 &&
                StartingChips >= RouletteMinimumBet &&
                (CurrencyItemId == Guid.Empty ||
                    NpcReserve >= RouletteMaximumBet * 35L),
            _ => false,
        });
}
