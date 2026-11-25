using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.EquipmentLoadouts;
using Intersect.Framework.Core.GameObjects.Conditions;
using Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network;
using Intersect.Network.Packets.EquipmentLoadouts;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.Entities;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Intersect.Tests.Server;

[TestFixture, NonParallelizable]
public sealed class EquipmentLoadoutTests
{
    private Options? _options;
    private readonly List<ItemDescriptor> _items = [];
    [SetUp] public void Setup()
    { _options = Options.Instance; typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, new Options()); }
    [TearDown] public void Cleanup()
    { foreach (var item in _items) ItemDescriptor.Lookup.Delete(item); _items.Clear(); typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, _options); }
    private static Player Create()
    {
        var p = new Player { Id = Guid.NewGuid(), Items = new(Options.Instance.Player.MaxInventory, InventorySlot.Create) };
        p.Items.FillToCapacity(); p.Equipment = Enumerable.Repeat(-1, Options.Instance.Equipment.Slots.Count).ToArray();
        p.SetVital(Vital.Health, 15); GC.SuppressFinalize(p); return p;
    }
    private ItemDescriptor Gear(Player p, int inventory, int equipment, int bonus = 0)
    {
        var item = new ItemDescriptor(Guid.NewGuid()) { Name = "Test gear", ItemType = ItemType.Equipment, EquipmentSlot = equipment, IgnoreGlobalCooldown = true };
        _items.Add(item); ItemDescriptor.Lookup.Set(item.Id, item);
        p.Items[inventory].ItemId = item.Id; p.Items[inventory].Quantity = 1; p.Items[inventory].Properties.StatModifiers[0] = bonus;
        p.Equipment[equipment] = inventory; return item;
    }
    private static EquipmentLoadoutRequestPacket Save(int slot = 0, long revision = 0, string name = "Dungeon") => new() { Action = EquipmentLoadoutAction.Save, Slot = slot, Revision = revision, Name = name };

    [Test] public void SavesPerCharacterAndRestoresThroughExistingProfileWithoutLosingOtherData()
    {
        var first = Create(); var second = Create(); var slot = Options.Instance.Equipment.WeaponSlot;
        Gear(first, 2, slot, 7); first.PetCollection.OwnedPets[Guid.NewGuid()] = new() { Level = 4 };
        string? saved = null;
        Assert.That(first.CommitEquipmentLoadoutDefinition(Save(), () => { saved = first.PetsJson; return true; }), Is.EqualTo("Equipment set saved."));
        Assert.That(second.PetCollection.EquipmentLoadouts.Sets, Is.Empty);
        second.PetsJson = saved!;
        Assert.That(second.PetCollection.EquipmentLoadouts.Sets[0].Items[slot]!.StatModifiers[0], Is.EqualTo(7));
        Assert.That(second.PetCollection.OwnedPets.Single().Value.Level, Is.EqualTo(4));
        Assert.That(second.PetCollection.EquipmentLoadouts.Revision, Is.EqualTo(1));
    }

    [TestCase("")] [TestCase("{}")] [TestCase("{\"EquipmentLoadouts\":null}")]
    [TestCase("{\"EquipmentLoadouts\":{\"Sets\":null}}")]
    public void LegacyProfilesStartWithEmptySets(string json)
    { var p = Create(); p.PetsJson = json; Assert.That(p.PetCollection.EquipmentLoadouts.Sets, Is.Empty); }

    [Test] public void ApplyFindsMovedItemsAndExactBonusesWithoutMutatingInventory()
    {
        var p = Create(); var slot = Options.Instance.Equipment.WeaponSlot;
        var item = Gear(p, 0, slot, 12); var set = p.CaptureEquipmentLoadout("Bow");
        p.Items[6].Set(p.Items[0]); p.Items[0].Properties.StatModifiers[0] = 3; p.Equipment[slot] = -1;
        var beforeItems = JsonConvert.SerializeObject(p.Items); string? equipment = null;
        Assert.That(p.CommitEquipmentLoadout(set, () => { equipment = p.EquipmentJson; return true; }), Is.EqualTo("Equipment set equipped."));
        Assert.That(p.Equipment[slot], Is.EqualTo(6)); Assert.That(JsonConvert.SerializeObject(p.Items), Is.EqualTo(beforeItems));
        var restored = Create(); restored.EquipmentJson = equipment!; Assert.That(restored.Equipment[slot], Is.EqualTo(6));
    }

    [Test] public void MissingOrChangedPieceLeavesEntireOutfitIntact()
    {
        var p = Create(); var slot = Options.Instance.Equipment.WeaponSlot;
        Gear(p, 0, slot, 2); var set = p.CaptureEquipmentLoadout("Adventure");
        p.Items[0].Properties.StatModifiers[0] = 3; var before = p.Equipment.ToArray();
        var saves = 0;
        Assert.That(p.CommitEquipmentLoadout(set, () => { saves++; return true; }), Does.Contain("missing"));
        Assert.That(p.Equipment, Is.EqualTo(before)); Assert.That(saves, Is.Zero);
    }

    [Test] public void CombatBlocksBeforeAnyMutationAndAllowsSwitchAfterExpiry()
    {
        var p = Create(); var slot = Options.Instance.Equipment.WeaponSlot;
        Gear(p, 0, slot); var set = p.CaptureEquipmentLoadout("Combat"); p.Equipment[slot] = -1;
        p.CombatTimer = Intersect.Framework.Core.Timing.Global.Milliseconds + 10000;
        var saved = false;
        Assert.That(p.CommitEquipmentLoadout(set, () => saved = true), Does.Contain("during combat"));
        Assert.That(saved, Is.False); Assert.That(p.Equipment[slot], Is.EqualTo(-1));
        p.CombatTimer = 0;
        Assert.That(p.CommitEquipmentLoadout(set, () => true), Is.EqualTo("Equipment set equipped."));
    }

    [Test] public void DeadCastingAndBusyPlayersCannotChangeSets()
    {
        var p = Create(); p.IsDead = true; Assert.That(p.EquipmentLoadoutBlockReason(), Is.Not.Empty);
        p.IsDead = false; p.CastTime = Intersect.Framework.Core.Timing.Global.Milliseconds + 10000;
        Assert.That(p.EquipmentLoadoutBlockReason(), Is.Not.Empty); p.CastTime = 0;
        p.OpenCraftingTableId = Guid.NewGuid(); Assert.That(p.EquipmentLoadoutBlockReason(), Is.Not.Empty);
    }

    [TestCase(false)] [TestCase(true)] public void FailedOrThrowingSaveRestoresEquipmentAndVitals(bool throws)
    {
        var p = Create(); var slot = Options.Instance.Equipment.WeaponSlot; var high = Gear(p, 0, slot);
        high.VitalsGiven[(int)Vital.Health] = 100; p.SetVital(Vital.Health, 90);
        var empty = p.CaptureEquipmentLoadout("Empty"); empty.Items[slot] = null;
        var vitals = p.VitalsJson; var before = p.EquipmentJson;
        if (throws) Assert.Throws<IOException>(() => p.CommitEquipmentLoadout(empty, () => throw new IOException("test")));
        else Assert.That(p.CommitEquipmentLoadout(empty, () => false), Does.StartWith("Unable to save"));
        Assert.That(p.EquipmentJson, Is.EqualTo(before)); Assert.That(p.VitalsJson, Is.EqualTo(vitals));
    }

    [Test] public void EmptySlotsUnequipAndNeverRestoreHealthWhenSwitchingBack()
    {
        var p = Create(); var slot = Options.Instance.Equipment.WeaponSlot; var item = Gear(p, 0, slot);
        item.VitalsGiven[(int)Vital.Health] = 100; var full = p.CaptureEquipmentLoadout("Full"); var empty = p.CaptureEquipmentLoadout("Empty"); empty.Items[slot] = null;
        p.SetVital(Vital.Health, 90); Assert.That(p.CommitEquipmentLoadout(empty, () => true), Is.EqualTo("Equipment set equipped."));
        Assert.That(p.GetVital(Vital.Health), Is.EqualTo(20));
        Assert.That(p.CommitEquipmentLoadout(full, () => true), Is.EqualTo("Equipment set equipped."));
        Assert.That(p.GetVital(Vital.Health), Is.EqualTo(20));
        var saves = 0; Assert.That(p.CommitEquipmentLoadout(full, () => { saves++; return true; }), Does.Contain("already equipped")); Assert.That(saves, Is.Zero);
    }

    [Test] public void TwoHandedAndChangedSlotLayoutsAreRejected()
    {
        var p = Create(); var weapon = Gear(p, 0, Options.Instance.Equipment.WeaponSlot); weapon.TwoHanded = true;
        Gear(p, 1, Options.Instance.Equipment.ShieldSlot); var set = p.CaptureEquipmentLoadout("Invalid");
        Assert.That(p.PlanEquipmentLoadout(set, out _), Does.Contain("two-handed"));
        set.SlotNames[0] = "Changed"; Assert.That(p.PlanEquipmentLoadout(set, out _), Does.Contain("slots have changed"));
    }

    [Test] public void SaveDeleteRequireCurrentRevisionAndRollbackOnFailure()
    {
        var p = Create(); Assert.That(p.CommitEquipmentLoadoutDefinition(Save(), () => true), Is.EqualTo("Equipment set saved."));
        Assert.That(p.CommitEquipmentLoadoutDefinition(Save(name: "Stale"), () => true), Does.Contain("Refresh"));
        var json = p.PetsJson;
        Assert.That(p.CommitEquipmentLoadoutDefinition(Save(revision: 1, name: "Replacement"), () => false), Does.StartWith("Unable"));
        Assert.That(p.PetsJson, Is.EqualTo(json));
        var delete = new EquipmentLoadoutRequestPacket { Action = EquipmentLoadoutAction.Delete, Slot = 0, Revision = 1 };
        Assert.Throws<IOException>(() => p.CommitEquipmentLoadoutDefinition(delete, () => throw new IOException())); Assert.That(p.PetsJson, Is.EqualTo(json));
        Assert.That(p.CommitEquipmentLoadoutDefinition(delete, () => true), Is.EqualTo("Equipment set deleted."));
        Assert.That(p.PetCollection.EquipmentLoadouts.Sets, Is.Empty);
    }

    [TestCase(-1)] [TestCase(5)] [TestCase(int.MaxValue)] public void InvalidSlotIsRejected(int slot)
    { Assert.That(Create().CommitEquipmentLoadoutDefinition(Save(slot), () => throw new Exception()), Is.EqualTo("Choose an equipment set.")); }
    [TestCase("")] [TestCase("\nBad")] [TestCase("123456789012345678901234567890123")] public void InvalidNameIsRejected(string name)
    { Assert.That(Create().CommitEquipmentLoadoutDefinition(Save(name: name), () => throw new Exception()), Does.StartWith("Enter a name")); }

    [Test] public void EquipmentCooldownBlocksSwitch()
    {
        var p = Create(); var slot = Options.Instance.Equipment.WeaponSlot; var item = Gear(p, 0, slot); var set = p.CaptureEquipmentLoadout("Cooldown"); p.Equipment[slot] = -1;
        p.ItemCooldowns[item.Id] = Intersect.Framework.Core.Timing.Global.MillisecondsUtc + 10000;
        Assert.That(p.CommitEquipmentLoadout(set, () => throw new Exception()), Does.Contain("cooldown")); Assert.That(p.Equipment[slot], Is.EqualTo(-1));
    }

    [Test] public void AuthoredRequirementsMustHoldBeforeAndAfterTheCompleteSwap()
    {
        var p = Create(); var weaponSlot = Options.Instance.Equipment.WeaponSlot; var shieldSlot = Options.Instance.Equipment.ShieldSlot;
        var weapon = Gear(p, 0, weaponSlot); var shield = Gear(p, 1, shieldSlot);
        weapon.UsageRequirements.Lists.Add(new ConditionList { Conditions = [new IsItemEquippedCondition { ItemId = shield.Id }] });
        var set = p.CaptureEquipmentLoadout("Needs shield"); set.Items[shieldSlot] = null;
        var before = p.EquipmentJson;
        Assert.That(p.CommitEquipmentLoadout(set, () => throw new Exception("Must not save")), Does.Contain("requirements"));
        Assert.That(p.EquipmentJson, Is.EqualTo(before));
        set.Items[shieldSlot] = p.CaptureEquipmentLoadout("Full").Items[shieldSlot]; p.Equipment[shieldSlot] = -1;
        Assert.That(p.CommitEquipmentLoadout(set, () => throw new Exception("Must not save")), Does.Contain("requirements"));
    }

    [Test] public void NavalCombatAlsoBlocksCrewAndAllowsIdleDeckChanges()
    {
        var p = Create(); var naval = typeof(Intersect.Server.Naval.NavalSystem);
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
        var ships = (System.Collections.IDictionary)naval.GetField("Ships", flags)!.GetValue(null)!;
        var vesselType = naval.GetNestedType("Vessel", System.Reflection.BindingFlags.NonPublic)!;
        var definition = new Intersect.Framework.Core.Naval.ShipDefinition { Id = Guid.NewGuid() };
        var vessel = Activator.CreateInstance(vesselType, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, [definition], null)!;
        var combat = vesselType.GetField("CombatUntil", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        p.BoardedShipId = definition.Id; ships.Add(definition.Id, vessel);
        try
        {
            combat.SetValue(vessel, Intersect.Framework.Core.Timing.Global.Milliseconds + 10000);
            Assert.That(p.EquipmentLoadoutBlockReason(), Does.Contain("combat"));
            combat.SetValue(vessel, 0L); Assert.That(p.EquipmentLoadoutBlockReason(), Is.Empty);
        }
        finally { ships.Remove(definition.Id); }
    }

    [Test] public void CatalogAndPacketRegistryCoverAllLanguagesAndActions()
    {
        foreach (var entry in EquipmentLoadoutText.Catalog.Entries) foreach (var lang in new[] { "en", "fr", "es" })
        { Assert.That(entry.Value.ContainsKey(lang), Is.True, entry.Key); Assert.That(Intersect.Localization.GameTextTokens.Compatible(entry.Key, entry.Value[lang]), Is.True); }
        Assert.That(EquipmentLoadoutText.Get("Equipment sets", "other"), Is.EqualTo("Equipment sets"));
        var registry = new PacketTypeRegistry(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, typeof(IntersectPacket).Assembly);
        Assert.That(registry.TryRegisterBuiltIn(), Is.True); PackedIntersectPacket.AddKnownTypes(registry.Types);
        foreach (var action in Enum.GetValues<EquipmentLoadoutAction>())
        {
            var packet = Save(); packet.Action = action; packet.RequestId = Guid.NewGuid();
            Assert.That(JsonConvert.SerializeObject(MessagePacker.Instance.Deserialize<EquipmentLoadoutRequestPacket>(MessagePacker.Instance.Serialize(packet))), Is.EqualTo(JsonConvert.SerializeObject(packet)));
        }
    }
}
