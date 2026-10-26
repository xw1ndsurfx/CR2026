using Intersect.Enums;
using Intersect.Framework.Core.Jousting;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Server.Jousting;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.Entities;
using Intersect.Server.Database;
using NUnit.Framework;
namespace Intersect.Tests.Server;
[TestFixture]
public sealed class JoustExchangeTests
{
    private static (JoustCareer Career, JoustOffer Offer, ItemDescriptor Item, InventorySlot[] Slots) Setup()
    {
        var item = new ItemDescriptor(Guid.NewGuid()) { ItemType = ItemType.Currency, MaxInventoryStack = 10 };
        var slots = new[] { new InventorySlot(0), new InventorySlot(1) };
        slots[0].Set(new Item(item.Id, 8)); slots[1].Set(Item.None);
        return (new() { Medals = 50 }, new() { ItemId = item.Id, Quantity = 5, Medals = 10 }, item, slots);
    }
    [Test] public void SuccessfulExchangeSavesItemsAndBalanceTogetherAndRejectsReplay()
    {
        var (c, o, item, slots) = Setup(); var request = Guid.NewGuid(); var saves = 0;
        bool Save() { saves++; Assert.That(c.Medals, Is.EqualTo(40)); Assert.That(slots.Sum(s => s.Quantity), Is.EqualTo(13)); Assert.That(c.Purchases, Does.Contain(request)); return true; }
        Assert.That(JoustEconomyExchange.Redeem(c, o, item, slots, request, Save), Is.EqualTo(JoustExchangeResult.Success));
        Assert.That(JoustEconomyExchange.Redeem(c, o, item, slots, request, Save), Is.EqualTo(JoustExchangeResult.Duplicate));
        Assert.That(saves, Is.EqualTo(1)); Assert.That(slots.Select(s => s.Quantity), Is.EqualTo(new[] { 10, 3 }));
    }
    [TestCase(false)] [TestCase(true)] public void FailedSaveRollsBackEverySlotAndReceipt(bool throws)
    {
        var (c, o, item, slots) = Setup(); var id = Guid.NewGuid();
        JoustExchangeResult Act() => JoustEconomyExchange.Redeem(c, o, item, slots, id, () => throws ? throw new InvalidOperationException() : false);
        if (throws) Assert.Throws<InvalidOperationException>(() => Act()); else Assert.That(Act(), Is.EqualTo(JoustExchangeResult.SaveFailed));
        Assert.That(c.Medals, Is.EqualTo(50)); Assert.That(c.Purchases, Is.Empty);
        Assert.That(slots[0].Quantity, Is.EqualTo(8)); Assert.That(slots[1].ItemId, Is.EqualTo(Guid.Empty));
    }
    [Test] public void FullInventoryCannotLoseMedalsOrPartiallyGrantItems()
    {
        var (c, o, item, slots) = Setup(); slots[1].Set(new Item(Guid.NewGuid(), 1));
        Assert.That(JoustEconomyExchange.Redeem(c, o, item, slots, Guid.NewGuid(), () => throw new Exception()), Is.EqualTo(JoustExchangeResult.InventoryFull));
        Assert.That(c.Medals, Is.EqualTo(50)); Assert.That(slots[0].Quantity, Is.EqualTo(8));
    }
    [Test] public void InsufficientBalanceAndMissingRequestAreRejected()
    {
        var (c, o, item, slots) = Setup(); c.Medals = 0;
        Assert.That(JoustEconomyExchange.Redeem(c, o, item, slots, Guid.NewGuid(), () => throw new Exception()), Is.EqualTo(JoustExchangeResult.Invalid));
        c.Medals = 50;
        Assert.That(JoustEconomyExchange.Redeem(c, o, item, slots, Guid.Empty, () => throw new Exception()), Is.EqualTo(JoustExchangeResult.Invalid));
    }
    [Test] public void CareerAndPurchaseReceiptsUseTheExistingNativeCharacterPayload()
    {
        var player = new Player(); GC.SuppressFinalize(player);
        player.PetCollection.Jousting.Medals = 42; var request = Guid.NewGuid(); player.PetCollection.Jousting.Purchases.Add(request);
        var other = new Player(); GC.SuppressFinalize(other); other.PetsJson = player.PetsJson;
        Assert.That(other.PetCollection.Jousting.Medals, Is.EqualTo(42)); Assert.That(other.PetCollection.Jousting.Purchases, Does.Contain(request));
    }
}
