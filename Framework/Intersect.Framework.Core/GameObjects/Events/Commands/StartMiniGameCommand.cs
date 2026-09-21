using System.ComponentModel;
using Intersect.Framework.Core.MiniGames;

namespace Intersect.Framework.Core.GameObjects.Events.Commands;

public enum MiniGameType { Poker = 0 }

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
    /// <summary>Stable object identity, not its name, icon or index.</summary>
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")]
    public Guid CurrencyItemId { get; set; }
    /// <summary>One-time authorized house seed, shared across instances; reopening never reseeds it.</summary>
    [DefaultValue(0L)] public long NpcReserve { get; set; }
    [DefaultValue(false)] public bool UnlimitedNpcBankroll { get; set; }
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid JoinAnimationId { get; set; }
    [DefaultValue("")] public string JoinSound { get; set; } = string.Empty;
    [DefaultValue("")] public string DealSound { get; set; } = string.Empty;
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid CheckAnimationId { get; set; }
    [DefaultValue("")] public string CheckSound { get; set; } = string.Empty;
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid CallAnimationId { get; set; }
    [DefaultValue("")] public string CallSound { get; set; } = string.Empty;
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid RaiseAnimationId { get; set; }
    [DefaultValue("")] public string RaiseSound { get; set; } = string.Empty;
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid FoldAnimationId { get; set; }
    [DefaultValue("")] public string FoldSound { get; set; } = string.Empty;
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid AllInAnimationId { get; set; }
    [DefaultValue("")] public string AllInSound { get; set; } = string.Empty;
    [DefaultValue("")] public string VictorySound { get; set; } = string.Empty;
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid LevelUpAnimationId { get; set; }
    [DefaultValue("")] public string LevelUpSound { get; set; } = string.Empty;
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid LeaveAnimationId { get; set; }
    [DefaultValue("")] public string LeaveSound { get; set; } = string.Empty;
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid YourTurnAnimationId { get; set; }
    [DefaultValue("")] public string YourTurnSound { get; set; } = string.Empty;
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid FlopAnimationId { get; set; }
    [DefaultValue("")] public string FlopSound { get; set; } = string.Empty;
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid TurnAnimationId { get; set; }
    [DefaultValue("")] public string TurnSound { get; set; } = string.Empty;
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid RiverAnimationId { get; set; }
    [DefaultValue("")] public string RiverSound { get; set; } = string.Empty;
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid LevelRewardItemId { get; set; }
    [DefaultValue(0)] public int LevelRewardQuantity { get; set; }

    public bool HasValidSettings() => Game == MiniGameType.Poker &&
        !string.IsNullOrEmpty(TableId) && TableId.Length <= 64 &&
        TableId.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or
            >= '0' and <= '9' or '-' or '_') &&
        MaxPlayers is >= 2 and <= 6 && SmallBlind >= 1 && BigBlind >= SmallBlind &&
        BigBlind <= 1_000_000_000 && StartingChips >= BigBlind && StartingChips <= 1_000_000_000 &&
        NpcReserve is >= 0 and <= 1_000_000_000 &&
        TurnSeconds is >= 5 and <= 300 && NpcPlayers is >= 0 and <= 5 &&
        NpcPlayers + (DealerPlays ? 1 : 0) < MaxPlayers && MiniGameProgression.IsBack(NpcCardBackId) &&
        LevelRewardQuantity is >= 0 and <= 1_000_000 &&
        (LevelRewardItemId != Guid.Empty || LevelRewardQuantity == 0) &&
        ValidSound(JoinSound) && ValidSound(DealSound) && ValidSound(CheckSound) && ValidSound(CallSound) &&
        ValidSound(RaiseSound) && ValidSound(FoldSound) && ValidSound(AllInSound) &&
        ValidSound(VictorySound) && ValidSound(LevelUpSound) && ValidSound(LeaveSound) &&
        ValidSound(YourTurnSound) && ValidSound(FlopSound) && ValidSound(TurnSound) && ValidSound(RiverSound);

    private static bool ValidSound(string? sound) => sound is null || sound.Length <= 128 && sound.All(c => !char.IsControl(c));
}
