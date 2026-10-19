using Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;
using Intersect.Framework.Core.WorldEvents.Invasions;
namespace Intersect.Editor.Localization;
public static partial class Strings
{
    public static string GetEventConditionalDesc(InvasionCondition condition) =>
        $"Invasion: {InvasionConfiguration.Instance.Invasions.FirstOrDefault(i => i.Id == condition.InvasionId)?.Name ?? (condition.InvasionId == Guid.Empty ? "Any / all invasions" : condition.InvasionId.ToString())} | {condition.Mode} | minimum {condition.Amount}";
}
