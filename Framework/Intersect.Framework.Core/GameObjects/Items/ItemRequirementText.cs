using Intersect.Framework.Core.GameObjects.Conditions;
using Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.QualityOfLife;

namespace Intersect.Framework.Core.GameObjects.Items;

public static class ItemRequirementText
{
    public static IEnumerable<(string Caption, string Value)> Rows(ConditionLists? requirements, string language)
    {
        string T(string text) => QualityOfLifeText.Get(text, language);
        var lists = requirements?.Lists ?? [];
        for (var i = 0; i < lists.Count; i++)
        {
            var list = lists[i];
            if (list.Conditions.Count == 0) continue;
            yield return (lists.Count == 1 ? T("Requirements (all)") : string.Format(T("Requirements (alternative {0})"), i + 1), "");
            var other = false;
            foreach (var condition in list.Conditions)
            {
                string caption, value;
                switch (condition)
                {
                    case LevelOrStatCondition { ComparingLevel: true } level:
                        caption = T("Character level"); value = Compare(level.Comparator) + " " + level.Value;
                        break;
                    case WeaponMasteryLevelCondition mastery:
                        var name = string.IsNullOrWhiteSpace(mastery.MasteryName) ? mastery.MasteryKey switch
                        {
                            "archery" => "Archery", "swords" => "Swordsmanship", "daggers" => "Daggers",
                            "axes" => "Axes & Hammers", "magic" => "Staves & Magic", "whips" => "Whips", _ => mastery.MasteryKey,
                        } : mastery.MasteryName;
                        caption = T("Weapon mastery") + " · " + T(name);
                        value = Compare(mastery.Comparator) + " " + mastery.Value;
                        break;
                    default: other = true; continue;
                }
                yield return (condition.Negated ? string.Format(T("NOT {0}"), caption) : caption, value);
            }
            if (other) yield return (T("Other conditions apply"), "");
        }
    }

    private static string Compare(VariableComparator comparator) => comparator switch
    {
        VariableComparator.Equal => "=", VariableComparator.GreaterOrEqual => ">=", VariableComparator.LesserOrEqual => "<=",
        VariableComparator.Greater => ">", VariableComparator.Less => "<", VariableComparator.NotEqual => "!=", _ => "?",
    };
}
