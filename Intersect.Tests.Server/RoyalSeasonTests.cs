using System.Reflection;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Dungeons;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.RoyalSeasons;
using Intersect.Network;
using Intersect.Network.Packets.RoyalSeasons;
using Intersect.Network.Packets.Server;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.Database;
using Intersect.Server.Entities;
using Intersect.Server.RoyalSeasons;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Intersect.Tests.Server;

[TestFixture, NonParallelizable]
public sealed class RoyalSeasonTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
    private Options? _options;
    [SetUp] public void Setup() { _options = Options.Instance; typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, new Options()); }
    [TearDown] public void Cleanup() => typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, _options);
    private static RoyalSeason Season() => new()
    {
        Name = "Royal autumn", Enabled = true, StartsAt = Now.AddDays(-1), EndsAt = Now.AddDays(10), ClaimGraceDays = 2,
        Objectives = [new() { Name = "Victory", Required = 2, Points = 100 }],
        Tiers = [new() { Name = "Recruit", Points = 100, Free = new() { ItemId = Guid.NewGuid(), Quantity = 4 }, Premium = new() { ItemId = Guid.NewGuid(), Quantity = 2 } }],
    };
    private static Player Player()
    {
        var player = new Player { Id = Guid.NewGuid(), Items = new(Options.Instance.Player.MaxInventory, InventorySlot.Create) };
        player.Items.FillToCapacity(); GC.SuppressFinalize(player); return player;
    }
    [Test]
    public void SeasonDatesRejectOverlapButAllowFollowingSeasonDuringClaimGrace()
    {
        var first = Season(); var second = Season(); second.StartsAt = first.EndsAt; second.EndsAt = second.StartsAt.AddDays(10);
        var config = new RoyalSeasonConfiguration { Enabled = true, Seasons = [first, second] };
        Assert.That(config.IsValid, Is.True);
        Assert.That(first.IsActive(first.StartsAt), Is.True); Assert.That(first.IsActive(first.EndsAt), Is.False);
        Assert.That(first.CanClaim(first.EndsAt.AddDays(2).AddTicks(-1)), Is.True);
        Assert.That(first.CanClaim(first.EndsAt.AddDays(2)), Is.False);
        second.StartsAt = first.EndsAt.AddTicks(-1); Assert.That(config.IsValid, Is.False);
        first.Tiers[0].Points = 101; Assert.That(first.IsValid, Is.False);
        first.Tiers[0].Points = 100;
        var reward = first.Tiers[0].Free.ItemId; first.Tiers[0].Free.ItemId = Guid.Empty; Assert.That(first.IsValid, Is.False, "A season must offer a free track."); first.Tiers[0].Free.ItemId = reward;
        first.Objectives = [first.Objectives[0], first.Objectives[0]]; Assert.That(first.IsValid, Is.False);
    }
    [Test]
    public void ModifiedVictoryCountsBothObjectivesOnceAndOnlyForMatchingTarget()
    {
        var season = Season(); var dungeon = Guid.NewGuid();
        season.Objectives = [new() { Name = "Any", Required = 2 }, new() { Name = "Challenge", Kind = SeasonObjectiveKind.ModifiedDungeonsCompleted, TargetId = dungeon }];
        var progress = new SeasonProgress(); var run = Guid.NewGuid();
        Assert.That(RoyalSeasonRules.Advance(season, progress, SeasonObjectiveKind.ModifiedDungeonsCompleted, dungeon, 1, Now, run), Is.True);
        Assert.That(progress.Objectives.Values, Is.EquivalentTo(new[] { 1, 1 }));
        Assert.That(RoyalSeasonRules.Advance(season, progress, SeasonObjectiveKind.ModifiedDungeonsCompleted, dungeon, 1, Now, run), Is.False);
        Assert.That(RoyalSeasonRules.Points(season, progress), Is.EqualTo(100));
        RoyalSeasonRules.Advance(season, progress, SeasonObjectiveKind.DungeonsCompleted, Guid.NewGuid(), long.MaxValue, Now, Guid.NewGuid());
        Assert.That(RoyalSeasonRules.Points(season, progress), Is.EqualTo(200));
        Assert.That(progress.Objectives[season.Objectives[0].Id], Is.EqualTo(2));
    }
    [Test]
    public void ProfessionProgressRequiresMatchingActivityPositiveGainAndActiveSeason()
    {
        var season = Season(); var objective = season.Objectives[0]; objective.Kind = SeasonObjectiveKind.ProfessionExperience; objective.TargetId = Guid.NewGuid(); objective.Required = 100;
        var progress = new SeasonProgress();
        foreach (var time in new[] { season.StartsAt.AddTicks(-1), season.EndsAt })
            Assert.That(RoyalSeasonRules.Advance(season, progress, objective.Kind, objective.TargetId, 20, time), Is.False);
        Assert.That(RoyalSeasonRules.Advance(season, progress, objective.Kind, Guid.NewGuid(), 20, Now), Is.False);
        Assert.That(RoyalSeasonRules.Advance(season, progress, objective.Kind, objective.TargetId, -1, Now), Is.False);
        Assert.That(RoyalSeasonRules.Advance(season, progress, SeasonObjectiveKind.DungeonsCompleted, objective.TargetId, 20, Now), Is.False);
        Assert.That(RoyalSeasonRules.Advance(season, progress, objective.Kind, objective.TargetId, long.MaxValue, Now), Is.True);
        Assert.That(progress.Objectives[objective.Id], Is.EqualTo(100)); Assert.That(progress.Completions, Is.Empty);
    }
    [Test]
    public void FreeAndPremiumClaimsAreIndependentAndLatePremiumUsesExistingProgress()
    {
        var season = Season(); var tier = season.Tiers[0]; var progress = new SeasonProgress();
        Assert.That(RoyalSeasonRules.CanClaim(season, progress, tier, false, true, Now), Is.False);
        RoyalSeasonRules.Advance(season, progress, SeasonObjectiveKind.DungeonsCompleted, Guid.Empty, 2, Now);
        Assert.That(RoyalSeasonRules.CanClaim(season, progress, tier, false, false, Now), Is.True);
        Assert.That(RoyalSeasonRules.CanClaim(season, progress, tier, true, false, Now), Is.False);
        progress.ClaimedFree.Add(tier.Id);
        Assert.That(RoyalSeasonRules.CanClaim(season, progress, tier, false, true, Now), Is.False);
        Assert.That(RoyalSeasonRules.CanClaim(season, progress, tier, true, true, season.EndsAt.AddDays(1)), Is.True);
        Assert.That(RoyalSeasonRules.CanClaim(season, progress, tier, true, false, season.EndsAt.AddDays(1)), Is.False);
        Assert.That(RoyalSeasonRules.CanClaim(season, progress, tier, true, true, season.EndsAt.AddDays(2)), Is.False);
        progress.ClaimedPremium.Add(tier.Id); Assert.That(RoyalSeasonRules.CanClaim(season, progress, tier, true, true, Now), Is.False);
    }
    [Test]
    public void RewardAndReceiptCommitTogetherSurviveReloadAndRejectDuplicateClaim()
    {
        var season = Season(); var tier = season.Tiers[0]; var player = Player();
        var item = new ItemDescriptor(tier.Free.ItemId) { Name = "Seal", Stackable = true, MaxInventoryStack = 99 };
        ItemDescriptor.Lookup.Set(item.Id, item);
        try
        {
            player.PetCollection.RoyalSeasons.Seasons[season.Id] = new();
            string? saved = null; int quantity = 0;
            var result = RoyalSeasonRuntime.CommitReward(player, season.Id, tier, false, () =>
            { saved = player.PetsJson; quantity = player.Items.Sum(i => i.Quantity); return true; });
            Assert.That(result, Is.EqualTo(RoyalSeasonRuntime.SeasonRewardResult.Success)); Assert.That(quantity, Is.EqualTo(4));
            var restored = Player(); restored.PetsJson = saved!;
            Assert.That(restored.PetCollection.RoyalSeasons.Seasons[season.Id].ClaimedFree, Does.Contain(tier.Id));
            Assert.That(RoyalSeasonRuntime.CommitReward(restored, season.Id, tier, false, () => throw new Exception("Duplicate save")), Is.EqualTo(RoyalSeasonRuntime.SeasonRewardResult.SaveFailed));
            Assert.That(restored.Items.Sum(i => i.Quantity), Is.Zero);
        }
        finally { ItemDescriptor.Lookup.Delete(item); }
    }
    [TestCase(false)] [TestCase(true)]
    public void FailedRewardSaveRollsBackInventoryAndReceipt(bool throws)
    {
        var season = Season(); var tier = season.Tiers[0]; var player = Player();
        var item = new ItemDescriptor(tier.Premium.ItemId) { Name = "Bonus", Stackable = true, MaxInventoryStack = 99 }; ItemDescriptor.Lookup.Set(item.Id, item);
        player.PetCollection.RoyalSeasons.Seasons[season.Id] = new(); player.Items[0].Set(new Item(item.Id, 3));
        try
        {
            if (throws) Assert.Throws<IOException>(() => RoyalSeasonRuntime.CommitReward(player, season.Id, tier, true, () => throw new IOException("disk")));
            else Assert.That(RoyalSeasonRuntime.CommitReward(player, season.Id, tier, true, () => false), Is.EqualTo(RoyalSeasonRuntime.SeasonRewardResult.SaveFailed));
            Assert.That(player.Items.Sum(i => i.Quantity), Is.EqualTo(3));
            Assert.That(player.PetCollection.RoyalSeasons.Seasons[season.Id].ClaimedPremium, Is.Empty);
        }
        finally { ItemDescriptor.Lookup.Delete(item); }
    }
    [Test]
    public void FullInventoryKeepsRewardUnclaimed()
    {
        var season = Season(); var player = Player(); var tier = season.Tiers[0];
        var item = new ItemDescriptor(tier.Free.ItemId) { Name = "Reward", Stackable = false }; ItemDescriptor.Lookup.Set(item.Id, item);
        player.PetCollection.RoyalSeasons.Seasons[season.Id] = new();
        foreach (var slot in player.Items) slot.Set(new Item(Guid.NewGuid(), 1));
        try
        {
            Assert.That(RoyalSeasonRuntime.CommitReward(player, season.Id, tier, false, () => throw new Exception("Inventory is full")), Is.EqualTo(RoyalSeasonRuntime.SeasonRewardResult.InventoryFull));
            Assert.That(player.PetCollection.RoyalSeasons.Seasons[season.Id].ClaimedFree, Is.Empty);
        }
        finally { ItemDescriptor.Lookup.Delete(item); }
    }
    [Test]
    public void TransactionalProfessionProgressRestoresOnFailureAndPersistsWithOtherProfileData()
    {
        var field = typeof(RoyalSeasonStore).GetField("_configuration", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var before = field.GetValue(RoyalSeasonRuntime.Store); var season = Season(); season.StartsAt = DateTimeOffset.UtcNow.AddDays(-1); season.EndsAt = DateTimeOffset.UtcNow.AddDays(1);
        season.Objectives[0].Kind = SeasonObjectiveKind.ProfessionExperience; season.Objectives[0].Required = 100;
        field.SetValue(RoyalSeasonRuntime.Store, new RoyalSeasonConfiguration { Enabled = true, Seasons = [season] });
        try
        {
            var player = Player(); var pet = Guid.NewGuid(); player.PetCollection.OwnedPets[pet] = new() { Level = 4 };
            Assert.That(RoyalSeasonRuntime.SaveProfessionProgress(player, Guid.NewGuid(), 20, () => false), Is.False);
            Assert.That(player.PetCollection.RoyalSeasons.Seasons, Is.Empty);
            Assert.Throws<IOException>(() => RoyalSeasonRuntime.SaveProfessionProgress(player, Guid.NewGuid(), 20, () => throw new IOException()));
            Assert.That(player.PetCollection.RoyalSeasons.Seasons, Is.Empty);
            string? json = null; Assert.That(RoyalSeasonRuntime.SaveProfessionProgress(player, Guid.NewGuid(), 20, () => { json = player.PetsJson; return true; }), Is.True);
            var restored = Player(); restored.PetsJson = json!;
            Assert.That(restored.PetCollection.RoyalSeasons.Seasons[season.Id].Objectives[season.Objectives[0].Id], Is.EqualTo(20));
            Assert.That(restored.PetCollection.OwnedPets[pet].Level, Is.EqualTo(4));
        }
        finally { field.SetValue(RoyalSeasonRuntime.Store, before); }
    }
    [TestCase("{}")] [TestCase("{\"RoyalSeasons\":null}")] [TestCase("{\"RoyalSeasons\":{\"Seasons\":null}}")]
    public void LegacyCharactersLoadWithoutResetOrMissingSeasonCollections(string json)
    { var player = Player(); player.PetsJson = json; Assert.That(player.PetCollection.RoyalSeasons.Seasons, Is.Empty); }
    [Test]
    public void StoreRejectsStaleRevisionAndStartedEditsAndPreservesUnreadableFile()
    {
        var folder = Path.Combine(TestContext.CurrentContext.WorkDirectory, "season-" + Guid.NewGuid()); Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "seasons.json"); var store = new RoyalSeasonStore(path); var revision = store.Revision;
            var season = Season(); var config = new RoyalSeasonConfiguration { Enabled = true, Seasons = [season] };
            store.Save(RoyalSeasonRules.Json(config), revision, Now, _ => { });
            Assert.Throws<InvalidOperationException>(() => store.Save(RoyalSeasonRules.Json(config), revision, Now, _ => { }));
            season.Name = "Edited";
            Assert.Throws<InvalidOperationException>(() => store.Save(RoyalSeasonRules.Json(config), store.Revision, Now, _ => { }));
            season.Name = "Royal autumn"; config.Enabled = false;
            store.Save(RoyalSeasonRules.Json(config), store.Revision, Now, _ => { });
            Assert.That(new RoyalSeasonStore(path).Configuration.Enabled, Is.False);
            File.WriteAllText(path, "broken"); var corrupt = new RoyalSeasonStore(path);
            Assert.That(corrupt.Configuration.Enabled, Is.False);
            Assert.Throws<InvalidOperationException>(() => corrupt.Save(RoyalSeasonRules.Json(config), corrupt.Revision, Now, _ => { }));
            Assert.That(File.ReadAllText(path), Is.EqualTo("broken"));
        }
        finally { Directory.Delete(folder, true); }
    }
    [Test]
    public void WeeklyModifiersAreUtcStableOrderIndependentAndSnapshotsDoNotChange()
    {
        var dungeon = Guid.NewGuid(); var config = new DungeonModifierConfiguration { Enabled = true, Mode = DungeonModifierMode.Weekly, ActiveCount = 2,
            Pool = Enumerable.Range(0, 8).Select(i => new DungeonModifier { Name = "Challenge " + i, EnemyHealthPercent = 125, LivesPenalty = 1 }).ToArray() };
        var monday = new DateTimeOffset(2026, 11, 16, 0, 0, 0, TimeSpan.Zero); var snapshot = config.Resolve(dungeon, monday);
        Assert.That(snapshot.Names.Length, Is.EqualTo(2)); Assert.That(snapshot.HealthPercent, Is.EqualTo(150));
        config.Pool = config.Pool.AsEnumerable().Reverse().ToArray();
        Assert.That(config.Resolve(dungeon, monday.AddDays(7).AddTicks(-1)).Names, Is.EqualTo(snapshot.Names));
        Assert.That(config.Resolve(dungeon, monday.ToOffset(TimeSpan.FromHours(-5))).Names, Is.EqualTo(snapshot.Names));
        Assert.That(Enumerable.Range(1, 10).Any(week => !config.Resolve(dungeon, monday.AddDays(7 * week)).Names.SequenceEqual(snapshot.Names)), Is.True);
        foreach (var entry in config.Pool) entry.EnemyHealthPercent = 500;
        Assert.That(snapshot.HealthPercent, Is.EqualTo(150)); Assert.That(config.Resolve(dungeon, monday).HealthPercent, Is.EqualTo(500));
    }
    [Test]
    public void ModifiersKeepLegacyDefaultsValidatePoolsAndClampWithoutOverflow()
    {
        var legacy = DungeonConfiguration.FromJson("{\"Dungeons\":[{\"Name\":\"Old\"}]}").Dungeons.Single();
        Assert.That(legacy.Modifiers.Enabled, Is.False);
        Assert.That(legacy.Modifiers.Resolve(legacy.Id, Now).Names, Is.Empty);
        var cfg = new DungeonModifierConfiguration { Enabled = true }; Assert.That(cfg.IsValid, Is.False);
        cfg.Pool = [new() { Name = "Elite", EnemyHealthPercent = 500, EnemyDamagePercent = 300, TimeLimitPercent = 25, LivesPenalty = 98, CompletionExperiencePercent = 300 }];
        Assert.That(cfg.IsValid, Is.True); var resolved = cfg.Resolve(Guid.NewGuid(), Now);
        cfg.ActiveCount = 3; Assert.That(cfg.IsValid, Is.True, "Weekly count does not limit fixed modifiers.");
        Assert.That(DungeonModifierSnapshot.Scale(long.MaxValue, 500), Is.EqualTo(long.MaxValue));
        Assert.That(DungeonModifierSnapshot.Scale(1, 25), Is.EqualTo(1)); Assert.That(DungeonModifierSnapshot.Scale(0, 300), Is.Zero);
        Assert.That(DungeonModifierSnapshot.Scale(-10, 300), Is.EqualTo(-10));
        Assert.That(DungeonModifierSnapshot.Scale(600, resolved.TimePercent), Is.EqualTo(150));
        cfg.Pool = [cfg.Pool[0], cfg.Pool[0]]; Assert.That(cfg.IsValid, Is.False);
    }
    [Test]
    public void SeasonCatalogCoversThreeLanguagesAndPreservesPlaceholdersAndFallback()
    {
        foreach (var entry in RoyalSeasonText.Catalog.Entries)
            foreach (var language in new[] { "en", "fr", "es" })
            { Assert.That(entry.Value.ContainsKey(language), Is.True, entry.Key); Assert.That(Intersect.Localization.GameTextTokens.Compatible(entry.Key, entry.Value[language]), Is.True, entry.Key); }
        Assert.That(RoyalSeasonText.Get("Royal seasons", "unsupported"), Is.EqualTo("Royal seasons"));
        Assert.That(RoyalSeasonText.Get("Authored name", "fr"), Is.EqualTo("Authored name"));
    }
    [Test]
    public void SeasonAndModifierPacketsUseRealPacketRegistry()
    {
        var registry = new PacketTypeRegistry(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, typeof(IntersectPacket).Assembly);
        Assert.That(registry.TryRegisterBuiltIn(), Is.True); PackedIntersectPacket.AddKnownTypes(registry.Types);
        var packet = new RoyalSeasonRequestPacket { Action = SeasonAction.ClaimPremium, SeasonId = Guid.NewGuid(), TierId = Guid.NewGuid() };
        Assert.That(JsonConvert.SerializeObject(MessagePacker.Instance.Deserialize<RoyalSeasonRequestPacket>(MessagePacker.Instance.Serialize(packet))), Is.EqualTo(JsonConvert.SerializeObject(packet)));
        var preview = new DungeonConfirmationPacket { PreviewUtcMilliseconds = Now.ToUnixTimeMilliseconds() };
        Assert.That(MessagePacker.Instance.Deserialize<DungeonConfirmationPacket>(MessagePacker.Instance.Serialize(preview))!.PreviewUtcMilliseconds, Is.EqualTo(preview.PreviewUtcMilliseconds));
    }
}
