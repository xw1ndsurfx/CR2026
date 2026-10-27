using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Framework.Core.Archery;
using Intersect.Framework.Core.GameObjects.Conditions;
using Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets.Server;
using Intersect.Server.Archery;
using Intersect.Server.Entities;
using Intersect.Server.Entities.Events;
using NUnit.Framework;

namespace Intersect.Tests.Server;

public sealed partial class RoyalCombatTests
{
    [Test]
    public void ConsecutiveKillsGrantTheFullAwardToTheRecordedWeaponOnceEach()
    {
        using var world = new World();
        var previous = RoyalCombatRuntime.ProgressPath;
        var path = Path.Combine(Path.GetTempPath(), "mastery-kills-" + Guid.NewGuid().ToString("N") + ".db");
        RoyalCombatRuntime.ProgressPath = path;
        var weapon = new ItemDescriptor(Guid.NewGuid());
        var swords = new MasteryCategory { Key = "swords", Name = "Swordsmanship", WeaponIds = [weapon.Id] };
        Config.SetValue(null, new RoyalCombatConfiguration { Masteries = [swords] });
        typeof(Player).GetField("_masteryExperience", Hidden)!.SetValue(world.Player, new Dictionary<string, long>());
        var award = typeof(Player).GetMethod("AwardWeaponMastery", Hidden)!;
        try
        {
            for (var i = 0; i < 25; i++)
            {
                var npc = (Npc)RuntimeHelpers.GetUninitializedObject(typeof(Npc)); npc.Id = Guid.NewGuid(); GC.SuppressFinalize(npc);
                world.Player.RecordMasteryHit(npc, weapon);
                award.Invoke(world.Player, [npc, 6000L]);
                award.Invoke(world.Player, [npc, 6000L]);
            }
            Assert.That(RoyalCombatRuntime.Load(world.Player.Id)["swords"], Is.EqualTo(150000L), "No fixed five-XP reward, 5000-XP ceiling, kill cooldown or minute limit");
            Assert.That(RoyalCombatRuntime.Award(world.Player.Id, swords, long.MaxValue), Is.EqualTo(swords.Maximum), "Large rewards clamp without overflow");
        }
        finally { RoyalCombatRuntime.ProgressPath = previous; if (File.Exists(path)) File.Delete(path); }
    }

    [Test]
    public void MasteryConditionWorksInItemRequirementsAndRoundTripsAsNativeConditions()
    {
        using var world = new World();
        var swords = new MasteryCategory { Key = "swords", Name = "Swordsmanship" };
        Config.SetValue(null, new RoyalCombatConfiguration { Masteries = [swords] });
        typeof(Player).GetField("_masteryExperience", Hidden)!.SetValue(world.Player, new Dictionary<string, long> { ["swords"] = 100 });
        var condition = new WeaponMasteryLevelCondition { MasteryKey = "swords", Value = 2 };
        Assert.That(Conditions.MeetsCondition(condition, world.Player, null!, null!), Is.True);
        condition.Value = 3; Assert.That(Conditions.MeetsCondition(condition, world.Player, null!, null!), Is.False);
        var restricted = new ItemDescriptor(Guid.NewGuid()) { ItemType = ItemType.Equipment, EquipmentSlot = Options.Instance.Equipment.WeaponSlot };
        var restriction = new ConditionList(); restriction.Conditions.Add(condition); restricted.UsageRequirements.Lists.Add(restriction);
        var equipment = world.Player.Equipment.ToArray();
        Assert.DoesNotThrow(() => world.Player.EquipItem(restricted, 0));
        Assert.That(world.Player.Equipment, Is.EqualTo(equipment), "Direct/event equipping must not bypass mastery requirements");
        condition.Comparator = VariableComparator.Less; Assert.That(Conditions.MeetsCondition(condition, world.Player, null!, null!), Is.True);
        condition.MasteryKey = "deleted"; condition.Negated = true;
        Assert.That(Conditions.MeetsCondition((Condition)condition, world.Player, null!, null!), Is.False, "Missing categories must fail closed even with negation");
        var list = new ConditionLists(); var group = new ConditionList(); group.Conditions.Add(condition); list.Lists.Add(group);
        var restored = new ConditionLists(list.Data());
        Assert.That(restored.Lists.Single().Conditions.Single(), Is.TypeOf<WeaponMasteryLevelCondition>());
        Assert.That(((WeaponMasteryLevelCondition)restored.Lists.Single().Conditions.Single()).MasteryKey, Is.EqualTo("deleted"));
    }

    [Test]
    public void CampfireTablesMaterializeNewRecipesAndFilterTheirRequirements()
    {
        using var world = new World();
        var recipe = new Intersect.Framework.Core.GameObjects.Crafting.CraftingRecipeDescriptor(Guid.NewGuid())
        { Campfire = new() { FireAnimationId = Guid.NewGuid(), AshesAnimationId = Guid.NewGuid() } };
        Intersect.Framework.Core.GameObjects.Crafting.CraftingRecipeDescriptor.Lookup.Set(recipe.Id, recipe);
        try
        {
            var table = new Intersect.Framework.Core.GameObjects.Crafting.CraftingTableDescriptor(Guid.NewGuid()) { CampfireCreator = true };
            var visible = Intersect.Server.Networking.PacketSender.FilterCraftingTable(world.Player, table);
            Assert.That(visible.Crafts, Does.Contain(recipe.Id));
            Assert.That(visible.CampfireCreator, Is.False);
            var requirements = new ConditionList();
            requirements.Conditions.Add(new WeaponMasteryLevelCondition { MasteryKey = "missing", Value = 2 });
            recipe.CraftingRequirements.Lists.Add(requirements);
            var filtered = Intersect.Server.Networking.PacketSender.FilterCraftingTable(world.Player, table);
            Assert.That(filtered.AvailableCrafts(), Does.Not.Contain(recipe.Id));
            Assert.That(table.CampfireCreator, Is.True);
            Assert.That(table.Crafts, Is.Empty, "Filtering must not mutate the authored table");
        }
        finally { Intersect.Framework.Core.GameObjects.Crafting.CraftingRecipeDescriptor.Lookup.DeleteAt(recipe.Id); }
    }

    [Test]
    public void MasteryHudUsesClampedActualGainAndNativeLevelProgress()
    {
        var swords = new MasteryCategory { Key = "swords", Name = "Swordsmanship", Cap = 3 };
        var progress = MasteryProgress.Create(swords, 75, 125);
        Assert.That(progress.ExperienceGained, Is.EqualTo(50)); Assert.That(progress.Level, Is.EqualTo(2));
        Assert.That(progress.ExperienceIntoLevel, Is.EqualTo(25)); Assert.That(progress.ExperienceRequiredForLevel, Is.EqualTo(400));
        Assert.That(progress.LeveledUp, Is.True);
        var maximum = MasteryProgress.Create(swords, 475, 6000);
        Assert.That(maximum.ExperienceGained, Is.EqualTo(25)); Assert.That(maximum.MaximumLevelReached, Is.True);
        Assert.That(maximum.Percentage, Is.EqualTo(100));
    }
}
