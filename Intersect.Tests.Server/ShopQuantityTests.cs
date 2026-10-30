using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.Systems.Deaths;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Database;
using Intersect.Server.Entities;
using MessagePack;
using NUnit.Framework;

namespace Intersect.Tests.Server;

[TestFixture, NonParallelizable]
public sealed class ShopQuantityTests
{
    private ItemDescriptor _sword, _coin;
    [SetUp] public void Start()
    {
        Options.EnsureCreated();
        _sword = new(Guid.NewGuid()) { ItemType = ItemType.Equipment, Stackable = false };
        _coin = new(Guid.NewGuid()) { ItemType = ItemType.Currency, Stackable = true, MaxInventoryStack = 100 };
        ItemDescriptor.Lookup.Set(_sword.Id, _sword); ItemDescriptor.Lookup.Set(_coin.Id, _coin);
    }
    [TearDown] public void Stop() { ItemDescriptor.Lookup.Delete(_sword); ItemDescriptor.Lookup.Delete(_coin); }

    [Test] public void BulkPurchaseAllocatesOneSlotAndIndependentPropertiesPerWeapon()
    {
        Item[] inventory = [new(_coin.Id, 30), Item.None, Item.None, Item.None];
        Assert.That(ShopInventoryPlan.Purchase(inventory, _sword, _coin, 5, 3, out var result), Is.EqualTo(ShopPlanError.None));
        Assert.That(result.Where(i => i.ItemId == _sword.Id).Select(i => i.Quantity), Is.EqualTo(new[] { 1, 1, 1 }));
        Assert.That(result[1].Properties, Is.Not.SameAs(result[2].Properties));
        Assert.That(result[0].Quantity, Is.EqualTo(15)); Assert.That(inventory[0].Quantity, Is.EqualTo(30));
    }
    [Test] public void PurchasesCanUseTheSlotFreedByThePayment()
    {
        Item[] inventory = [new(_coin.Id, 10)];
        Assert.That(ShopQuantities.MaximumPurchase(inventory.Select(i => (i.ItemId, i.Quantity)), _sword, _coin, 10), Is.EqualTo(1));
        Assert.That(ShopInventoryPlan.Purchase(inventory, _sword, _coin, 10, 1, out var result), Is.EqualTo(ShopPlanError.None));
        Assert.That(result[0].ItemId, Is.EqualTo(_sword.Id));
    }
    [Test] public void QuantityAndSellAllWorkAcrossSeparateInventorySlots()
    {
        Item[] inventory = [new(_sword.Id, 1), new(_sword.Id, 1), new(_sword.Id, 1), new(_coin.Id, 10)];
        Assert.That(ShopInventoryPlan.Sale(inventory, 1, _sword, _coin, 5, 2, false, _ => true, out var partial, out var sold), Is.EqualTo(ShopPlanError.None));
        Assert.That(sold, Is.EqualTo(2)); Assert.That(partial.Count(i => i.ItemId == _sword.Id), Is.EqualTo(1));
        Assert.That(partial.Single(i => i.ItemId == _coin.Id).Quantity, Is.EqualTo(20));
        Assert.That(ShopInventoryPlan.Sale(inventory, 1, _sword, _coin, 5, 1, true, i => i != 2, out var all, out sold), Is.EqualTo(ShopPlanError.None));
        Assert.That(sold, Is.EqualTo(2)); Assert.That(all[2].ItemId, Is.EqualTo(_sword.Id), "Ineligible slots such as non-empty bags are preserved");
        Assert.That(inventory.Count(i => i.ItemId == _sword.Id), Is.EqualTo(3), "The plan does not mutate the source");
    }
    [Test] public void FailedExchangesDoNotConsumeAnyItems()
    {
        Item[] inventory = [new(_coin.Id, 2), Item.None];
        Assert.That(ShopInventoryPlan.Purchase(inventory, _sword, _coin, 3, 1, out _), Is.EqualTo(ShopPlanError.NotEnough));
        Assert.That(ShopInventoryPlan.Purchase(inventory, _sword, _coin, int.MaxValue, 2, out _), Is.EqualTo(ShopPlanError.Overflow));
        Assert.That(ShopInventoryPlan.Purchase(inventory, _sword, _coin, 0, 3, out _), Is.EqualTo(ShopPlanError.NoSpace));
        Assert.That(ShopInventoryPlan.Purchase(inventory, _sword, _coin, 1, -1, out _), Is.EqualTo(ShopPlanError.Invalid));
        Assert.That(inventory[0].Quantity, Is.EqualTo(2)); Assert.That(inventory[1].ItemId, Is.EqualTo(Guid.Empty));
        Item[] sale = [new(_sword.Id, 1)];
        Assert.That(ShopInventoryPlan.Sale(sale, 0, _sword, _coin, 101, 1, false, _ => true, out _, out _), Is.EqualTo(ShopPlanError.NoSpace));
        Assert.That(sale[0].ItemId, Is.EqualTo(_sword.Id));
    }
    [Test] public void StackLimitsAndTheClientQuantityLimitMatchTheServerPlan()
    {
        Item[] inventory = [new(_coin.Id, 30), Item.None, Item.None];
        var maximum = ShopQuantities.MaximumPurchase(inventory.Select(i => (i.ItemId, i.Quantity)), _sword, _coin, 5);
        Assert.That(maximum, Is.EqualTo(2));
        for (var quantity = 1; quantity <= 6; quantity++)
            Assert.That(ShopInventoryPlan.Purchase(inventory, _sword, _coin, 5, quantity, out _) == ShopPlanError.None, Is.EqualTo(quantity <= maximum));
        _sword.ItemType = ItemType.Consumable; _sword.Stackable = true; _sword.MaxInventoryStack = 4;
        Assert.That(ShopInventoryPlan.Purchase(inventory, _sword, _coin, 1, 7, out var stacks), Is.EqualTo(ShopPlanError.None));
        Assert.That(stacks.Where(i => i.ItemId == _sword.Id).Select(i => i.Quantity), Is.EqualTo(new[] { 4, 3 }));
    }
    [Test] public void NewPacketFieldsAndDeathDurationsRoundTripWithoutChangingLegacyDefaults()
    {
        var sell = MessagePackSerializer.Deserialize<SellItemPacket>(MessagePackSerializer.Serialize(new SellItemPacket(3, 1) { AllCopies = true }));
        Assert.That(sell.AllCopies && sell.Slot == 3, Is.True);
        var animation = MessagePackSerializer.Deserialize<PlayAnimationPacket>(MessagePackSerializer.Serialize(new PlayAnimationPacket { DurationMilliseconds = 12345 }));
        Assert.That(animation.DurationMilliseconds, Is.EqualTo(12345)); Assert.That(new PlayAnimationPacket().DurationMilliseconds, Is.Zero);
        var rule = new DeathRule { Name = "Test", MapIds = [Guid.NewGuid()], RespawnMapId = Guid.NewGuid(), GroundAnimationId = Guid.NewGuid(), GroundAnimationSeconds = 45 };
        var config = new DeathConfiguration { Rules = [rule] };
        Assert.That(DeathConfiguration.FromJson(config.ToJson()).Rules[0].GroundAnimationSeconds, Is.EqualTo(45));
        rule.GroundAnimationSeconds = 601; Assert.Throws<InvalidDataException>(() => DeathConfiguration.FromJson(config.ToJson()));
        Assert.That(new NPCDescriptor(Guid.NewGuid()).DeathAnimationDurationSeconds, Is.Zero);
    }
}
