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
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void NormalWhipAttackCreditsConfiguredMasteryThroughActualNpcDeath(int mode)
    {
        using var world = new World();
        var oldPath = RoyalCombatRuntime.ProgressPath;
        var path = Path.Combine(Path.GetTempPath(), "whip-kill-" + Guid.NewGuid().ToString("N") + ".db");
        var formulas = typeof(Intersect.Server.General.Formulas).GetField("_formulas", BindingFlags.Static | BindingFlags.NonPublic)!;
        var oldFormulas = formulas.GetValue(null);
        formulas.SetValue(null, new Intersect.Server.General.Formulas { PhysicalDamage = "BaseDamage", MagicDamage = "BaseDamage", TrueDamage = "BaseDamage" });
        RoyalCombatRuntime.ProgressPath = path;
        var whip = new ItemDescriptor(Guid.NewGuid()) { Name = "Whip", ItemType = ItemType.Equipment,
            EquipmentSlot = Options.Instance.Equipment.WeaponSlot, Damage = mode == 2 ? 0 : 100, DamageType = (int)Intersect.Enums.DamageType.True };
        var category = new MasteryCategory { Key = "whips", Name = "Whips", WeaponIds = [whip.Id] };
        Config.SetValue(null, new RoyalCombatConfiguration { Masteries = [category] });
        ItemDescriptor.Lookup.Set(whip.Id, whip);
        world.Map.SurroundingMaps = []; world.Map.SurroundingMapIds = [];
        var instance = new Intersect.Server.Maps.MapInstance(world.Map, Guid.Empty, world.Player);
        var instances = (System.Collections.IDictionary)typeof(Intersect.Server.Maps.MapController).GetField("mInstances", Hidden)!.GetValue(world.Map)!;
        instances[Guid.Empty] = instance;
        world.Player.Client = null; world.Player.Level = Options.Instance.Player.MaxLevel;
        world.Player.Items[0].Set(new Intersect.Server.Database.Item(whip.Id, 1));
        world.Player.Equipment[Options.Instance.Equipment.WeaponSlot] = 0;
        typeof(Player).GetField("_masteryExperience", Hidden)!.SetValue(world.Player, new Dictionary<string, long>());
        var descriptor = new Intersect.Framework.Core.GameObjects.NPCs.NPCDescriptor(Guid.NewGuid()) { Experience = 123, ExperienceMaximum = 123 };
        descriptor.MaxVitals[(int)Intersect.Enums.Vital.Health] = 50;
        var target = new Npc(descriptor) { MapId = world.Map.Id, MapInstanceId = Guid.Empty, X = 4, Y = 5 };
        GC.SuppressFinalize(target); instance.AddEntity(target);
        var effect = new Intersect.GameObjects.SpellDescriptor(Guid.NewGuid());
        effect.SpellType = Intersect.Enums.SpellType.CombatSpell;
        effect.Combat.TargetType = Intersect.Enums.SpellTargetType.Single;
        effect.Combat.DamageType = (int)Intersect.Enums.DamageType.True;
        effect.Combat.VitalDiff[(int)Intersect.Enums.Vital.Health] = 100;
        Intersect.GameObjects.SpellDescriptor.Lookup.Set(effect.Id, effect);
        try
        {
            if (mode == 3) world.Player.TryAttack(target, effect);
            else if (mode > 0) world.Player.TryAttack(target, new Intersect.GameObjects.ProjectileDescriptor(Guid.NewGuid()) { SpellId = mode == 2 ? effect.Id : Guid.Empty }, null, whip, Intersect.Enums.Direction.Down);
            else world.Player.TryAttack(target);
            Assert.That(target.IsDead, Is.True, "The normal whip attack kills the NPC through the real combat path.");
            Assert.That(RoyalCombatRuntime.Load(world.Player.Id).GetValueOrDefault("whips"), Is.EqualTo(mode == 3 ? 0 : 123));
        }
        finally
        {
            instances.Clear(); Intersect.GameObjects.SpellDescriptor.Lookup.DeleteAt(effect.Id); ItemDescriptor.Lookup.DeleteAt(whip.Id); formulas.SetValue(null, oldFormulas);
            RoyalCombatRuntime.ProgressPath = oldPath; if (File.Exists(path)) File.Delete(path);
        }
    }

    [Test]
    public void MasteryLevelUpBroadcastsOneConfiguredAnimationOnThePlayerAfterCommittedExperience()
    {
        using var world = new World();
        var oldPath = RoyalCombatRuntime.ProgressPath;
        var oldBatch = Options.Instance.Packets.BatchAnimationPackets;
        var path = Path.Combine(Path.GetTempPath(), "mastery-animation-" + Guid.NewGuid().ToString("N") + ".db");
        RoyalCombatRuntime.ProgressPath = path; Options.Instance.Packets.BatchAnimationPackets = true;
        var animation = new Intersect.Framework.Core.GameObjects.Animations.AnimationDescriptor(Guid.NewGuid());
        Intersect.Framework.Core.GameObjects.Animations.AnimationDescriptor.Lookup.Set(animation.Id, animation);
        var weapon = new ItemDescriptor(Guid.NewGuid());
        var category = new MasteryCategory { Key = "swords", Cap = 4, WeaponIds = [weapon.Id], LevelUpAnimationId = animation.Id };
        Config.SetValue(null, new RoyalCombatConfiguration { Masteries = [category] });
        typeof(Player).GetField("_masteryExperience", Hidden)!.SetValue(world.Player, new Dictionary<string, long>());
        var instance = (Intersect.Server.Maps.MapInstance)RuntimeHelpers.GetUninitializedObject(typeof(Intersect.Server.Maps.MapInstance));
        GC.SuppressFinalize(instance);
        var batch = new Intersect.Server.Maps.MapAnimations();
        typeof(Intersect.Server.Maps.MapInstance).GetField("mMapAnimations", Hidden)!.SetValue(instance, batch);
        var instances = (IDictionary<Guid, Intersect.Server.Maps.MapInstance>)typeof(Intersect.Server.Maps.MapController).GetField("mInstances", Hidden)!.GetValue(world.Map)!;
        instances[world.Player.MapInstanceId] = instance;
        var award = typeof(Player).GetMethod("AwardWeaponMastery", Hidden)!;
        void Kill(long experience)
        {
            var npc = (Npc)RuntimeHelpers.GetUninitializedObject(typeof(Npc)); npc.Id = Guid.NewGuid(); GC.SuppressFinalize(npc);
            world.Player.RecordMasteryHit(npc, weapon);
            award.Invoke(world.Player, [npc, experience]);
            award.Invoke(world.Player, [npc, experience]); // Duplicate notification must not replay it.
        }
        try
        {
            Kill(99); Assert.That(batch.mAnimations, Is.Empty);
            Kill(1);
            Assert.That(batch.mAnimations, Has.Count.EqualTo(1));
            var packet = batch.mAnimations.Single();
            Assert.That(packet.AnimationId, Is.EqualTo(animation.Id));
            Assert.That(packet.EntityId, Is.EqualTo(world.Player.Id));
            Assert.That(packet.MapId, Is.EqualTo(world.Player.MapId));
            Assert.That(packet.TargetType, Is.EqualTo(1));
            Assert.That(RoyalCombatRuntime.Load(world.Player.Id)[category.Key], Is.EqualTo(100));
            Kill(1300); Assert.That(batch.mAnimations, Has.Count.EqualTo(2), "Multiple levels in one reward play once");
            Kill(100); Assert.That(batch.mAnimations, Has.Count.EqualTo(2), "Capped mastery does not replay the animation");
        }
        finally
        {
            instances.Clear(); Options.Instance.Packets.BatchAnimationPackets = oldBatch;
            Intersect.Framework.Core.GameObjects.Animations.AnimationDescriptor.Lookup.DeleteAt(animation.Id);
            RoyalCombatRuntime.ProgressPath = oldPath; if (File.Exists(path)) File.Delete(path);
        }
    }

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
