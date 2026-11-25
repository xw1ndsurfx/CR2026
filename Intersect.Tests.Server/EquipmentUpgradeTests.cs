using System.Reflection;
using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Crafting;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.GameObjects;
using Intersect.Server.Database;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.Entities;
using MessagePack;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Intersect.Tests.Server;

[TestFixture, NonParallelizable]
public sealed class EquipmentUpgradeTests
{
    private Options? _options;
    private readonly List<ItemDescriptor> _items = [];
    private readonly List<SpellDescriptor> _spells = [];
    private readonly List<EventDescriptor> _events = [];
    [SetUp] public void Setup() { _options = Options.Instance; typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, new Options()); }
    [TearDown] public void Cleanup()
    {
        foreach (var item in _items) ItemDescriptor.Lookup.Delete(item); _items.Clear();
        foreach (var spell in _spells) SpellDescriptor.Lookup.Delete(spell); _spells.Clear();
        foreach (var evt in _events) EventDescriptor.Lookup.Delete(evt); _events.Clear();
        typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, _options);
    }
    private static Player Create()
    {
        var p = new Player { Id = Guid.NewGuid(), Items = new(Options.Instance.Player.MaxInventory, InventorySlot.Create) };
        p.Items.FillToCapacity(); p.Equipment = Enumerable.Repeat(-1, Options.Instance.Equipment.Slots.Count).ToArray(); p.SetVital(Vital.Health, 15); GC.SuppressFinalize(p); return p;
    }
    private ItemDescriptor Item(Player p, int slot, bool equipment = true, int quantity = 1)
    {
        var item = new ItemDescriptor(Guid.NewGuid()) { Name = "Test equipment", ItemType = equipment ? ItemType.Equipment : ItemType.Crafting, EquipmentSlot = Options.Instance.Equipment.WeaponSlot, IgnoreGlobalCooldown = true, Rarity = 1 };
        _items.Add(item); ItemDescriptor.Lookup.Set(item.Id, item); p.Items[slot].ItemId = item.Id; p.Items[slot].Quantity = quantity; return item;
    }
    private SpellDescriptor Spell()
    {
        var spell = new SpellDescriptor(Guid.NewGuid()) { Name = "Test spell", IgnoreGlobalCooldown = true };
        _spells.Add(spell); SpellDescriptor.Lookup.Set(spell.Id, spell); return spell;
    }
    private (Player Player, CraftingRecipeDescriptor Recipe, EquipmentUpgradeTarget Target) Recipe()
    {
        var p = Create(); var gear = Item(p, 0); p.Items[0].Properties.StatModifiers[0] = 9;
        p.Items[1].Set(p.Items[0]); var material = Item(p, 2, false, 10);
        var recipe = new CraftingRecipeDescriptor(Guid.NewGuid()) { ItemId = gear.Id, Quantity = 1, EquipmentUpgrade = new() { TargetRarity = 3 } };
        recipe.Ingredients.Add(new(material.Id, 4));
        Assert.That(p.PrepareEquipmentUpgrade(recipe, 0, out var target), Is.Empty);
        return (p, recipe, target!);
    }
    [Test] public void ImprovesOnlySelectedCopyAndPersistsWithoutRerollingStats()
    {
        var (p, recipe, target) = Recipe(); string? stored = null; var originalDescriptor = JsonConvert.SerializeObject(p.Items[0].Descriptor);
        Assert.That(p.CommitEquipmentUpgrade(recipe, target, false, false, () => { stored = JsonConvert.SerializeObject(p.Items); return true; }), Is.EqualTo("Equipment improved."));
        Assert.Multiple(() => {
            Assert.That(p.Items[0].Descriptor.EffectiveRarity(p.Items[0].Properties), Is.EqualTo(3));
            Assert.That(p.Items[1].Properties.Enhancement, Is.Null); Assert.That(p.Items[0].Properties.StatModifiers[0], Is.EqualTo(9));
            Assert.That(p.Items[2].Quantity, Is.EqualTo(6)); Assert.That(JsonConvert.SerializeObject(p.Items[0].Descriptor), Is.EqualTo(originalDescriptor));
            Assert.That(stored, Does.Contain("Enhancement"));
        });
        var restored = JsonConvert.DeserializeObject<List<InventorySlot>>(stored!)!;
        Assert.That(restored[0].Properties.Enhancement!.Rarity, Is.EqualTo(3));
        Assert.That(p.PrepareEquipmentUpgrade(recipe, 0, out _), Does.Contain("already"));
    }
    [TestCase(false)] [TestCase(true)] public void FailedSaveRestoresEveryCopyAndAllMaterials(bool throws)
    {
        var (p, recipe, target) = Recipe(); var before = JsonConvert.SerializeObject(p.Items);
        if (throws) Assert.Throws<IOException>(() => p.CommitEquipmentUpgrade(recipe, target, false, false, () => throw new IOException("test")));
        else Assert.That(p.CommitEquipmentUpgrade(recipe, target, false, false, () => false), Does.Contain("restored"));
        Assert.That(JsonConvert.SerializeObject(p.Items), Is.EqualTo(before));
    }
    [TestCase(false)] [TestCase(true)] public void CraftFailureNeverDestroysSelectedGear(bool loseMaterials)
    {
        var (p, recipe, target) = Recipe(); var before = JsonConvert.SerializeObject(p.Items[0]); var saves = 0;
        Assert.That(p.CommitEquipmentUpgrade(recipe, target, true, loseMaterials, () => { saves++; return true; }), Does.Contain("failed"));
        Assert.That(JsonConvert.SerializeObject(p.Items[0]), Is.EqualTo(before)); Assert.That(p.Items[2].Quantity, Is.EqualTo(loseMaterials ? 6 : 10)); Assert.That(saves, Is.EqualTo(loseMaterials ? 1 : 0));
    }
    [Test] public void MaterialsNeverConsumeTargetOrEquippedCopyAndDuplicateRowsAreAggregated()
    {
        var (p, recipe, _) = Recipe(); recipe.Ingredients = [new(recipe.ItemId, 1), new(recipe.ItemId, 1)];
        Assert.That(p.PrepareEquipmentUpgrade(recipe, 0, out _), Does.Contain("materials"));
        recipe.Ingredients.RemoveAt(1); p.Equipment[Options.Instance.Equipment.WeaponSlot] = 1;
        Assert.That(p.PrepareEquipmentUpgrade(recipe, 0, out _), Does.Contain("materials"));
        p.Equipment[Options.Instance.Equipment.WeaponSlot] = -1;
        Assert.That(p.PrepareEquipmentUpgrade(recipe, 0, out var target), Is.Empty);
        Assert.That(p.CommitEquipmentUpgrade(recipe, target!, false, false, () => true), Is.EqualTo("Equipment improved."));
        Assert.That(p.Items[0].Quantity, Is.EqualTo(1)); Assert.That(p.Items[1].IsEmpty, Is.True);
    }
    [TestCase("move")] [TestCase("stats")] [TestCase("enhancement")] [TestCase("recipe")] [TestCase("combat")]
    public void RevalidatesBeforeCommit(string change)
    {
        var (p, recipe, target) = Recipe();
        switch (change) { case "move": p.Items[0].Set(Intersect.Server.Database.Item.None); break; case "stats": p.Items[0].Properties.StatModifiers[0]++; break; case "enhancement": p.Items[0].Properties.Enhancement = new() { Rarity = 2 }; break; case "recipe": recipe.EquipmentUpgrade!.TargetRarity = 4; break; case "combat": p.CombatTimer = Timing.Global.Milliseconds + 10000; break; }
        var before = JsonConvert.SerializeObject(p.Items); var saved = false;
        Assert.That(p.CommitEquipmentUpgrade(recipe, target, false, false, () => saved = true), Is.Not.EqualTo("Equipment improved."));
        Assert.That(saved, Is.False); Assert.That(JsonConvert.SerializeObject(p.Items), Is.EqualTo(before));
    }
    [Test] public void RefusesDowngradesInvalidSpellsAndNonCommonEvents()
    {
        var (p, recipe, _) = Recipe(); recipe.EquipmentUpgrade!.TargetRarity = 0; Assert.That(p.PrepareEquipmentUpgrade(recipe, 0, out _), Does.Contain("lower"));
        recipe.EquipmentUpgrade.TargetRarity = 3; recipe.EquipmentUpgrade.Spell = new() { SpellId = Guid.NewGuid() }; Assert.That(p.PrepareEquipmentUpgrade(recipe, 0, out _), Does.Contain("invalid"));
        recipe.EquipmentUpgrade.Spell.SpellId = Spell().Id; recipe.EquipmentUpgrade.Spell.ChancePercent = 0; Assert.That(p.PrepareEquipmentUpgrade(recipe, 0, out _), Does.Contain("invalid"));
        recipe.EquipmentUpgrade.Spell = null; var evt = new EventDescriptor(Guid.NewGuid()) { CommonEvent = false }; _events.Add(evt); EventDescriptor.Lookup.Set(evt.Id, evt); recipe.EquipmentUpgrade.EventId = evt.Id;
        Assert.That(p.PrepareEquipmentUpgrade(recipe, 0, out _), Does.Contain("invalid"));
        evt.CommonEvent = true; Assert.That(p.PrepareEquipmentUpgrade(recipe, 0, out _), Is.Empty);
    }
    [Test] public void AttachesSpellAndEventWithoutChangingOtherCopiesOrBaseSpell()
    {
        var (p, recipe, _) = Recipe(); var spell = Spell(); var evt = new EventDescriptor(Guid.NewGuid()) { CommonEvent = true }; _events.Add(evt); EventDescriptor.Lookup.Set(evt.Id, evt);
        recipe.EquipmentUpgrade!.Spell = new() { SpellId = spell.Id, Trigger = WeaponSpellTrigger.OnHit, ChancePercent = 35 }; recipe.EquipmentUpgrade.EventId = evt.Id; recipe.EquipmentUpgrade.EventTrigger = ItemEventTrigger.OnDamageReceived;
        Assert.That(p.PrepareEquipmentUpgrade(recipe, 0, out var target), Is.Empty);
        Assert.That(p.CommitEquipmentUpgrade(recipe, target!, false, false, () => true), Is.EqualTo("Equipment improved."));
        Assert.That(p.Items[0].Descriptor.EffectiveWeaponSpell(p.Items[0].Properties)!.SpellId, Is.EqualTo(spell.Id));
        Assert.That(p.Items[1].Descriptor.EffectiveWeaponSpell(p.Items[1].Properties), Is.Null);
        Assert.That(p.Items[0].Properties.Enhancement!.EventId, Is.EqualTo(evt.Id));
        var copy = p.Items[0].Clone(); copy.Properties.Enhancement!.Spell!.ChancePercent = 90;
        Assert.That(p.Items[0].Properties.Enhancement.Spell!.ChancePercent, Is.EqualTo(35));
    }
    [Test] public void EnchantedEquipmentKeepsItsIdentityInLoadoutsAndProjectileSnapshots()
    {
        var (p, recipe, target) = Recipe(); p.CommitEquipmentUpgrade(recipe, target, false, false, () => true);
        p.Equipment[Options.Instance.Equipment.WeaponSlot] = 0;
        var set = p.CaptureEquipmentLoadout("Forge"); var snapshot = p.CaptureWeaponProperties(p.Items[0].Descriptor)!;
        p.Items[0].Properties.Enhancement!.Rarity = 4;
        Assert.That(set.Items[Options.Instance.Equipment.WeaponSlot]!.Enhancement!.Rarity, Is.EqualTo(3)); Assert.That(snapshot.Enhancement!.Rarity, Is.EqualTo(3));
        Assert.That(p.CommitEquipmentLoadout(set, () => true), Does.Contain("missing"));
    }
    [Test] public void PropertiesRoundTripInNetworkAndLegacyJson()
    {
        var props = new ItemProperties { Enhancement = new() { Rarity = 4, Spell = new() { SpellId = Guid.NewGuid(), Trigger = WeaponSpellTrigger.OnHit }, EventId = Guid.NewGuid() } }; props.StatModifiers[0] = 12;
        var restored = MessagePackSerializer.Deserialize<ItemProperties>(MessagePackSerializer.Serialize(props));
        Assert.That(EquipmentEnhancement.Same(props.Enhancement, restored.Enhancement), Is.True); Assert.That(restored.StatModifiers[0], Is.EqualTo(12));
        Assert.That(JsonConvert.DeserializeObject<ItemProperties>("{\"StatModifiers\":[1,2,3,4,5,6]}")!.Enhancement, Is.Null);
    }
    [Test] public void RefusesStaleInventorySelectionBeforeStartingCraft()
    {
        var (p, recipe, _) = Recipe(); var expected = new ItemProperties(p.Items[0].Properties);
        Assert.That(p.EquipmentUpgradeSelectionMatches(recipe, 0, expected), Is.True);
        p.Items[0].Properties.Enhancement = new() { Rarity = 4 };
        Assert.That(p.EquipmentUpgradeSelectionMatches(recipe, 0, expected), Is.False);
        Assert.That(p.EquipmentUpgradeSelectionMatches(recipe, 0, null), Is.False);
        Assert.That(p.EquipmentUpgradeSelectionMatches(recipe, -1, expected), Is.False);
    }
    [Test] public void RecipeDatabaseJsonRetainsLegacyIngredientsAndNewUpgrade()
    {
        var (_, recipe, _) = Recipe(); var restored = new CraftingRecipeDescriptor();
        typeof(CraftingRecipeDescriptor).GetProperty(nameof(CraftingRecipeDescriptor.IngredientsJson))!.SetValue(restored, recipe.IngredientsJson);
        Assert.That(restored.EquipmentUpgrade!.TargetRarity, Is.EqualTo(3)); Assert.That(restored.Ingredients.Single().Quantity, Is.EqualTo(4));
        typeof(CraftingRecipeDescriptor).GetProperty(nameof(CraftingRecipeDescriptor.IngredientsJson))!.SetValue(restored, JsonConvert.SerializeObject(recipe.Ingredients));
        Assert.That(restored.EquipmentUpgrade, Is.Null); Assert.That(restored.Campfire, Is.Null); Assert.That(restored.Ingredients.Single().Quantity, Is.EqualTo(4));
    }
}
