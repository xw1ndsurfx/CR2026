using Intersect.GameObjects;

namespace Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;

/// <summary>Compares the current game clock with the configured daylight phases.</summary>
public partial class TimePhaseCondition : Condition
{
    public override ConditionType Type { get; } = ConditionType.TimePhase;

    public DayPhase Phase { get; set; } = DayPhase.Night;
}
