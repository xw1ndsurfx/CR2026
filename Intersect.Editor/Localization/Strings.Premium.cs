using Intersect.Framework.Core.GameObjects.Conditions;
using Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;
namespace Intersect.Editor.Localization;

public static partial class Strings
{
    public static string GetEventConditionalDesc(Condition condition) => condition.Type.ToString();
    public static string GetEventConditionalDesc(PremiumStatusCondition condition) => condition.Mode switch
    {
        PremiumConditionMode.Active => "Premium is active",
        PremiumConditionMode.Inactive => "Premium is inactive / expired",
        PremiumConditionMode.RemainingDaysAtLeast => $"Premium remaining at least {condition.RemainingDays} day(s)",
        _ => "Premium status (invalid mode)",
    };
}
