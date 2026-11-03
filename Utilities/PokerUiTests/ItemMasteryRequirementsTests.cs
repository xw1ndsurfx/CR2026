using System.Runtime.CompilerServices;
using Intersect.Framework.Core.Archery;
using Intersect.Framework.Core.GameObjects.Conditions;
using Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.QualityOfLife;

internal static class ItemMasteryRequirementsTests
{
    [ModuleInitializer] internal static void Run()
    {
        try { Verify(); } catch (Exception error) { System.Console.Error.WriteLine(error); Environment.Exit(1); }
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Verify()
    {
        var configuration = new RoyalCombatConfiguration();
        Check(configuration.Masteries.Single(m => m.Key == "whips").Name == "Whips", "Whips available by default");
        var sword = configuration.Masteries.Single(m => m.Key == "swords");
        var weapon = Guid.NewGuid(); sword.WeaponIds = [weapon]; sword.DamagePercentPerLevel = 1.25;
        configuration.Masteries = configuration.Masteries.Where(m => m.Key != "whips").ToArray();
        var upgraded = RoyalCombatConfiguration.FromJson(configuration.ToJson());
        Check(upgraded.Masteries.Any(m => m.Key == "whips") && upgraded.Masteries.Single(m => m.Key == "swords").WeaponIds.Single() == weapon,
            "Old configurations gain Whips without losing mappings");
        Check(upgraded.Masteries.Single(m => m.Key == "swords").DamagePercentPerLevel == 1.25, "Custom balance survives upgrade");
        upgraded.Masteries = upgraded.Masteries.Where(m => m.Key != "whips").ToArray();
        Check(!RoyalCombatConfiguration.FromJson(upgraded.ToJson()).Masteries.Any(m => m.Key == "whips"), "An explicit later deletion is respected");
        var requirements = new ConditionLists();
        requirements.Lists.Add(new ConditionList { Conditions = [
            new LevelOrStatCondition { ComparingLevel = true, Comparator = VariableComparator.GreaterOrEqual, Value = 25 },
            new WeaponMasteryLevelCondition { MasteryKey = "whips", MasteryName = "Whips", Value = 12 },
        ] });
        var restored = new ConditionLists(requirements.Data());
        Check(restored.Lists.Count == 1 && restored.Lists[0].Conditions.Count == 2, "Both requirements survive serialization in one AND branch");
        foreach (var language in new[] { "en", "fr", "es" })
        {
            var rows = ItemRequirementText.Rows(restored, language).ToArray();
            Check(rows.Length == 3 && rows[1].Value == ">= 25" && rows[2].Value == ">= 12", "Both actual prerequisite values are displayed");
            Check(rows[2].Caption.Contains(QualityOfLifeText.Get("Whips", language)), "Weapon name localized");
            Check(QualityOfLifeText.Get("Mastery damage bonus", language).Length > 0, "Stats label translated");
        }
        Check(QualityOfLifeText.Get("Whips", "fr") == "Fouets" && QualityOfLifeText.Get("Whips", "es") == "Látigos", "French/Spanish weapon labels");
        Check(QualityOfLifeText.Get("Whips", "unknown") == "Whips", "Unknown language falls back to English");
        requirements.Lists.Add(new ConditionList { Conditions = [new WeaponMasteryLevelCondition { MasteryKey = "swords", Value = 5, Negated = true }] });
        var alternatives = ItemRequirementText.Rows(requirements, "en").ToArray();
        Check(alternatives.Count(r => r.Caption.StartsWith("Requirements (alternative")) == 2 && alternatives.Any(r => r.Caption.StartsWith("NOT ")),
            "Tooltip preserves OR alternatives and negation");
        System.Console.WriteLine("PASS item mastery requirements: migration, mapping preservation, AND/OR semantics, serialized values, three locales and English fallback");
    }
}
