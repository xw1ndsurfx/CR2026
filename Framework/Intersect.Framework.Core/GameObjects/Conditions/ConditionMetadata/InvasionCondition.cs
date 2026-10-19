namespace Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;

public enum InvasionConditionMode
{
    Active, Preparing, PlayerParticipating, WaveAtLeast,
    VictoriesAtLeast, ParticipationsAtLeast, WavesClearedAtLeast, DefeatsAtLeast,
}

public partial class InvasionCondition : Condition
{
    public override ConditionType Type { get; } = ConditionType.Invasion;
    public Guid InvasionId { get; set; }
    public InvasionConditionMode Mode { get; set; }
    public int Amount { get; set; } = 1;
}
