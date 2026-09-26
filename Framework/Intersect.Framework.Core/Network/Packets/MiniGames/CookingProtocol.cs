using Intersect.Framework.Core.MiniGames.Cooking;
using MessagePack;

namespace Intersect.Network.Packets.MiniGames;

public enum CookingRequestKind
{
    Refresh = 0,
    StartRecipe = 1,
    RespondInvite = 2,
    Action = 3,
    Leave = 4,
}

[MessagePackObject]
public sealed partial class CookingRecipeSummary
{
    [Key(0)] public Guid Id { get; set; }
    [Key(1)] public string Name { get; set; } = string.Empty;
    [Key(2)] public int RequiredLevel { get; set; }
    [Key(3)] public long Experience { get; set; }
    [Key(4)] public bool AllowSolo { get; set; }
    [Key(5)] public bool AllowCoop { get; set; }
    [Key(6)] public bool RequireCoop { get; set; }
    [Key(7)] public bool Unlocked { get; set; }
    [Key(8)] public string LockedReason { get; set; } = string.Empty;
    [Key(9)] public CookingIngredientState[] Ingredients { get; set; } = [];
}

[MessagePackObject]
public sealed partial class CookingIngredientState
{
    [Key(0)] public Guid ItemId { get; set; }
    [Key(1)] public string Name { get; set; } = string.Empty;
    [Key(2)] public int Needed { get; set; }
    [Key(3)] public int Available { get; set; }
}

[MessagePackObject]
public sealed partial class CookingPartyCandidate
{
    [Key(0)] public Guid PlayerId { get; set; }
    [Key(1)] public string Name { get; set; } = string.Empty;
}

[MessagePackObject]
public sealed partial class CookingParticipantState
{
    [Key(0)] public Guid PlayerId { get; set; }
    [Key(1)] public string Name { get; set; } = string.Empty;
    [Key(2)] public int Score { get; set; }
    [Key(3)] public int Actions { get; set; }
    [Key(4)] public bool Ready { get; set; }
}

[MessagePackObject]
public sealed partial class CookingSessionState
{
    [Key(0)] public long Revision { get; set; }
    [Key(1)] public bool RecipeSelectionRequired { get; set; }
    [Key(2)] public bool WaitingForPartner { get; set; }
    [Key(3)] public bool InvitePendingForYou { get; set; }
    [Key(4)] public bool IsHost { get; set; }
    [Key(5)] public Guid HostId { get; set; }
    [Key(6)] public string HostName { get; set; } = string.Empty;
    [Key(7)] public Guid PartnerId { get; set; }
    [Key(8)] public string PartnerName { get; set; } = string.Empty;
    [Key(9)] public CookingRecipeSummary[] Recipes { get; set; } = [];
    [Key(10)] public CookingPartyCandidate[] PartyCandidates { get; set; } = [];
    [Key(11)] public Guid RecipeId { get; set; }
    [Key(12)] public string RecipeName { get; set; } = string.Empty;
    [Key(13)] public int ProfessionLevel { get; set; }
    [Key(14)] public string ProfessionName { get; set; } = string.Empty;
    [Key(15)] public int StageIndex { get; set; } = -1;
    [Key(16)] public int StageCount { get; set; }
    [Key(17)] public CookingStageType StageType { get; set; }
    [Key(18)] public CookingStageAssignment StageAssignment { get; set; }
    [Key(19)] public long StageStartedUnixMs { get; set; }
    [Key(20)] public int StageDurationMs { get; set; }
    [Key(21)] public int TargetPermille { get; set; }
    [Key(22)] public int TolerancePermille { get; set; }
    [Key(23)] public int RequiredActions { get; set; }
    [Key(24)] public int CompletedActions { get; set; }
    [Key(25)] public bool YourTurn { get; set; }
    [Key(26)] public int StageScore { get; set; }
    [Key(27)] public int TeamScore { get; set; }
    [Key(28)] public CookingQuality Quality { get; set; }
    [Key(29)] public bool Complete { get; set; }
    [Key(30)] public string Status { get; set; } = string.Empty;
    [Key(31)] public CookingParticipantState[] Participants { get; set; } = [];
    [Key(32)] public string RewardText { get; set; } = string.Empty;
    [Key(33)] public int StageDifficulty { get; set; }
    [Key(34)] public int MeterPermille { get; set; }
    [Key(35)] public int Combo { get; set; }
    [Key(36)] public int Mishaps { get; set; }
    [Key(37)] public string ActionHint { get; set; } = string.Empty;

    [IgnoreMember]
    public bool IsValid =>
        Revision >= 0 &&
        HostId != Guid.Empty &&
        HostName is { Length: >= 1 and <= 64 } &&
        PartnerName is { Length: <= 64 } &&
        Recipes is { Length: <= 128 } &&
        PartyCandidates is { Length: <= 12 } &&
        StageIndex is >= -1 and < 12 &&
        StageCount is >= 0 and <= 12 &&
        StageDurationMs is >= 0 and <= 60_000 &&
        TargetPermille is >= 0 and <= 1000 &&
        TolerancePermille is >= 0 and <= 500 &&
        RequiredActions is >= 0 and <= 20 &&
        CompletedActions is >= 0 and <= 40 &&
        StageDifficulty is >= 0 and <= 5 &&
        MeterPermille is >= 0 and <= 1000 &&
        Combo is >= 0 and <= 999 &&
        Mishaps is >= 0 and <= 999 &&
        ActionHint is { Length: <= 160 } &&
        StageScore is >= 0 and <= 100 &&
        TeamScore is >= 0 and <= 100 &&
        Enum.IsDefined(Quality) &&
        Status is { Length: <= 240 } &&
        RewardText is { Length: <= 240 };
}
