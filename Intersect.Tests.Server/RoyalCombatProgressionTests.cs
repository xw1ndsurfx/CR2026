using System.Reflection;
using Intersect.Framework.Core.Archery;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.GameObjects;
using Intersect.Network.Packets.Client;
using Intersect.Server.Archery;
using Intersect.Server.Database;
using NUnit.Framework;

namespace Intersect.Tests.Server;
public sealed partial class RoyalCombatTests
{
    private sealed class ProgressDatabase : IDisposable
    {
        private readonly string _previous = RoyalCombatRuntime.ProgressPath;
        private readonly string _path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
        internal ProgressDatabase() => RoyalCombatRuntime.ProgressPath = _path;
        public void Dispose() { RoyalCombatRuntime.ProgressPath = _previous; if (File.Exists(_path)) File.Delete(_path); }
    }
    [Test]
    public void TalentPurchaseIsIdempotentAndCannotSpendTheSamePointConcurrently()
    {
        using var db = new ProgressDatabase(); var character = Guid.NewGuid();
        var category = new MasteryCategory { Talents = [new() { DamagePercentPerRank = 1 }] };
        RoyalCombatRuntime.Award(character, category, 100);
        var request = Guid.NewGuid();
        Assert.That(RoyalCombatRuntime.BuyTalent(character, category, "steady-aim", request), Is.True);
        Assert.That(RoyalCombatRuntime.BuyTalent(character, category, "steady-aim", request), Is.True);
        Assert.That(RoyalCombatRuntime.LoadRanks(character)["archery"]["steady-aim"], Is.EqualTo(1));
        Assert.That(RoyalCombatRuntime.BuyTalent(character, category, "steady-aim", Guid.NewGuid()), Is.False);
        var other = Guid.NewGuid(); RoyalCombatRuntime.Award(other, category, 100);
        var attempts = Enumerable.Range(0, 12).Select(_ => Task.Run(() => RoyalCombatRuntime.BuyTalent(other, category, "steady-aim", Guid.NewGuid()))).ToArray();
        Task.WaitAll(attempts);
        Assert.That(attempts.Count(t => t.Result), Is.EqualTo(1));
        Assert.That(RoyalCombatRuntime.LoadRanks(other)["archery"]["steady-aim"], Is.EqualTo(1));
    }
    [Test]
    public void PrerequisiteAndLevelChecksUsePersistedExperienceAndRanks()
    {
        using var db = new ProgressDatabase(); var character = Guid.NewGuid();
        var category = new MasteryCategory { Talents = [new() { DamagePercentPerRank = 1 },
            new() { Key = "eagle-eye", RequiredLevel = 3, Prerequisite = "steady-aim", PrerequisiteRank = 2, CritChancePerRank = 1 }] };
        RoyalCombatRuntime.Award(character, category, 1500);
        Assert.That(RoyalCombatRuntime.BuyTalent(character, category, "eagle-eye", Guid.NewGuid()), Is.False);
        Assert.That(RoyalCombatRuntime.BuyTalent(character, category, "steady-aim", Guid.NewGuid()), Is.True);
        Assert.That(RoyalCombatRuntime.BuyTalent(character, category, "steady-aim", Guid.NewGuid()), Is.True);
        Assert.That(RoyalCombatRuntime.BuyTalent(character, category, "eagle-eye", Guid.NewGuid()), Is.True);
        Assert.That(RoyalCombatRuntime.LoadRanks(character)["archery"]["eagle-eye"], Is.EqualTo(1));
        Assert.That(RoyalCombatRuntime.BuyTalent(character, category, "steady-aim", Guid.Empty), Is.False);
    }
    [Test]
    public void UnconfiguredActiveTalentsCannotConsumePoints()
    {
        using var db = new ProgressDatabase(); var character = Guid.NewGuid();
        var category = new MasteryCategory { Talents = [new()] };
        RoyalCombatRuntime.Award(character, category, 1500);
        Assert.That(RoyalCombatRuntime.BuyTalent(character, category, "steady-aim", Guid.NewGuid()), Is.False);
        Assert.That(RoyalCombatRuntime.LoadRanks(character), Is.Empty);
    }
    [Test]
    public void ProjectileAmmoDeductionSpansStacksAndConcurrentLastArrowIsConsumedOnce()
    {
        using var world = new World(); var p = world.Player; Array.Fill(p.Equipment, -1);
        var ammo = new ItemDescriptor(Guid.NewGuid()) { ItemType = ItemType.Ammo, Stackable = true };
        ItemDescriptor.Lookup.Set(ammo.Id, ammo);
        try
        {
            p.Items[0].Set(new Item(ammo.Id, 2)); p.Items[1].Set(new Item(ammo.Id, 4));
            var projectile = new ProjectileDescriptor(Guid.NewGuid()) { AmmoItemId = ammo.Id, AmmoRequired = 5 };
            Assert.That(p.TryConsumeProjectileAmmunition(projectile), Is.True);
            Assert.That(p.FindInventoryItemQuantity(ammo.Id), Is.EqualTo(1));
            Assert.That(p.TryConsumeProjectileAmmunition(projectile), Is.False);
            Assert.That(p.FindInventoryItemQuantity(ammo.Id), Is.EqualTo(1), "Insufficient ammo must not partially drain stacks.");
            projectile.AmmoRequired = 1;
            var shots = Enumerable.Range(0, 12).Select(_ => Task.Run(() => p.TryConsumeProjectileAmmunition(projectile))).ToArray();
            Task.WaitAll(shots); Assert.That(shots.Count(s => s.Result), Is.EqualTo(1));
            Assert.That(p.FindInventoryItemQuantity(ammo.Id), Is.Zero);
            projectile.AmmoRequired = -1; Assert.That(p.TryConsumeProjectileAmmunition(projectile), Is.False);
        }
        finally { ItemDescriptor.Lookup.DeleteAt(ammo.Id); }
    }
    [Test]
    public void SelectedAmmoPersistsAndDoesNotFallBackWhenDepletedOrAcceptAnotherWeapon()
    {
        using var db = new ProgressDatabase(); using var world = new World(); var p = world.Player;
        var wooden = new ItemDescriptor(Guid.NewGuid()) { ItemType = ItemType.Ammo, Stackable = true };
        var fire = new ItemDescriptor(Guid.NewGuid()) { ItemType = ItemType.Ammo, Stackable = true };
        var native = new ProjectileDescriptor(Guid.NewGuid()) { AmmoItemId = wooden.Id, AmmoRequired = 1 };
        var alternate = new ProjectileDescriptor(Guid.NewGuid()) { AmmoItemId = fire.Id, AmmoRequired = 1 };
        var bow = new ItemDescriptor(Guid.NewGuid()) { ItemType = ItemType.Equipment, EquipmentSlot = Options.Instance.Equipment.WeaponSlot, ProjectileId = native.Id };
        foreach (var i in new[] { wooden, fire, bow }) ItemDescriptor.Lookup.Set(i.Id, i);
        foreach (var i in new[] { native, alternate }) ProjectileDescriptor.Lookup.Set(i.Id, i);
        try
        {
            Config.SetValue(null, new RoyalCombatConfiguration { Ammunition = [new() { WeaponId = bow.Id, ProjectileIds = [alternate.Id] }] });
            Array.Fill(p.Equipment, -1); p.Items[0].Set(new Item(bow.Id, 1)); p.Equipment[Options.Instance.Equipment.WeaponSlot] = 0;
            p.Items[1].Set(new Item(fire.Id, 1)); p.Items[2].Set(new Item(wooden.Id, 10));
            Assert.That(p.SelectWeaponAmmo(Guid.NewGuid(), fire.Id), Is.False);
            Assert.That(p.SelectWeaponAmmo(bow.Id, Guid.NewGuid()), Is.False);
            p.HandleArcheryRequest(new() { Action = ArcheryAction.Ammunition, RequestId = Guid.NewGuid() });
            p.HandleArcheryRequest(new() { Action = ArcheryAction.SelectAmmunition, WeaponId = bow.Id, ItemId = fire.Id });
            Assert.That(RoyalCombatRuntime.LoadAmmo(p.Id)[bow.Id], Is.EqualTo(fire.Id));
            Assert.That(p.SelectedWeaponProjectile(bow), Is.SameAs(alternate));
            Assert.That(p.TryConsumeProjectileAmmunition(alternate), Is.True);
            Assert.That(p.SelectedWeaponProjectile(bow), Is.SameAs(alternate), "Never silently spend another ammo type.");
            Assert.That(p.TryConsumeProjectileAmmunition(alternate), Is.False);
            Assert.That(p.FindInventoryItemQuantity(wooden.Id), Is.EqualTo(10));
            p.Equipment[Options.Instance.Equipment.WeaponSlot] = -1;
            Assert.That(p.SelectWeaponAmmo(bow.Id, wooden.Id), Is.False);
        }
        finally
        {
            foreach (var i in new[] { wooden, fire, bow }) ItemDescriptor.Lookup.DeleteAt(i.Id);
            foreach (var i in new[] { native, alternate }) ProjectileDescriptor.Lookup.DeleteAt(i.Id);
        }
    }
    [Test]
    public void ActiveTalentLearnsNativeSpellAndRequiresRankAndCompatibleEquippedWeapon()
    {
        using var db = new ProgressDatabase(); using var world = new World(); var p = world.Player;
        var spell = new SpellDescriptor(Guid.NewGuid()); SpellDescriptor.Lookup.Set(spell.Id, spell);
        var bow = new ItemDescriptor(Guid.NewGuid()) { ItemType = ItemType.Equipment, EquipmentSlot = Options.Instance.Equipment.WeaponSlot };
        ItemDescriptor.Lookup.Set(bow.Id, bow);
        try
        {
            var category = new MasteryCategory { WeaponIds = [bow.Id], Talents = [new() { RankSpellIds = Enumerable.Repeat(spell.Id, 5).ToArray() }] };
            Config.SetValue(null, new RoyalCombatConfiguration { Masteries = [category] });
            Array.Fill(p.Equipment, -1); p.Items[0].Set(new Item(bow.Id, 1)); p.Equipment[Options.Instance.Equipment.WeaponSlot] = 0;
            RoyalCombatRuntime.Award(p.Id, category, 100);
            Assert.That(p.CanUseMasterySpell(spell.Id), Is.False);
            for (var index = 0; index < Options.Instance.Player.MaxSpells; index++) p.Spells[index].Set(new Spell(Guid.NewGuid()));
            p.HandleArcheryRequest(new() { Action = ArcheryAction.BuyTalent, CategoryKey = category.Key, TalentKey = "steady-aim", RequestId = Guid.NewGuid() });
            Assert.That(RoyalCombatRuntime.LoadRanks(p.Id), Is.Empty, "A full native spellbook cannot consume a point.");
            p.Spells[0].Set(Spell.None);
            typeof(Intersect.Server.Entities.Player).GetField("_talentRequestAt", Hidden)!.SetValue(p, 0L);
            p.HandleArcheryRequest(new() { Action = ArcheryAction.BuyTalent, CategoryKey = category.Key, TalentKey = "steady-aim", RequestId = Guid.NewGuid() });
            Assert.That(p.FindSpell(spell.Id), Is.GreaterThanOrEqualTo(0), "The native spellbook is used; no parallel casting engine.");
            Assert.That(p.CanUseMasterySpell(spell.Id), Is.True);
            p.Equipment[Options.Instance.Equipment.WeaponSlot] = -1;
            Assert.That(p.CanUseMasterySpell(spell.Id), Is.False, "Equipment is checked again at native cast validation.");
            Config.SetValue(null, new RoyalCombatConfiguration());
            Assert.That(p.CanUseMasterySpell(spell.Id), Is.False, "Removing the mapping must not turn a mastery-granted spell into a free ordinary spell.");
            Assert.That(RoyalCombatRuntime.LearnedSpells(p.Id), Does.Contain(spell.Id));
        }
        finally { ItemDescriptor.Lookup.DeleteAt(bow.Id); SpellDescriptor.Lookup.DeleteAt(spell.Id); }
    }
    [Test]
    public void PassiveRanksAffectWorldDamageSpeedAndCritOnlyForMappedWeapon()
    {
        using var db = new ProgressDatabase(); using var world = new World();
        var weapon = new ItemDescriptor(Guid.NewGuid());
        var category = new MasteryCategory { WeaponIds = [weapon.Id], Talents = [new() { DamagePercentPerRank = 1, AttackSpeedPercentPerRank = 1, CritChancePerRank = 1 }] };
        Config.SetValue(null, new RoyalCombatConfiguration { Masteries = [category] });
        RoyalCombatRuntime.Award(world.Player.Id, category, 100);
        RoyalCombatRuntime.BuyTalent(world.Player.Id, category, "steady-aim", Guid.NewGuid());
        Assert.That(world.Player.MasteryPassives(weapon), Is.EqualTo((1d, 1d, 1)));
        Assert.That(world.Player.MasteryMultiplier(weapon), Is.EqualTo(1.015).Within(.00001));
        Assert.That(world.Player.MasteryPassives(new ItemDescriptor(Guid.NewGuid())), Is.EqualTo((0d, 0d, 0)));
    }
}
