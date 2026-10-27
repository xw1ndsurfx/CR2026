using Intersect.Framework.Core.GameObjects.Events;

namespace Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;

public sealed class WeaponMasteryLevelCondition : Condition
{
    public override ConditionType Type => ConditionType.WeaponMasteryLevel;
    public string MasteryKey { get; set; } = "";
    public VariableComparator Comparator { get; set; } = VariableComparator.GreaterOrEqual;
    public int Value { get; set; } = 1;
}
