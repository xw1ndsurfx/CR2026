namespace Intersect.Framework.Core.Naval;

public enum NavalExpeditionMode : byte { Either, SingleShip, Fleet }
public enum NavalExpeditionObjective : byte { ReachPoint, DefeatPatrol, DefeatMonster, ReturnToPort }
public enum NavalExpeditionStatus : byte { Preparing, Active, Completed, Failed }
public enum NavalExpeditionAction : byte { Open, Propose, Ready, Start, Leave, Claim }

public sealed class NavalExpeditionStage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Description { get; set; } = "";
    public NavalExpeditionObjective Objective { get; set; }
    public Guid MapId { get; set; }
    public Guid TargetId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Radius { get; set; } = 4;
    public int Quantity { get; set; } = 1;

    public bool IsValid(int width, int height) => Id != Guid.Empty && Description != null && Description.Length <= 1000 &&
        Enum.IsDefined(Objective) && MapId != Guid.Empty && X >= 0 && X < width && Y >= 0 && Y < height &&
        Radius is >= 1 and <= 12 && Quantity is >= 1 and <= 20 &&
        (Objective is NavalExpeditionObjective.ReachPoint or NavalExpeditionObjective.ReturnToPort || TargetId != Guid.Empty);
}

public sealed class NavalExpeditionDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool Enabled { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public NavalExpeditionMode Mode { get; set; }
    public int MinimumPlayers { get; set; } = 2;
    public int MaximumPlayers { get; set; } = 24;
    public int MaximumShips { get; set; } = 4;
    public int RequiredSailingLevel { get; set; } = 1;
    public int TimeLimitMinutes { get; set; } = 30;
    public int ReconnectGraceSeconds { get; set; } = 180;
    public int SailingExperienceReward { get; set; } = 250;
    public Guid RewardItemId { get; set; }
    public int RewardQuantity { get; set; }
    public NavalExpeditionStage[] Stages { get; set; } = [];

    public bool AcceptsFleet(int players, int ships) => players >= MinimumPlayers && players <= MaximumPlayers &&
        ships >= (Mode == NavalExpeditionMode.Fleet ? 2 : 1) && ships <= MaximumShips &&
        (Mode != NavalExpeditionMode.SingleShip || ships == 1);

    public bool IsValid(int width, int height) => Id != Guid.Empty && Name != null && Name.Length <= 120 &&
        (!Enabled || !string.IsNullOrWhiteSpace(Name)) && Description != null && Description.Length <= 2000 &&
        Enum.IsDefined(Mode) && MinimumPlayers is >= 2 and <= 32 && MaximumPlayers >= MinimumPlayers && MaximumPlayers <= 32 &&
        MaximumShips is >= 1 and <= 8 && (Mode != NavalExpeditionMode.Fleet || MaximumShips >= 2) &&
        RequiredSailingLevel is >= 1 and <= 200 && TimeLimitMinutes is >= 5 and <= 180 && ReconnectGraceSeconds is >= 30 and <= 600 &&
        SailingExperienceReward is >= 0 and <= 1000000 && RewardQuantity is >= 0 and <= 1000000 &&
        (RewardItemId == Guid.Empty ? RewardQuantity == 0 : RewardQuantity > 0) &&
        Stages != null && Stages.Length <= 16 && (!Enabled || Stages.Length > 0) &&
        Stages.All(s => s?.IsValid(width, height) == true) && Stages.Select(s => s.Id).Distinct().Count() == Stages.Length;
}

// JSON view models keep the request surface limited to actions and identifiers.
public sealed class NavalExpeditionView
{
    public string Message { get; set; } = "";
    public NavalExpeditionDefinition[] Available { get; set; } = [];
    public Guid RunId { get; set; }
    public Guid LeaderId { get; set; }
    public NavalExpeditionDefinition? Definition { get; set; }
    public NavalExpeditionStatus Status { get; set; }
    public NavalExpeditionMemberView[] Members { get; set; } = [];
    public int StageIndex { get; set; }
    public int Progress { get; set; }
    public int Required { get; set; }
    public int RemainingSeconds { get; set; }
    public string StageMapName { get; set; } = "";
    public string StageTargetName { get; set; } = "";
    public string RewardItemName { get; set; } = "";
    public bool CanStart { get; set; }
    public bool CanClaim { get; set; }
}

public sealed class NavalExpeditionMemberView
{
    public Guid PlayerId { get; set; }
    public string Name { get; set; } = "";
    public Guid ShipId { get; set; }
    public bool Ready { get; set; }
    public bool Present { get; set; }
    public bool Claimed { get; set; }
}
