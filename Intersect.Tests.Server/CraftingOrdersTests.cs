using Intersect.Config;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Crafting;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets.Server;
using Intersect.Server.CraftingOrders;
using Intersect.Server.Database;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Intersect.Tests.Server;

[TestFixture, NonParallelizable]
public sealed class CraftingOrdersTests
{
    private string _folder = null!;
    private CraftingOrdersService _service = null!;
    private ItemDescriptor _coin = null!, _goods = null!, _filler = null!;
    private CraftingRecipeDescriptor _recipe = null!;
    // Unconnected fixture actors have no map lifecycle to dispose.
#pragma warning disable NUnit1032
    private Player _customer = null!, _crafter = null!;
#pragma warning restore NUnit1032
    private Options? _previous;
    private PlayerContext Open() => new SqlitePlayerContext(new DatabaseContextOptions {
        DatabaseType = DatabaseType.SQLite, ConnectionStringBuilder = new SqliteConnectionStringBuilder {
            DataSource = Path.Combine(_folder, "players.db"), Pooling = false, ForeignKeys = true },
        AutoDetectChanges = true, DisableAutoInclude = true, LoggerFactory = NullLoggerFactory.Instance });

    [SetUp] public void Setup()
    {
        _previous = Options.Instance; typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, new Options());
        _folder = Path.Combine(Path.GetTempPath(), "crafting-orders-" + Guid.NewGuid()); Directory.CreateDirectory(_folder);
        var restored = Environment.GetEnvironmentVariable("CRAFTING_ORDERS_TEST_DB");
        if (!string.IsNullOrEmpty(restored))
        {
            // The opt-in path must be an isolated copy. Every case makes another
            // SQLite backup; the source and its accounts are never mutated.
            using var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = restored, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            using var target = new SqliteConnection("Data Source=" + Path.Combine(_folder, "players.db"));
            source.Open(); target.Open(); source.BackupDatabase(target);
            using var db = Open(); db.Database.Migrate();
        }
        else { using var db = Open(); db.Database.EnsureCreated(); }
        _coin = new(Guid.NewGuid()) { Name = "Aureons", ItemType = ItemType.Currency, Stackable = true, MaxInventoryStack = 10000, MaxBankStack = 10000 };
        _goods = new(Guid.NewGuid()) { Name = "Test crafted sword", ItemType = ItemType.Equipment, CanTrade = true };
        _filler = new(Guid.NewGuid()) { Name = "Full bag fixture", ItemType = ItemType.Equipment };
        foreach (var item in new[] { _coin, _goods, _filler }) ItemDescriptor.Lookup.Set(item.Id, item);
        _recipe = new(Guid.NewGuid()) { ItemId = _goods.Id, Quantity = 1 }; CraftingRecipeDescriptor.Lookup.Set(_recipe.Id, _recipe);
        _service = new(Open, Path.Combine(_folder, "legacy.json"));
        _customer = Create("Customer"); _crafter = Create("Crafter");
        _customer.Items[0].Set(new Item(_coin.Id, 1000)); Persist(_customer);
    }
    private Player Create(string name)
    {
        var user = new User { Name = name, Email = name + "@example.invalid", Password = "test", Salt = "test" };
        var player = new Player { Id = Guid.NewGuid(), Name = name,
            Items = new(8, InventorySlot.Create), Bank = new(4, BankSlot.Create) };
        GC.SuppressFinalize(player); player.Items.FillToCapacity(); player.Bank.FillToCapacity();
        player.Equipment = Enumerable.Repeat(-1, Options.Instance.Equipment.Slots.Count).ToArray();
        player.SetVital(Vital.Health, 20); user.Players.Add(player);
        typeof(Player).GetProperty(nameof(Player.User))!.SetValue(player, user);
        using var db = Open(); db.Users.Add(user); db.SaveChanges(); return player;
    }
    private void Persist(Player player) { using var db = Open(); Assert.That(player.User.Save(db, force: true), Is.EqualTo(UserSaveResult.Completed)); }
    private CraftingOrdersStatePacket Post(Player? player = null, Guid request = default, int quantity = 1, int reward = 100)
    { player ??= _customer; return _service.Run(player, s => _service.Post(s, player, _goods.Id, quantity, reward, 1, request)); }
    private CraftingOrdersStatePacket Act(Player p, Guid id, byte action) => _service.Run(p, s => _service.Act(s, p, id, action));
    private int Coins(Player p) => p.Items.Concat<Item>(p.Bank).Where(i => i.ItemId == _coin.Id).Sum(i => i.Quantity);
    private CraftingOrderStore Stored() { using var db = Open(); return JsonConvert.DeserializeObject<CraftingOrderStore>(db.CraftingOrdersLedger.Single().State)!; }
    [TearDown] public void Cleanup()
    {
        foreach (var item in new[] { _coin, _goods, _filler }) if (item != null) ItemDescriptor.Lookup.Delete(item);
        if (_recipe != null) CraftingRecipeDescriptor.Lookup.Delete(_recipe);
        typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, _previous);
        SqliteConnection.ClearAllPools(); if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    [Test] public void TwoPlayersPostAcceptDeliverAndClaimExactlyOnceWithActualProperties()
    {
        var post = Post(); Assert.That(post.MessageKey, Is.EqualTo("Posted")); Assert.That(Coins(_customer), Is.EqualTo(900));
        var id = post.Orders.Single().Id; Assert.That(Act(_customer, id, 0).MessageKey, Is.EqualTo("Unavailable"));
        Assert.That(Act(_crafter, id, 0).MessageKey, Is.EqualTo("Accepted"));
        Assert.That(Act(_customer, id, 2).MessageKey, Is.EqualTo("Unavailable"));
        Assert.That(Act(_crafter, id, 1).MessageKey, Is.EqualTo("MissingItems"));
        var item = new Item(_goods.Id, 1); item.Properties.StatModifiers[0] = 17;
        item.Properties.Enhancement = new() { Rarity = 4, EventId = Guid.NewGuid() };
        _crafter.Items[0].Set(item); Persist(_crafter);
        Assert.That(Act(_crafter, id, 1).MessageKey, Is.EqualTo("Delivered")); Assert.That(Coins(_crafter), Is.EqualTo(100));
        Assert.That(Act(_crafter, id, 1).MessageKey, Is.EqualTo("Unavailable"));
        _service = new(Open, Path.Combine(_folder, "legacy.json")); _service.Run(_customer); _service.Run(_customer);
        var received = _customer.Items.Single(i => i.ItemId == _goods.Id);
        Assert.That(JsonConvert.SerializeObject(received.Properties), Is.EqualTo(JsonConvert.SerializeObject(item.Properties)));
        Assert.That(Stored().Deliveries, Is.Empty); Assert.That(Stored().PendingAureons, Is.Empty);
        using var db = Open(); Assert.That(db.Player_Items.Single(i => i.PlayerId == _customer.Id && i.ItemId == _goods.Id).Properties.StatModifiers[0], Is.EqualTo(17));
    }

    [Test] public void RepeatedPublishRequestChargesOnlyOnceAcrossServiceRestart()
    {
        var request = Guid.NewGuid(); Post(request: request); _service = new(Open, "unused");
        Assert.That(Post(request: request).MessageKey, Is.EqualTo("AlreadyPosted"));
        Assert.That(Coins(_customer), Is.EqualTo(900)); Assert.That(Stored().Orders, Has.Count.EqualTo(1));
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public void FailureAfterDatabaseWriteRollsBackOrderMoneyAndItems(int phase)
    {
        Guid id = default;
        if (phase > 0) id = Post().Orders.Single().Id;
        if (phase == 2) { Act(_crafter, id, 0); _crafter.Items[0].Set(new Item(_goods.Id, 1)); Persist(_crafter); }
        var player = phase is 1 or 2 ? _crafter : _customer;
        var inventory = JsonConvert.SerializeObject(player.Items);
        using var beforeDb = Open(); var before = beforeDb.CraftingOrdersLedger.AsNoTracking().SingleOrDefault()?.State;
        _service.BeforeCommit = () => throw new IOException("Injected before transaction commit");
        var result = phase == 0 ? Post() : Act(player, id, phase == 1 ? (byte)0 : phase == 2 ? (byte)1 : (byte)2);
        Assert.That(result.MessageKey, Is.EqualTo("SaveFailed")); Assert.That(JsonConvert.SerializeObject(player.Items), Is.EqualTo(inventory));
        using var afterDb = Open(); Assert.That(afterDb.CraftingOrdersLedger.AsNoTracking().SingleOrDefault()?.State, Is.EqualTo(before));
        Assert.That(afterDb.Player_Items.Where(i => i.PlayerId == player.Id).AsEnumerable().Sum(i => i.Quantity), Is.EqualTo(player.Items.Sum(i => i.Quantity)));
    }

    [Test] public void FullInventoryAndBankKeepPaymentAndDeliveryUntilSpaceIsAvailable()
    {
        var id = Post().Orders.Single().Id; Act(_crafter, id, 0);
        foreach (var p in new[] { _customer, _crafter })
        { foreach (var slot in p.Items.Concat<Item>(p.Bank)) slot.Set(new Item(_filler.Id, 1)); Persist(p); }
        _goods.ItemType = ItemType.None; _goods.Stackable = true; _goods.MaxInventoryStack = 99;
        _crafter.Items[0].Set(new Item(_goods.Id, 2)); Persist(_crafter);
        var result = Act(_crafter, id, 1); Assert.That(result.MessageKey, Is.EqualTo("Delivered")); Assert.That(result.PendingAureons, Is.EqualTo(100));
        Assert.That(_service.Run(_customer).PendingItems, Is.EqualTo(1));
        _service = new(Open, "unused"); _crafter.Items[1].Set(Item.None); Persist(_crafter);
        Assert.That(_service.Run(_crafter).PendingAureons, Is.Zero); Assert.That(Coins(_crafter), Is.EqualTo(100));
        _customer.Items[0].Set(Item.None); Persist(_customer);
        Assert.That(_service.Run(_customer).PendingItems, Is.Zero); Assert.That(_customer.Items[0].ItemId, Is.EqualTo(_goods.Id));
    }

    [Test] public void CancellationAndExpiryRefundOnlyOnce()
    {
        var id = Post().Orders.Single().Id; Assert.That(Act(_customer, id, 2).MessageKey, Is.EqualTo("Cancelled"));
        Act(_customer, id, 2); Assert.That(Coins(_customer), Is.EqualTo(1000));
        id = Post().Orders.Single(o => o.Status == 0).Id; Act(_crafter, id, 0);
        _service.Now = () => DateTimeOffset.UtcNow.AddHours(2); _service.Run(_customer); _service.Run(_customer);
        Assert.That(Coins(_customer), Is.EqualTo(1000)); Assert.That(Stored().Orders.Single(o => o.Id == id).Status, Is.EqualTo(4));
        Assert.That(Act(_crafter, id, 1).MessageKey, Is.EqualTo("Unavailable"));
    }

    [Test] public void LegacyEscrowIsImportedOnceAndOriginalFileIsPreserved()
    {
        var json = JsonConvert.SerializeObject(new CraftingOrderStore { PendingAureons = { [_customer.Id] = 37 }, PendingItems = { [_customer.Id] = new() { [_goods.Id] = 1 } } });
        File.WriteAllText(Path.Combine(_folder, "legacy.json"), json);
        _service.Run(_customer); _service = new(Open, Path.Combine(_folder, "legacy.json")); _service.Run(_customer);
        Assert.That(Coins(_customer), Is.EqualTo(1037)); Assert.That(_customer.Items.Count(i => i.ItemId == _goods.Id), Is.EqualTo(1));
        Assert.That(File.ReadAllText(Path.Combine(_folder, "legacy.json")), Is.EqualTo(json));
    }
    [Test] public void CorruptLegacyEscrowIsRejectedWithoutResetOrCharge()
    {
        File.WriteAllText(Path.Combine(_folder, "legacy.json"), "{broken"); Assert.That(Post().MessageKey, Is.EqualTo("SaveFailed"));
        Assert.That(Coins(_customer), Is.EqualTo(1000)); using var db = Open(); Assert.That(db.CraftingOrdersLedger.Any(), Is.False);
    }
    [Test] public void EquippedAndNoLongerTradeableItemsCannotBeDelivered()
    {
        var id = Post().Orders.Single().Id; Act(_crafter, id, 0); _crafter.Items[0].Set(new Item(_goods.Id, 1)); _crafter.Equipment[0] = 0;
        Assert.That(Act(_crafter, id, 1).MessageKey, Is.EqualTo("MissingItems")); _crafter.Equipment[0] = -1; _goods.CanTrade = false;
        Assert.That(Act(_crafter, id, 1).MessageKey, Is.EqualTo("InvalidRecipe")); Assert.That(_crafter.Items[0].ItemId, Is.EqualTo(_goods.Id));
    }

    [Test] public void SimultaneousAcceptanceHasOneWinner()
    {
        var id = Post().Orders.Single().Id; var other = Create("SecondCrafter");
        var results = new string[2];
        Parallel.Invoke(() => results[0] = Act(_crafter, id, 0).MessageKey, () => results[1] = Act(other, id, 0).MessageKey);
        Assert.That(results.Count(r => r == "Accepted"), Is.EqualTo(1));
        Assert.That(results.Count(r => r == "Unavailable"), Is.EqualTo(1));
        Assert.That(Stored().Orders.Single().CrafterId, Is.AnyOf(_crafter.Id, other.Id));
    }

    [Test] public void FailedClaimRetainsPendingPaymentAndItemsForRetry()
    {
        var id = Post().Orders.Single().Id; Act(_crafter, id, 0); _crafter.Items[0].Set(new Item(_goods.Id, 1)); Persist(_crafter); Act(_crafter, id, 1);
        _service.BeforeCommit = () => throw new IOException("Claim failure");
        Assert.That(_service.Run(_customer).MessageKey, Is.EqualTo("SaveFailed"));
        Assert.That(_customer.Items.Any(i => i.ItemId == _goods.Id), Is.False);
        Assert.That(Stored().Deliveries[_customer.Id], Has.Count.EqualTo(1));
        _service.BeforeCommit = null; _service.Run(_customer); _service.Run(_customer);
        Assert.That(_customer.Items.Count(i => i.ItemId == _goods.Id), Is.EqualTo(1));
    }

    [Test] public void NoOpRefreshDoesNotSaveAndFailureToExpireCanRetry()
    {
        var id = Post().Orders.Single().Id;
        _service.BeforeCommit = () => throw new IOException("Unexpected write");
        Assert.That(_service.Run(_customer).MessageKey, Is.Empty);
        _service.Now = () => DateTimeOffset.UtcNow.AddHours(2);
        Assert.That(_service.Run(_customer).MessageKey, Is.EqualTo("SaveFailed")); Assert.That(Stored().Orders.Single().Status, Is.Zero);
        _service.BeforeCommit = null; _service.Run(_customer); Assert.That(Coins(_customer), Is.EqualTo(1000));
    }

    [Test] public void BankOverflowRespectsOpeningAndItemBankPermission()
    {
        var id = Post().Orders.Single().Id; Act(_crafter, id, 0);
        _crafter.Items[0].Set(new Item(_goods.Id, 1)); Persist(_crafter); Act(_crafter, id, 1);
        foreach (var slot in _customer.Items) slot.Set(new Item(_filler.Id, 1)); Persist(_customer);
        Assert.That(_service.Run(_customer).PendingItems, Is.EqualTo(1), "Unopened bank must not bypass its unlock");
        _customer.BankAccount.Opened = true; _goods.CanBank = false;
        Assert.That(_service.Run(_customer).PendingItems, Is.EqualTo(1), "Unbankable item must remain in escrow");
        _goods.CanBank = true;
        Assert.That(_service.Run(_customer).PendingItems, Is.Zero);
        Assert.That(_customer.Bank.Count(i => i.ItemId == _goods.Id), Is.EqualTo(1));
    }

    [TestCase("{\"PendingAureons\":{\"00000000-0000-0000-0000-000000000001\":-1}}")]
    [TestCase("{\"Orders\":[{\"Quantity\":-1}]}")]
    public void InvalidEscrowValuesFailWithoutOverwritingTheSource(string json)
    {
        File.WriteAllText(Path.Combine(_folder, "legacy.json"), json);
        Assert.That(Post().MessageKey, Is.EqualTo("SaveFailed"));
        Assert.That(Coins(_customer), Is.EqualTo(1000));
        Assert.That(File.ReadAllText(Path.Combine(_folder, "legacy.json")), Is.EqualTo(json));
        using var db = Open(); Assert.That(db.CraftingOrdersLedger.Any(), Is.False);
    }

    [Test] public void TranslationsCoverAllLocalesAndPreservePlaceholdersWithEnglishFallback()
    {
        var entries = (Dictionary<string, string[]>)typeof(Intersect.Framework.Core.CraftingOrders.CraftingOrdersText)
            .GetField("Entries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.GetValue(null)!;
        foreach (var (key, values) in entries)
        {
            Assert.That(values, Has.Length.EqualTo(3), key);
            var placeholders = System.Text.RegularExpressions.Regex.Matches(values[0], @"\{[^}]+\}").Select(m => m.Value).Order().ToArray();
            foreach (var text in values)
            {
                Assert.That(text, Is.Not.Empty, key);
                Assert.That(System.Text.RegularExpressions.Regex.Matches(text, @"\{[^}]+\}").Select(m => m.Value).Order().ToArray(), Is.EqualTo(placeholders), key);
            }
            Assert.That(Intersect.Framework.Core.CraftingOrders.CraftingOrdersText.Get(key, "unknown"), Is.EqualTo(values[0]));
        }
    }

    [Test] public void LargeHistoryDoesNotHideAcceptedWorkOrPublicOrders()
    {
        var store = new CraftingOrderStore();
        for (var i = 0; i < 510; i++)
            store.Orders.Add(new() { CustomerId = _customer.Id, ItemId = _goods.Id, Quantity = 1, Reward = 1, Status = 3 });
        var available = new CraftingOrderRecord { CustomerId = _crafter.Id, ItemId = _goods.Id, Quantity = 1, Reward = 1, Deadline = DateTimeOffset.UtcNow.AddHours(1) };
        var accepted = new CraftingOrderRecord { CustomerId = _crafter.Id, CrafterId = _customer.Id, ItemId = _goods.Id, Quantity = 1, Reward = 1, Status = 1, Deadline = DateTimeOffset.UtcNow.AddHours(1) };
        store.Orders.Add(available); store.Orders.Add(accepted);
        File.WriteAllText(Path.Combine(_folder, "legacy.json"), JsonConvert.SerializeObject(store));
        var view = _service.Run(_customer);
        Assert.That(view.Orders.Select(o => o.Id), Does.Contain(available.Id).And.Contain(accepted.Id));
        Assert.That(view.Orders, Has.Length.EqualTo(500));
    }
}
