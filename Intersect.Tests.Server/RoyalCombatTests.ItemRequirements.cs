using Intersect.Framework.Core.Archery;
using Intersect.Framework.Core.GameObjects.Conditions;
using Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Server.Archery;
using Intersect.Server.Entities;
using Intersect.Server.Entities.Events;
using NUnit.Framework;

namespace Intersect.Tests.Server;

public sealed partial class RoyalCombatTests
{
    [Test]
    public void WeaponEquipRequirementsDemandBothCharacterAndMatchingMasteryLevel()
    {
        using var world = new World();
        world.Player.Level = 25;
        typeof(Player).GetField("_masteryExperience", Hidden)!.SetValue(world.Player,
            new Dictionary<string, long> { ["swords"] = 100_000, ["whips"] = 0 });
        var requirements = new ConditionLists();
        requirements.Lists.Add(new ConditionList { Conditions = [
            new LevelOrStatCondition { ComparingLevel = true, Comparator = VariableComparator.GreaterOrEqual, Value = 25 },
            new WeaponMasteryLevelCondition { MasteryKey = "whips", Value = 2 },
        ] });
        Assert.That(Conditions.MeetsConditionLists(requirements, world.Player, null), Is.False,
            "Sword mastery cannot satisfy a Whip requirement.");
        typeof(Player).GetField("_masteryExperience", Hidden)!.SetValue(world.Player,
            new Dictionary<string, long> { ["whips"] = 100 });
        Assert.That(Conditions.MeetsConditionLists(requirements, world.Player, null), Is.True);
        world.Player.Level = 24;
        Assert.That(Conditions.MeetsConditionLists(requirements, world.Player, null), Is.False,
            "Mastery does not bypass character level.");
    }

    [Test]
    public void CharacterMasteryProfileReportsTheEffectiveDamageSpeedAndCriticalBonuses()
    {
        using var database = new ProgressDatabase(); using var world = new World();
        var category = RoyalCombatRuntime.Current.Masteries.Single(c => c.Key == "whips");
        category.DamagePercentPerLevel = 1.25;
        category.Talents = [new MasteryTalent { DamagePercentPerRank = 1, AttackSpeedPercentPerRank = .5, CritChancePerRank = 1 }];
        var character = Guid.NewGuid(); RoyalCombatRuntime.Award(character, category, 100);
        Assert.That(RoyalCombatRuntime.BuyTalent(character, category, "steady-aim", Guid.NewGuid()), Is.True);
        var profile = RoyalCombatRuntime.PublicMasteries(character).Single(m => m.Key == "whips");
        Assert.That(profile.Level, Is.EqualTo(2));
        Assert.That(profile.DamageBonusPercent, Is.EqualTo(2.25));
        Assert.That(profile.AttackSpeedBonusPercent, Is.EqualTo(.5));
        Assert.That(profile.CritChanceBonus, Is.EqualTo(1));
    }
}
