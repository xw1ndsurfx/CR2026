namespace Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;

public enum PremiumConditionMode
{
    Active = 0,
    Inactive = 1,
    RemainingDaysAtLeast = 2,
}

public partial class PremiumStatusCondition : Condition
{
    public override ConditionType Type { get; } = ConditionType.PremiumStatus;

    public PremiumConditionMode Mode { get; set; } = PremiumConditionMode.Active;

    public int RemainingDays { get; set; } = 1;
}
