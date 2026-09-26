using Intersect.Framework.Core.GameObjects.Events;

namespace Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;

public partial class ProfessionLevelCondition : Condition
{
    public override ConditionType Type { get; } = ConditionType.ProfessionLevel;
    public Guid ProfessionId { get; set; }
    public VariableComparator Comparator { get; set; } = VariableComparator.GreaterOrEqual;
    public int Value { get; set; } = 1;
}
