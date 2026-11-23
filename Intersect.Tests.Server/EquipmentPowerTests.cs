using System.Reflection;
using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.GameObjects;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.Entities;
using NUnit.Framework;

namespace Intersect.Tests.Server;

[TestFixture, NonParallelizable]
public sealed class EquipmentPowerTests
{
    private Options? _options;
    private ItemDescriptor _weapon = null!;
    private SpellDescriptor _spell = null!;
    private ProbePlayer _player = null!;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private sealed class ProbePlayer : Player
    {
        public int Casts; public Guid CastId; public bool Reenter;
        public override void CastSpell(Guid id, int slot = -1) { Casts++; CastId = id; if (Reenter) Hit(); }
        public void Hit() => typeof(Player).GetMethod("OnEquipmentWeaponHit", Private)!.Invoke(this, [this]);
        public void Complete() => typeof(Player).GetMethod("UpdateEquipmentSpell", Private)!.Invoke(this, []);
    }
    [SetUp] public void Setup()
    {
        _options = Options.Instance; typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, new Options());
        _spell = new SpellDescriptor(Guid.NewGuid()) { Name = "Equipment test", IgnoreGlobalCooldown = true, CastDuration = 1000 };
        _spell.Combat.TargetType = SpellTargetType.Self; _spell.Combat.Friendly = true; _spell.VitalCost[(int)Vital.Mana] = 4;
        SpellDescriptor.Lookup.Set(_spell.Id, _spell);
        _weapon = new ItemDescriptor(Guid.NewGuid()) { ItemType = ItemType.Equipment, EquipmentSlot = Options.Instance.Equipment.WeaponSlot };
        _weapon.Presentation.WeaponSpell = new() { SpellId = _spell.Id, Trigger = WeaponSpellTrigger.Manual };
        ItemDescriptor.Lookup.Set(_weapon.Id, _weapon);
        _player = new ProbePlayer { Id = Guid.NewGuid(), InGame = true, Items = new(Options.Instance.Player.MaxInventory, InventorySlot.Create) };
        _player.Items.FillToCapacity(); _player.Items[0].ItemId = _weapon.Id; _player.Items[0].Quantity = 1;
        _player.Equipment = Enumerable.Repeat(-1, Options.Instance.Equipment.Slots.Count).ToArray(); _player.Equipment[Options.Instance.Equipment.WeaponSlot] = 0;
        _player.SetVital(Vital.Health, 15); _player.SetVital(Vital.Mana, 10); GC.SuppressFinalize(_player);
    }
    [TearDown] public void Cleanup()
    {
        _player.Dispose();
        ItemDescriptor.Lookup.Delete(_weapon); SpellDescriptor.Lookup.Delete(_spell);
        typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, _options);
    }
    [Test] public void ManualSpellNeedsWeaponAndCompletesWithoutTeachingIt()
    {
        Assert.That(_player.BeginEquipmentSpell(null), Is.True); Assert.That(_player.Casts, Is.Zero);
        Assert.That(_player.BeginEquipmentSpell(null), Is.False); // Cannot replace an in-progress cast.
        _player.CastTime = Timing.Global.Milliseconds - 1; _player.Complete();
        Assert.That(_player.Casts, Is.EqualTo(1)); Assert.That(_player.CastId, Is.EqualTo(_spell.Id)); Assert.That(_player.Spells.All(s => s.SpellId != _spell.Id), Is.True);
        _player.Equipment[Options.Instance.Equipment.WeaponSlot] = -1; Assert.That(_player.BeginEquipmentSpell(null), Is.False);
    }
    [TestCase("unequip")] [TestCase("modify")] [TestCase("cancel")] [TestCase("death")]
    public void ManualCastCancelsWhenItsWeaponOrCharacterChanges(string change)
    {
        Assert.That(_player.BeginEquipmentSpell(null), Is.True); _player.CastTime = Timing.Global.Milliseconds - 1;
        switch (change) { case "unequip": _player.Equipment[Options.Instance.Equipment.WeaponSlot] = -1; break; case "modify": _player.Items[0].Properties.Enhancement = new() { Rarity = 4 }; break; case "cancel": _player.CastTime = 0; break; case "death": _player.IsDead = true; break; }
        _player.Complete(); Assert.That(_player.Casts, Is.Zero); Assert.That(_player.CastTime, Is.Zero);
    }
    [Test] public void ManualChecksManaAndRechecksAfterCastDuration()
    {
        _player.SetVital(Vital.Mana, 0); Assert.That(_player.BeginEquipmentSpell(null), Is.False);
        _player.SetVital(Vital.Mana, 10); Assert.That(_player.BeginEquipmentSpell(null), Is.True);
        _player.SetVital(Vital.Mana, 0); _player.CastTime = Timing.Global.Milliseconds - 1; _player.Complete(); Assert.That(_player.Casts, Is.Zero);
    }
    [Test] public void AutomaticProcUsesPerCopySpellAndCannotRecursivelyProc()
    {
        _player.Items[0].Properties.Enhancement = new() { Spell = new() { SpellId = _spell.Id, Trigger = WeaponSpellTrigger.OnHit, ChancePercent = 100 } };
        _player.LastAttackingWeapon = _weapon;
        typeof(Player).GetField("_attackingEquipmentProperties", Private)!.SetValue(_player, _player.CaptureWeaponProperties(_weapon));
        _player.Reenter = true; _player.Hit(); Assert.That(_player.Casts, Is.EqualTo(1)); Assert.That(_player.CastId, Is.EqualTo(_spell.Id));
        Assert.That(_weapon.Presentation.WeaponSpell!.Trigger, Is.EqualTo(WeaponSpellTrigger.Manual));
    }
    [Test] public void AutomaticRespectsChanceManaAndSpellCooldown()
    {
        _weapon.Presentation.WeaponSpell!.Trigger = WeaponSpellTrigger.OnHit; _player.LastAttackingWeapon = _weapon;
        _weapon.Presentation.WeaponSpell.ChancePercent = 0; _player.Hit(); Assert.That(_player.Casts, Is.Zero);
        _weapon.Presentation.WeaponSpell.ChancePercent = 100; _player.SetVital(Vital.Mana, 0); _player.Hit(); Assert.That(_player.Casts, Is.Zero);
        _player.SetVital(Vital.Mana, 10); _player.SpellCooldowns[_spell.Id] = Timing.Global.MillisecondsUtc + 10000; _player.Hit(); Assert.That(_player.Casts, Is.Zero);
        _player.SpellCooldowns.Clear(); _player.Hit(); Assert.That(_player.Casts, Is.EqualTo(1));
    }
}
