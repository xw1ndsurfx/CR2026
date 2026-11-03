using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Collections.Slotting;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Server.Database;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.Entities;
using Intersect.Server.Maps;
using NUnit.Framework;

namespace Intersect.Tests.Server;

[TestFixture, NonParallelizable]
public sealed class CrashRegressionTests
{
    private static Player InventoryPlayer()
    {
        Options.EnsureCreated();
        var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
        GC.SuppressFinalize(player);
        player.Items = new SlotList<InventorySlot>(Options.Instance.Player.MaxInventory, InventorySlot.Create);
        return player;
    }

    [Test]
    public void MissingInstanceCleanupIsSafeToRepeat()
    {
        Options.EnsureCreated();
        var map = new MapController(Guid.NewGuid()); GC.SuppressFinalize(map);
        var id = Guid.NewGuid();
        Assert.DoesNotThrow(() => { map.DisposeInstanceWithId(id); map.DisposeInstanceWithId(id); });
    }

    private sealed class CountedInstance(MapController map) : MapInstance(map, Guid.NewGuid(), null)
    {
        internal int Calls;
        public override void Dispose() => Interlocked.Increment(ref Calls);
    }

    [Test]
    public void ConcurrentCleanupDisposesAnInstanceOnlyOnce()
    {
        Options.EnsureCreated();
        var map = new MapController(Guid.NewGuid()); GC.SuppressFinalize(map);
        var instance = (CountedInstance)RuntimeHelpers.GetUninitializedObject(typeof(CountedInstance));
        GC.SuppressFinalize(instance);
        var instances = (ConcurrentDictionary<Guid, MapInstance>)typeof(MapController).GetField("mInstances", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(map)!;
        var id = Guid.NewGuid(); instances[id] = instance;
        Assert.DoesNotThrow(() => Parallel.For(0, 64, _ => map.DisposeInstanceWithId(id)));
        Assert.That(instance.Calls, Is.EqualTo(1));
        Assert.That(instances.ContainsKey(id), Is.False);
    }

    [Test]
    public void EmptyShopCurrencyReturnsZeroWithoutDereferencingEmptyInventorySlots()
    {
        var player = InventoryPlayer();
        Assert.That(player.FindInventoryItemQuantity(Guid.Empty), Is.Zero);
        Assert.That(player.Items.Count, Is.Zero, "An empty currency is not an inventory item lookup.");
    }

    [Test]
    public void DeletedItemDefinitionCannotCrashAShopBalanceLookup()
    {
        var player = InventoryPlayer(); var missing = Guid.NewGuid();
        player.Items[0].Set(new Item(missing, 12));
        Assert.That(player.FindInventoryItemQuantity(missing), Is.Zero);
    }

    [Test]
    public void ValidStacksStillCountAndClampWithoutOverflowEvenWithANullSlot()
    {
        var player = InventoryPlayer(); var item = new ItemDescriptor(Guid.NewGuid()) { Stackable = true };
        ItemDescriptor.Lookup[item.Id] = item;
        try
        {
            player.Items[0] = null!;
            player.Items[1].Set(new Item(item.Id, 25)); player.Items[3].Set(new Item(item.Id, 75));
            Assert.That(player.FindInventoryItemQuantity(item.Id), Is.EqualTo(100));
            player.Items[1].Quantity = int.MaxValue;
            Assert.That(player.FindInventoryItemQuantity(item.Id), Is.EqualTo(int.MaxValue));
        }
        finally { ItemDescriptor.Lookup.DeleteAt(item.Id); }
    }

    [Test]
    public void NonStackableItemsStillCountOccupiedSlots()
    {
        var player = InventoryPlayer(); var item = new ItemDescriptor(Guid.NewGuid()) { Stackable = false };
        ItemDescriptor.Lookup[item.Id] = item;
        try
        {
            player.Items[0].Set(new Item(item.Id, 20)); player.Items[1].Set(new Item(item.Id, 30));
            Assert.That(player.FindInventoryItemQuantity(item.Id), Is.EqualTo(2));
        }
        finally { ItemDescriptor.Lookup.DeleteAt(item.Id); }
    }
}
