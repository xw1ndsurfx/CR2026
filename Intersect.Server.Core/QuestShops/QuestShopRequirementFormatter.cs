using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Conditions;
using Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.PlayerClass;
using Intersect.Framework.Core.GameObjects.Spells;
using Intersect.Framework.Core.GameObjects.Variables;
using Intersect.Framework.Core.Professions;
using Intersect.GameObjects;
using Intersect.Server.Entities;
using Intersect.Server.Entities.Events;

namespace Intersect.Server.QuestShops;

internal static class QuestShopRequirementFormatter
{
    internal static string Build(Player player, QuestDescriptor quest)
    {
        var lists = quest.Requirements?.Lists ?? [];
        if (lists.Count == 0)
            return "No prerequisites";

        var groups = new List<string>();
        for (var index = 0; index < lists.Count; index++)
        {
            var list = lists[index];
            if ((list.Conditions?.Count ?? 0) == 0)
                continue;

            var lines = list.Conditions
                .Select(condition =>
                {
                    var met = Conditions.MeetsCondition(condition, player, null, quest);
                    return $"{(met ? "✓" : "✗")} {Format(condition)}";
                })
                .ToArray();

            var heading = string.IsNullOrWhiteSpace(list.Name)
                ? $"Requirement set {index + 1}"
                : list.Name.Trim();

            groups.Add($"{heading}: {string.Join(" • ", lines)}");
        }

        return groups.Count switch
        {
            0 => "No prerequisites",
            1 => groups[0],
            _ => string.Join("  OR  ", groups),
        };
    }

    private static string Format(Condition condition)
    {
        var text = condition switch
        {
            VariableIsCondition value => FormatVariable(value),
            HasItemCondition value => value.UseVariable
                ? $"Have {ItemDescriptor.GetName(value.ItemId)} (quantity from {GetVariableName(value.VariableType, value.VariableId)})"
                : $"Have {Math.Max(0, value.Quantity):N0} x {ItemDescriptor.GetName(value.ItemId)}",
            IsItemEquippedCondition value => $"Equip {ItemDescriptor.GetName(value.ItemId)}",
            ClassIsCondition value => $"Class is {ClassDescriptor.GetName(value.ClassId)}",
            KnowsSpellCondition value => $"Know spell {SpellDescriptor.GetName(value.SpellId)}",
            LevelOrStatCondition value =>
                $"{(value.ComparingLevel ? "Level" : value.Stat.ToString())} {Comparator(value.Comparator)} {value.Value:N0}",
            AccessIsCondition value => $"Access is {value.Access}",
            TimeBetweenCondition => "Meet the configured time requirement",
            CanStartQuestCondition value => $"Can start quest {QuestDescriptor.GetName(value.QuestId)}",
            QuestInProgressCondition value => $"Quest in progress: {QuestDescriptor.GetName(value.QuestId)}",
            QuestCompletedCondition value => $"Complete quest: {QuestDescriptor.GetName(value.QuestId)}",
            GenderIsCondition value => $"Gender is {value.Gender}",
            MapIsCondition value => $"Be on map {MapDescriptor.GetName(value.MapId)}",
            HasFreeInventorySlots value => value.UseVariable
                ? $"Have free inventory slots from {GetVariableName(value.VariableType, value.VariableId)}"
                : $"Have {Math.Max(0, value.Quantity):N0} free inventory slot(s)",
            InGuildWithRank value => $"Be in a guild with rank {value.Rank:N0} or higher",
            CheckEquippedSlot value => $"Equipment requirement: {value.Name}",
            CombatCondition => "Be in combat",
            ProfessionLevelCondition value =>
                $"Profession {ProfessionConfiguration.Instance.Find(value.ProfessionId)?.Name ?? "Unknown"} level {Comparator(value.Comparator)} {value.Value:N0}",
            PremiumStatusCondition value => value.Mode switch
            {
                PremiumConditionMode.Active => "Premium active",
                PremiumConditionMode.Inactive => "Premium inactive",
                PremiumConditionMode.RemainingDaysAtLeast => $"Premium remaining at least {value.RemainingDays:N0} day(s)",
                _ => "Premium requirement",
            },
            _ => condition.Type.ToString(),
        };

        return condition.Negated ? $"NOT ({text})" : text;
    }

    private static string FormatVariable(VariableIsCondition condition)
    {
        var left = GetVariableName(condition.VariableType, condition.VariableId);
        return condition.Comparison switch
        {
            BooleanVariableComparison value =>
                $"{left} {(value.ComparingEqual ? "==" : "!=")} {FormatBooleanValue(value)}",
            IntegerVariableComparison value when value.Comparator == VariableComparator.Between =>
                $"{left} between {value.Value:N0} and {value.MaxValue:N0}",
            IntegerVariableComparison value =>
                $"{left} {Comparator(value.Comparator)} {FormatIntegerValue(value)}",
            StringVariableComparison value =>
                $"{left} {StringComparator(value.Comparator)} \"{value.Value}\"",
            _ => $"{left} matches configured value",
        };
    }

    private static string FormatBooleanValue(BooleanVariableComparison value) =>
        value.CompareVariableId == Guid.Empty
            ? value.Value.ToString()
            : GetVariableName(value.CompareVariableType, value.CompareVariableId);

    private static string FormatIntegerValue(IntegerVariableComparison value) =>
        value.CompareVariableId == Guid.Empty
            ? value.Value.ToString("N0")
            : GetVariableName(value.CompareVariableType, value.CompareVariableId);

    private static string GetVariableName(VariableType type, Guid id) =>
        type switch
        {
            VariableType.PlayerVariable => PlayerVariableDescriptor.GetName(id),
            VariableType.ServerVariable => ServerVariableDescriptor.GetName(id),
            VariableType.GuildVariable => GuildVariableDescriptor.GetName(id),
            VariableType.UserVariable => UserVariableDescriptor.GetName(id),
            _ => "Variable",
        };

    private static string Comparator(VariableComparator comparator) =>
        comparator switch
        {
            VariableComparator.Equal => "==",
            VariableComparator.GreaterOrEqual => ">=",
            VariableComparator.LesserOrEqual => "<=",
            VariableComparator.Greater => ">",
            VariableComparator.Less => "<",
            VariableComparator.NotEqual => "!=",
            VariableComparator.Between => "between",
            _ => "?",
        };

    private static string StringComparator(StringVariableComparator comparator) =>
        comparator switch
        {
            StringVariableComparator.Equal => "==",
            StringVariableComparator.Contains => "contains",
            _ => comparator.ToString(),
        };
}
