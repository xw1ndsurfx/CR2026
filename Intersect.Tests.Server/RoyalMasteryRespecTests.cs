using Intersect.Framework.Core.Archery;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Archery;
using Intersect.Server.Database;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.Entities;
using MessagePack;
using NUnit.Framework;

namespace Intersect.Tests.Server;
public sealed partial class RoyalCombatTests
{
    private sealed class RespecCurrency : IDisposable
    {
        internal readonly ItemDescriptor Item = new(Guid.NewGuid()) { ItemType = ItemType.Currency, MaxInventoryStack = 100000 };
        internal RespecCurrency() => ItemDescriptor.Lookup.Set(Item.Id, Item);
        public void Dispose() => ItemDescriptor.Lookup.DeleteAt(Item.Id);
    }
    [TestCase(false)] [TestCase(true)]
    public void FailedRespecPaymentRestoresEveryNativeStack(bool throws)
    {
        var currency = Guid.NewGuid(); var slots = new[] { new InventorySlot(0), new InventorySlot(1) };
        slots[0].Set(new Item(currency, 2)); slots[1].Set(new Item(currency, 3));
        bool Save() { Assert.That(slots.Sum(s => s.Quantity), Is.EqualTo(1)); if (throws) throw new IOException("test"); return false; }
        if (throws) Assert.Throws<IOException>(() => MasteryRespecPayment.Debit(slots, currency, 4, Save));
        else Assert.That(MasteryRespecPayment.Debit(slots, currency, 4, Save), Is.False);
        Assert.That(slots.Select(s => s.Quantity), Is.EqualTo(new[] { 2, 3 }));
        var called = false;
        Assert.That(MasteryRespecPayment.Debit(slots, currency, 6, () => { called = true; return true; }), Is.False);
        Assert.That(called, Is.False); Assert.That(slots.Sum(s => s.Quantity), Is.EqualTo(5));
    }
    [Test]
    public void PaidPendingOperationIsSavedWithTheDebitAndSurvivesNativeJsonReload()
    {
        using var world = new World(); using var definition = new RespecCurrency(); var currency = definition.Item.Id;
        world.Player.Items[0].Set(new Item(currency, 2)); world.Player.Items[1].Set(new Item(currency, 3));
        var pending = new PaidMasteryRespec { RequestId = Guid.NewGuid(), Category = "archery", CurrencyId = currency, Cost = 4 };
        world.Player.PetCollection.MasteryRespec = pending;
        string saved = "";
        Assert.That(MasteryRespecPayment.Debit(world.Player.Items, currency, 4, () => {
            Assert.That(world.Player.Items.Where(s => s.ItemId == currency).Sum(s => s.Quantity), Is.EqualTo(1)); saved = world.Player.PetsJson; return true;
        }), Is.True);
        world.Player.PetsJson = saved;
        Assert.That(world.Player.PetCollection.MasteryRespec!.RequestId, Is.EqualTo(pending.RequestId));
        Assert.That(world.Player.PetCollection.MasteryRespec.Cost, Is.EqualTo(4));
        Assert.That(world.Player.PetCollection.Jousting, Is.Not.Null); Assert.That(world.Player.PetCollection.Mounts, Is.Not.Null);
    }
    [Test]
    public void CompletingPaidRespecRefundsPointsPreservesExperienceAndNeverResetsTwice()
    {
        using var store = new ResultStore(); var player = Guid.NewGuid();
        var category = new MasteryCategory { Talents = MasteryTalent.ArcheryDefaults() };
        var other = new MasteryCategory { Key = "swords", Talents = MasteryTalent.ArcheryDefaults() };
        RoyalCombatRuntime.Award(player, category, 100); RoyalCombatRuntime.Award(player, other, 100);
        var purchase = Guid.NewGuid();
        Assert.That(RoyalCombatRuntime.BuyTalent(player, category, "steady-aim", purchase), Is.True);
        Assert.That(RoyalCombatRuntime.BuyTalent(player, other, "steady-aim", Guid.NewGuid()), Is.True);
        var request = Guid.NewGuid(); RoyalCombatRuntime.CompletePaidRespec(player, request, category.Key);
        Assert.That(RoyalCombatRuntime.Load(player)[category.Key], Is.EqualTo(100));
        Assert.That(RoyalCombatRuntime.LoadRanks(player).ContainsKey(category.Key), Is.False);
        Assert.That(RoyalCombatRuntime.LoadRanks(player)[other.Key]["steady-aim"], Is.EqualTo(1));
        RoyalCombatRuntime.BuyTalent(player, category, "steady-aim", purchase);
        Assert.That(RoyalCombatRuntime.LoadRanks(player).ContainsKey(category.Key), Is.False, "Old purchase receipts cannot restore ranks.");
        Assert.That(RoyalCombatRuntime.BuyTalent(player, category, "steady-aim", Guid.NewGuid()), Is.True);
        RoyalCombatRuntime.CompletePaidRespec(player, request, category.Key);
        Assert.That(RoyalCombatRuntime.LoadRanks(player)[category.Key]["steady-aim"], Is.EqualTo(1), "Retry after a paid recovery cannot erase later purchases.");
        Assert.That(RoyalCombatRuntime.RespecReceipt(player, request), Is.EqualTo(category.Key));
        Assert.Throws<InvalidDataException>(() => RoyalCombatRuntime.CompletePaidRespec(player, request, other.Key));
    }
    [Test]
    public void PublicMasterySnapshotsSeparateCharactersAndExposeLevelsProgressAndPoints()
    {
        using var store = new ResultStore(); using var world = new World(); var category = RoyalCombatRuntime.Current.Masteries[0];
        RoyalCombatRuntime.Award(world.Player.Id, category, 150);
        var publicView = RoyalCombatRuntime.PublicMasteries(world.Player.Id);
        var archery = publicView.Single(m => m.Key == "archery");
        Assert.That(archery.Level, Is.EqualTo(2)); Assert.That(archery.LevelExperience, Is.EqualTo(50));
        Assert.That(archery.RequiredExperience, Is.EqualTo(400)); Assert.That(archery.AvailablePoints, Is.EqualTo(1));
        Assert.That(RoyalCombatRuntime.PublicMasteries(Guid.NewGuid()).Single(m => m.Key == "archery").Level, Is.EqualTo(1));
        var packet = new PlayerProfilePacket { MasteriesAvailable = true, Masteries = publicView };
        var received = MessagePackSerializer.Deserialize<PlayerProfilePacket>(MessagePackSerializer.Serialize(packet));
        Assert.That(received.MasteriesAvailable, Is.True); Assert.That(received.Masteries[0].Level, Is.EqualTo(2));
        var request = new ArcheryRequestPacket { Action = ArcheryAction.Respec, TargetId = Guid.NewGuid(), ExpectedCost = 1000, ItemId = Guid.NewGuid() };
        var decoded = MessagePackSerializer.Deserialize<ArcheryRequestPacket>(MessagePackSerializer.Serialize(request));
        Assert.That((int)ArcheryAction.Leaderboard, Is.EqualTo(12)); Assert.That((int)ArcheryAction.Respec, Is.EqualTo(13));
        Assert.That(decoded.ExpectedCost, Is.EqualTo(1000)); Assert.That(decoded.TargetId, Is.EqualTo(request.TargetId));
        var readOnly = new ArcheryStatePacket { MasteriesReadOnly = true, MasteriesOwner = "Observer target" };
        Assert.That(MessagePackSerializer.Deserialize<ArcheryStatePacket>(MessagePackSerializer.Serialize(readOnly)).MasteriesReadOnly, Is.True);
    }
    [Test]
    public void UnauthenticatedRespecAndForeignTargetCannotChangeMasteryOrInventory()
    {
        using var store = new ResultStore(); using var world = new World(); using var definition = new RespecCurrency(); var currency = definition.Item.Id;
        var category = RoyalCombatRuntime.Current.Masteries[0]; RoyalCombatRuntime.Current.RespecEnabled = true;
        RoyalCombatRuntime.Current.RespecCurrencyId = currency; world.Player.Items[0].Set(new Item(currency, 10000));
        RoyalCombatRuntime.Award(world.Player.Id, category, 100);
        RoyalCombatRuntime.BuyTalent(world.Player.Id, category, "steady-aim", Guid.NewGuid());
        world.Player.HandleArcheryRequest(new() { Action = ArcheryAction.Respec, TargetId = Guid.NewGuid(), CategoryKey = category.Key,
            ItemId = currency, ExpectedCost = 1000, RequestId = Guid.NewGuid() });
        Assert.That(world.Player.Items.Where(s => s.ItemId == currency).Sum(s => s.Quantity), Is.EqualTo(10000));
        Assert.That(RoyalCombatRuntime.LoadRanks(world.Player.Id)[category.Key]["steady-aim"], Is.EqualTo(1));
    }
}
