using Intersect.Config;
using Intersect.Enums;
using Intersect.Framework.Core.Fishing;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Server.Database;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.Entities;
using Intersect.Server.Fishing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Intersect.Tests.Server;

/// <summary>Real account checkpoints, optionally on per-case backups of an isolated player database.</summary>
[TestFixture, NonParallelizable]
public sealed class FishingPersistenceTests
{
    private string _folder = null!;
    private Options? _previous;
    private ItemDescriptor _fish = null!, _bait = null!, _bonus = null!;
#pragma warning disable NUnit1032 // Unconnected fixture actors have no map lifecycle.
    private Player _player = null!;
#pragma warning restore NUnit1032
    private PlayerContext Open() => new SqlitePlayerContext(new DatabaseContextOptions {
        DatabaseType = DatabaseType.SQLite, ConnectionStringBuilder = new SqliteConnectionStringBuilder {
            DataSource = Path.Combine(_folder, "players.db"), Pooling = false, ForeignKeys = true },
        AutoDetectChanges = true, DisableAutoInclude = true, LoggerFactory = NullLoggerFactory.Instance });

    [SetUp] public void Setup()
    {
        _previous = Options.Instance; typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, new Options());
        Options.Instance.PlayerDatabase.KillServerOnConcurrencyException = false;
        _folder = Path.Combine(Path.GetTempPath(), "fishing-persistence-" + Guid.NewGuid()); Directory.CreateDirectory(_folder);
        var restored = Environment.GetEnvironmentVariable("FISHING_TEST_DB");
        if (!string.IsNullOrEmpty(restored))
        {
            using var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = restored, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            using var target = new SqliteConnection("Data Source=" + Path.Combine(_folder, "players.db"));
            source.Open(); target.Open(); source.BackupDatabase(target);
            using var db = Open(); db.Database.Migrate();
        }
        else { using var db = Open(); db.Database.EnsureCreated(); }
        _fish = new(Guid.NewGuid()) { Name = "Validation fish", ItemType = ItemType.None, Stackable = true, MaxInventoryStack = 5, MaxBankStack = 5, CanBank = true };
        _bait = new(Guid.NewGuid()) { Name = "Validation bait", ItemType = ItemType.None, Stackable = true, MaxInventoryStack = 20 };
        _bonus = new(Guid.NewGuid()) { Name = "Validation bonus", ItemType = ItemType.Currency, MaxInventoryStack = 100 };
        foreach (var item in new[] { _fish, _bait, _bonus }) ItemDescriptor.Lookup.Set(item.Id, item);
        var user = new User { Name = "FishingValidation", Email = "fishing@example.invalid", Password = "fixture", Salt = "fixture" };
        _player = new Player { Id = Guid.NewGuid(), Name = "FishingValidation", Items = new(8, InventorySlot.Create), Bank = new(4, BankSlot.Create) };
        GC.SuppressFinalize(_player); _player.Items.FillToCapacity(); _player.Bank.FillToCapacity();
        _player.Equipment = Enumerable.Repeat(-1, Options.Instance.Equipment.Slots.Count).ToArray();
        _player.SetVital(Vital.Health, 20); user.Players.Add(_player);
        typeof(Player).GetProperty(nameof(Player.User))!.SetValue(_player, user);
        _player.Items[0].Set(new Item(_bait.Id, 10));
        using var create = Open(); create.Users.Add(user); create.SaveChanges();
    }

    private UserSaveResult Save() { using var db = Open(); return _player.User.Save(db, force: true); }
    private FishingProgressAward Award(int experience = 25)
    {
        var before = _player.GetVariable(FishingRules.FallbackExperienceId)?.Value.Integer ?? 0;
        return new() { VariableId = FishingRules.FallbackExperienceId, Before = before, After = before + experience, PreviousLevel = 1, NewLevel = 1 };
    }
    private FishingRewardResult Reward(Guid receipt, int quantity = 7, int experience = 25) =>
        FishingRewards.Commit(_player, receipt, _fish.Id, quantity, Award(experience), Guid.Empty, 0, Save,
            bonusLoot: [new() { ItemId = _bonus.Id, Quantity = 2 }]);
    private void Reload()
    {
        using var db = Open();
        var user = db.Users.AsSplitQuery().Include(u => u.Players).ThenInclude(p => p.Items)
            .Include(u => u.Players).ThenInclude(p => p.Bank)
            .Include(u => u.Players).ThenInclude(p => p.Variables).Single(u => u.Id == _player.User.Id);
        _player = user.Players.Single(); GC.SuppressFinalize(_player);
    }
    private int Count(Guid item) => _player.Items.Where(i => i.ItemId == item).Sum(i => i.Quantity);
    private long Experience => _player.GetVariable(FishingRules.FallbackExperienceId)?.Value.Integer ?? 0;

    [TestCase(false)] [TestCase(true)]
    public void CatchAndBonusExperienceAndReceiptSurviveReloadWithoutDuplication(bool automatic)
    {
        var receipt = Guid.NewGuid(); var xp = automatic ? FishingRules.AutomaticExperience(new(), 25) : 25;
        Assert.That(Reward(receipt, experience: xp), Is.EqualTo(FishingRewardResult.Success));
        Reload(); Assert.That(Count(_fish.Id), Is.EqualTo(7)); Assert.That(Count(_bonus.Id), Is.EqualTo(2));
        Assert.That(Experience, Is.EqualTo(automatic ? 5 : 25));
        Assert.That(_player.Items.Where(i => i.ItemId == _fish.Id).Select(i => i.Quantity), Is.EquivalentTo(new[] { 5, 2 }));
        Assert.That(Reward(receipt), Is.EqualTo(FishingRewardResult.AlreadyCommitted));
        Reload(); Assert.That(Count(_fish.Id), Is.EqualTo(7)); Assert.That(Experience, Is.EqualTo(xp));
    }

    [Test] public void BaitIsCommittedAtCastAndRemainsSpentWithoutAReward()
    {
        Assert.That(FishingRewards.Commit(_player, Guid.Empty, Guid.Empty, 0, null, _bait.Id, 1, Save), Is.EqualTo(FishingRewardResult.Success));
        Reload(); Assert.That(Count(_bait.Id), Is.EqualTo(9)); Assert.That(Count(_fish.Id), Is.Zero); Assert.That(Experience, Is.Zero);
        Assert.That(FishingRewards.Commit(_player, Guid.Empty, Guid.Empty, 0, null, _bait.Id, 10, Save), Is.EqualTo(FishingRewardResult.MissingBait));
        Reload(); Assert.That(Count(_bait.Id), Is.EqualTo(9));
    }

    [TestCase(false)] [TestCase(true)]
    public void DatabaseFailureRollsBackAllLootAndProgressThenAllowsOneRetry(bool existingProgress)
    {
        if (existingProgress) Assert.That(Reward(Guid.NewGuid(), 1), Is.EqualTo(FishingRewardResult.Success));
        var beforeXp = Experience; var beforeFish = Count(_fish.Id); var beforeBonus = Count(_bonus.Id);
        // Abort a real SQLite write, after the account checkpoint has staged the graph.
        using (var db = Open()) db.Database.ExecuteSqlRaw("CREATE TRIGGER reject_fishing BEFORE UPDATE ON Players BEGIN SELECT RAISE(ABORT, 'fishing validation failure'); END;");
        var receipt = Guid.NewGuid();
        Assert.That(Reward(receipt), Is.EqualTo(FishingRewardResult.SaveFailed));
        Assert.That(Experience, Is.EqualTo(beforeXp)); Assert.That(Count(_fish.Id), Is.EqualTo(beforeFish));
        Reload(); Assert.That(Experience, Is.EqualTo(beforeXp)); Assert.That(Count(_fish.Id), Is.EqualTo(beforeFish)); Assert.That(Count(_bonus.Id), Is.EqualTo(beforeBonus));
        using (var db = Open()) db.Database.ExecuteSqlRaw("DROP TRIGGER reject_fishing;");
        Assert.That(Reward(receipt), Is.EqualTo(FishingRewardResult.Success));
        Reload(); Assert.That(Experience, Is.EqualTo(beforeXp + 25)); Assert.That(Count(_fish.Id), Is.EqualTo(beforeFish + 7));
        Assert.That(Reward(receipt), Is.EqualTo(FishingRewardResult.AlreadyCommitted));
    }

    [Test] public void FullInventoryDoesNotPartiallyGrantLootOrExperience()
    {
        _ = _player.FindOpenInventorySlots();
        foreach (var item in _player.Items) item.Set(new Item(_bait.Id, 20));
        Assert.That(Save(), Is.EqualTo(UserSaveResult.Completed));
        Assert.That(Reward(Guid.NewGuid()), Is.EqualTo(FishingRewardResult.InventoryFull));
        Reload(); Assert.That(Count(_fish.Id), Is.Zero); Assert.That(Count(_bonus.Id), Is.Zero); Assert.That(Experience, Is.Zero);
    }

    [Test] public void PremiumBankLootPersistsWithoutAnInventoryCopyAndCannotBeReplayed()
    {
        var shop = Intersect.Server.LogiCoins.LogiCoinShopRuntime.Current;
        var previous = shop.PremiumUntilUserVariableId;
        try
        {
            var premiumId = Guid.NewGuid(); shop.PremiumUntilUserVariableId = premiumId;
            var premium = new UserVariable(premiumId); premium.Value.Integer = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds();
            _player.User.Variables.Add(premium);
            _player.BankAccount = new BankAccountProfile { Opened = true, BaseSlots = 4 };
            var receipt = Guid.NewGuid();
            Assert.That(FishingRewards.Commit(_player, receipt, _fish.Id, 7, Award(), Guid.Empty, 0, Save, bank: true), Is.EqualTo(FishingRewardResult.Success));
            Reload(); Assert.That(_player.Bank.Where(i => i.ItemId == _fish.Id).Select(i => i.Quantity), Is.EquivalentTo(new[] {5, 2}));
            Assert.That(Count(_fish.Id), Is.Zero); Assert.That(Experience, Is.EqualTo(25));
            Assert.That(Reward(receipt), Is.EqualTo(FishingRewardResult.AlreadyCommitted));
            Reload(); Assert.That(Count(_fish.Id), Is.Zero); Assert.That(Experience, Is.EqualTo(25));
        }
        finally { shop.PremiumUntilUserVariableId = previous; }
    }

    [TearDown] public void Cleanup()
    {
        foreach (var item in new[] { _fish, _bait, _bonus }) if (item != null) ItemDescriptor.Lookup.Delete(item);
        typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, _previous);
        SqliteConnection.ClearAllPools(); if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }
}
