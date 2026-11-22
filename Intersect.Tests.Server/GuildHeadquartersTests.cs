using System.Reflection;
using Intersect.Framework.Core.GuildHeadquarters;
using Intersect.Network;
using Intersect.Network.Packets.GuildHeadquarters;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.GuildHeadquarters;
using MessagePack;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Newtonsoft.Json;
using NUnit.Framework;
using Intersect.Enums;
using Intersect.Server.Entities;
using Intersect.Server.Maps;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;
using Intersect.Framework.Core.GameObjects.Maps;

namespace Intersect.Tests.Server;

[TestFixture, NonParallelizable]
public sealed class GuildHeadquartersTests
{
    private Options? _before;
    [SetUp] public void Setup()
    { _before = Options.Instance; typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, new Options()); }
    [TearDown] public void Cleanup() => typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, _before);
    private static HeadquartersConfiguration Config(Guid item = default) => new() { Enabled = true, Tiers = [
        new() { Name = "Lodge", MapId = Guid.NewGuid() },
        new() { Name = "Manor", MapId = Guid.NewGuid(), CostItemId = item, CostQuantity = item == Guid.Empty ? 0 : 75, BankSlots = 80 } ] };
    private sealed class HeadquartersWorld : IDisposable
    {
        private readonly FieldInfo _config = typeof(HeadquartersStore).GetField("_configuration", BindingFlags.NonPublic | BindingFlags.Instance)!;
        private readonly object? _before;
        internal readonly MapController Outside = new(Guid.NewGuid()), Hall = new(Guid.NewGuid());
        internal readonly HeadquartersConfiguration Configuration;
        internal HeadquartersWorld()
        {
            foreach (var map in new[] { Outside, Hall }) { MapController.Lookup.Set(map.Id, map); GC.SuppressFinalize(map); }
            Configuration = new() { Enabled = true, Tiers = [new() { MapId = Hall.Id, Name = "Hall" }] };
            _before = _config.GetValue(HeadquartersRuntime.Store); _config.SetValue(HeadquartersRuntime.Store, Configuration);
        }
        public void Dispose()
        { _config.SetValue(HeadquartersRuntime.Store, _before); MapController.Lookup.DeleteAt(Outside.Id); MapController.Lookup.DeleteAt(Hall.Id); }
    }
    [Test]
    public void HeadquartersRestartRestoresOutsidePositionAndNeverTrustsSavedVisitForAdmission()
    {
        using var world = new HeadquartersWorld();
        var player = new Player { MapId = world.Hall.Id, MapInstanceId = Guid.NewGuid(), InstanceType = MapInstanceType.Shared };
        GC.SuppressFinalize(player);
        player.HeadquartersVisit = new() { MapId = world.Hall.Id, InstanceId = player.MapInstanceId, ReturnMapId = world.Outside.Id, ReturnX = 7, ReturnY = 8 };
        Assert.That(HeadquartersRuntime.CanWarp(player, world.Hall.Id, player.MapInstanceId), Is.False, "Saved return data cannot grant access.");
        Assert.That(HeadquartersRuntime.CanWarp(player, world.Hall.Id, Guid.Empty, MapInstanceType.Overworld), Is.False);
        HeadquartersRuntime.RestoreLogin(player);
        Assert.That((player.MapId, player.X, player.Y), Is.EqualTo((world.Outside.Id, 7f, 8f)));
        Assert.That(player.MapInstanceId, Is.EqualTo(Guid.Empty)); Assert.That(player.InstanceType, Is.EqualTo(MapInstanceType.Overworld));
        Assert.That(player.GuildHeadquartersVisitJson, Is.Null);
        Assert.That(HeadquartersRuntime.CanWarp(player, world.Outside.Id, Guid.Empty), Is.True);
    }
    [TestCase("broken")] [TestCase(null)]
    public void HeadquartersRestartRecoversMissingOrCorruptReturnWithoutLeavingPlayerInPrivateMap(string? saved)
    {
        using var world = new HeadquartersWorld();
        var player = new Player { MapId = world.Hall.Id, MapInstanceId = Guid.NewGuid(), InstanceType = MapInstanceType.Shared, GuildHeadquartersVisitJson = saved };
        GC.SuppressFinalize(player);
        HeadquartersRuntime.RestoreLogin(player);
        // Other fixtures may have registered public maps; the emergency fallback may use any of them.
        Assert.That(MapController.Get(player.MapId), Is.Not.Null);
        Assert.That(player.MapId, Is.Not.EqualTo(world.Hall.Id));
        Assert.That(HeadquartersRuntime.CanWarp(player, player.MapId, Guid.Empty), Is.True);
        Assert.That(player.MapInstanceId, Is.EqualTo(Guid.Empty));
        Assert.That(player.InstanceType, Is.EqualTo(MapInstanceType.Overworld));
        Assert.That(player.GuildHeadquartersVisitJson, Is.Null);
    }
    [TestCase("broken")] [TestCase(null)]
    public void HeadquartersRestartPrefersLastOverworldPositionWhenReturnIsMissingOrCorrupt(string? saved)
    {
        using var world = new HeadquartersWorld();
        var player = new Player { MapId = world.Hall.Id, MapInstanceId = Guid.NewGuid(), InstanceType = MapInstanceType.Shared, GuildHeadquartersVisitJson = saved };
        GC.SuppressFinalize(player);
        player.UpdateLastOverworldLocation(world.Outside.Id, 7, 8);
        HeadquartersRuntime.RestoreLogin(player);
        Assert.That((player.MapId, player.X, player.Y), Is.EqualTo((world.Outside.Id, 7f, 8f)));
        Assert.That(player.MapInstanceId, Is.EqualTo(Guid.Empty));
        Assert.That(player.InstanceType, Is.EqualTo(MapInstanceType.Overworld));
        Assert.That(player.GuildHeadquartersVisitJson, Is.Null);
    }
    [Test]
    public void HeadquartersConfigurationRejectsIncomingConnectionsAndBlockedEntry()
    {
        using var world = new HeadquartersWorld();
        Assert.DoesNotThrow(() => HeadquartersRuntime.Validate(world.Configuration));
        world.Outside.Right = world.Hall.Id;
        Assert.Throws<InvalidOperationException>(() => HeadquartersRuntime.Validate(world.Configuration));
        world.Outside.Right = Guid.Empty;
        world.Hall.Attributes[10,10] = new MapBlockedAttribute();
        Assert.Throws<InvalidOperationException>(() => HeadquartersRuntime.Validate(world.Configuration));
    }
    private static Guild GuildWithFunds(Guid item)
    {
        var guild = new Guild();
        var member = Guid.NewGuid(); guild.Members[member] = new(member, "Nicolas", 0, 1, "Human", "Town");
        guild.Bank[0].ItemId = item; guild.Bank[0].Quantity = 40;
        guild.Bank[1].ItemId = item; guild.Bank[1].Quantity = 60;
        return guild;
    }
    [Test]
    public void HeadquartersLegacyDefaultsAndConfigurationRejectUnsafeOrIncompleteValues()
    {
        Assert.That(JsonConvert.DeserializeObject<HeadquartersConfiguration>("{}")!.Enabled, Is.False);
        Assert.That(HeadquartersProfile.Read(null).Revision, Is.Zero);
        var cfg = Config(); Assert.That(cfg.IsValid(32, 26), Is.True);
        cfg.Tiers[0].CostQuantity = 2; Assert.That(cfg.IsValid(32,26), Is.False); cfg.Tiers[0].CostQuantity = 0;
        cfg.Tiers[1].MapId = cfg.Tiers[0].MapId; Assert.That(cfg.IsValid(32,26), Is.False); cfg.Tiers[1].MapId = Guid.NewGuid();
        cfg.Tiers[1].EntryX = 32; Assert.That(cfg.IsValid(32,26), Is.False); cfg.Tiers[1].EntryX = 10;
        cfg.Assets.Background = "../secret.png"; Assert.That(cfg.IsValid(32,26), Is.False);
        cfg.Assets.Background = ""; cfg.Assets.EffectVolume = 101; Assert.That(cfg.IsValid(32,26), Is.False);
        Assert.Throws<InvalidDataException>(() => HeadquartersProfile.Read("{\"Revision\":-1}"));
        Assert.Throws<JsonReaderException>(() => HeadquartersProfile.Read("broken"));
    }
    [TestCase(-1)] [TestCase(1)] [TestCase(99)]
    public void HeadquartersPurchaseRejectsUnauthorizedRanksWithoutTouchingBank(int rank)
    {
        var item=Guid.NewGuid(); var guild=GuildWithFunds(item); var cfg=Config(item);
        Assert.Throws<InvalidOperationException>(()=>HeadquartersPurchase.Buy(guild,cfg,cfg.Tiers[1].Id,0,rank,()=>Assert.Fail("Unauthorized save")));
        Assert.That(HeadquartersPurchase.Available(guild,item),Is.EqualTo(100)); Assert.That(guild.HeadquartersJson,Is.Null);
    }
    [Test]
    public void HeadquartersPurchaseSpansStacksExpandsBankAndSurvivesJsonReload()
    {
        var item=Guid.NewGuid(); var guild=GuildWithFunds(item); var cfg=Config(item); var saves=0;
        HeadquartersPurchase.Buy(guild,cfg,cfg.Tiers[1].Id,0,0,()=>saves++);
        Assert.That(saves,Is.EqualTo(1)); Assert.That(HeadquartersPurchase.Available(guild,item),Is.EqualTo(25));
        Assert.That(guild.Bank[0].IsEmpty,Is.True); Assert.That(guild.Bank[1].Quantity,Is.EqualTo(25));
        Assert.That(guild.BankSlotsCount,Is.GreaterThanOrEqualTo(80)); Assert.That(guild.Bank.Capacity,Is.EqualTo(guild.BankSlotsCount));
        var reloaded=HeadquartersProfile.Read(guild.HeadquartersJson); Assert.That(reloaded.TierIndex(cfg),Is.EqualTo(1)); Assert.That(reloaded.Revision,Is.EqualTo(1));
        Assert.Throws<InvalidOperationException>(()=>HeadquartersPurchase.Buy(guild,cfg,cfg.Tiers[1].Id,0,0,()=>saves++));
        Assert.That(saves,Is.EqualTo(1)); Assert.That(HeadquartersPurchase.Available(guild,item),Is.EqualTo(25));
    }
    [Test]
    public void HeadquartersSaveFailureRestoresEveryStackCapacityAndUnlock()
    {
        var item=Guid.NewGuid(); var guild=GuildWithFunds(item); var cfg=Config(item); var slots=guild.BankSlotsCount;
        Assert.Throws<IOException>(()=>HeadquartersPurchase.Buy(guild,cfg,cfg.Tiers[1].Id,0,0,()=>throw new IOException("disk full")));
        Assert.That(guild.HeadquartersJson,Is.Null); Assert.That(guild.BankSlotsCount,Is.EqualTo(slots)); Assert.That(guild.Bank.Capacity,Is.EqualTo(slots));
        Assert.That(guild.Bank[0].Quantity,Is.EqualTo(40)); Assert.That(guild.Bank[0].ItemId,Is.EqualTo(item)); Assert.That(guild.Bank[1].Quantity,Is.EqualTo(60));
    }
    [Test]
    public void HeadquartersConcurrentManagersPayOnlyOnce()
    {
        var item=Guid.NewGuid(); var guild=GuildWithFunds(item); var cfg=Config(item); var saves=0;
        Parallel.For(0,10,_=>{try { HeadquartersPurchase.Buy(guild,cfg,cfg.Tiers[1].Id,0,0,()=>Interlocked.Increment(ref saves)); } catch(InvalidOperationException) { }});
        Assert.That(saves,Is.EqualTo(1)); Assert.That(HeadquartersPurchase.Available(guild,item),Is.EqualTo(25));
    }
    [Test]
    public void HeadquartersInsufficientFundsMembershipAndStaleTierNeverSpend()
    {
        var item=Guid.NewGuid(); var guild=GuildWithFunds(item); var cfg=Config(item);
        cfg.Tiers[1].CostQuantity=101;
        Assert.Throws<InvalidOperationException>(()=>HeadquartersPurchase.Buy(guild,cfg,cfg.Tiers[1].Id,0,0,()=>Assert.Fail()));
        cfg.Tiers[1].CostQuantity=75; cfg.Tiers[1].MinimumMembers=2;
        Assert.Throws<InvalidOperationException>(()=>HeadquartersPurchase.Buy(guild,cfg,cfg.Tiers[1].Id,0,0,()=>Assert.Fail()));
        cfg.Tiers[1].MinimumMembers=1;
        Assert.Throws<InvalidOperationException>(()=>HeadquartersPurchase.Buy(guild,cfg,Guid.NewGuid(),0,0,()=>Assert.Fail()));
        Assert.Throws<InvalidOperationException>(()=>HeadquartersPurchase.Buy(guild,cfg,cfg.Tiers[1].Id,2,0,()=>Assert.Fail()));
        Assert.That(HeadquartersPurchase.Available(guild,item),Is.EqualTo(100));
    }
    [Test]
    public void HeadquartersInvitationsAreRecipientBoundSingleUseExpiringAndRevocable()
    {
        var invitations=new HeadquartersInvitations(); var host=Guid.NewGuid(); var guest=Guid.NewGuid();var guild=Guid.NewGuid();
        var token=invitations.Create(host,guest,guild,1000);
        Assert.That(invitations.Take(token,Guid.NewGuid(),1001),Is.Null);
        Assert.That(invitations.Take(token,guest,1001)!.Guild,Is.EqualTo(guild));
        Assert.That(invitations.Take(token,guest,1002),Is.Null);
        token=invitations.Create(host,guest,guild,1000);Assert.That(invitations.Take(token,guest,121000),Is.Null);
        token=invitations.Create(host,guest,guild,1000);invitations.RemoveGuild(guild);Assert.That(invitations.Take(token,guest,1002),Is.Null);
        token=invitations.Create(host,guest,guild,1000);invitations.RemovePlayer(host);Assert.That(invitations.Take(token,guest,1002),Is.Null);
        token=invitations.Create(host,guest,guild,1000);invitations.RemovePlayer(guest);Assert.That(invitations.Take(token,guest,1002),Is.Null);
    }
    [Test]
    public void HeadquartersConfigurationUsesRevisionsAndPreservesPaidTierIds()
    {
        var directory=Path.Combine(TestContext.CurrentContext.WorkDirectory,"hq-test-"+Guid.NewGuid());Directory.CreateDirectory(directory);
        try
        {
            var path=Path.Combine(directory,"headquarters.json"); var store=new HeadquartersStore(path); var revision=store.Revision;var cfg=Config();
            store.Save(JsonConvert.SerializeObject(cfg),revision,_=>{});
            Assert.Throws<InvalidOperationException>(()=>store.Save(JsonConvert.SerializeObject(cfg),revision,_=>{}));
            cfg.Tiers=cfg.Tiers.AsEnumerable().Reverse().ToArray();
            Assert.Throws<InvalidOperationException>(()=>store.Save(JsonConvert.SerializeObject(cfg),store.Revision,_=>{}));
            var reload=new HeadquartersStore(path); Assert.That(reload.Configuration.Tiers[0].Name,Is.EqualTo("Lodge"));
            File.WriteAllText(path,"invalid file"); Assert.That(new HeadquartersStore(path).Configuration.Enabled,Is.False); Assert.That(File.ReadAllText(path),Is.EqualTo("invalid file"));
        }
        finally { Directory.Delete(directory,true); }
    }
    [Test]
    public void HeadquartersCatalogCoversThreeLanguagesAndKeepsPlaceholdersAndFallback()
    {
        foreach(var entry in HeadquartersText.Catalog.Entries)
            foreach(var language in new[]{"en","fr","es"})
            { Assert.That(entry.Value.ContainsKey(language),Is.True,entry.Key);Assert.That(Intersect.Localization.GameTextTokens.Compatible(entry.Key,entry.Value[language]),Is.True,entry.Key); }
        Assert.That(HeadquartersText.Get("Guild headquarters","unsupported"),Is.EqualTo("Guild headquarters"));
        Assert.That(HeadquartersText.Get("Untranslated custom guild name","fr"),Is.EqualTo("Untranslated custom guild name"));
    }
    [Test]
    public void HeadquartersPacketsRoundTripThroughRealRegistry()
    {
        var registry=new PacketTypeRegistry(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,typeof(IntersectPacket).Assembly);
        Assert.That(registry.TryRegisterBuiltIn(),Is.True); PackedIntersectPacket.AddKnownTypes(registry.Types);
        foreach(var action in Enum.GetValues<HeadquartersAction>())
        {
            var packet=new HeadquartersRequestPacket{Action=action,TierId=Guid.NewGuid(),Revision=3,InvitationId=Guid.NewGuid(),ConfigurationRevision="revision",Name="Nicolas"};
            var restored=MessagePacker.Instance.Deserialize<HeadquartersRequestPacket>(MessagePacker.Instance.Serialize(packet))!;
            Assert.That(JsonConvert.SerializeObject(restored),Is.EqualTo(JsonConvert.SerializeObject(packet)));
        }
    }
    private sealed class PersistenceContext(DbContextOptions options):DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder b)
        {
            var guild=b.Entity<Guild>();
            foreach(var p in typeof(Guild).GetProperties().Where(p=>p.Name is not (nameof(Guild.Id) or nameof(Guild.HeadquartersJson) or nameof(Guild.BankSlotsCount))))guild.Ignore(p.Name);
            guild.HasKey(g=>g.Id);guild.Property(g=>g.HeadquartersJson).HasColumnName("Headquarters");guild.ToTable("Guilds");
            var slot=b.Entity<GuildBankSlot>();
            foreach(var p in typeof(GuildBankSlot).GetProperties().Where(p=>p.Name is not (nameof(GuildBankSlot.Id) or nameof(GuildBankSlot.ItemId) or nameof(GuildBankSlot.Quantity))))slot.Ignore(p.Name);
            slot.HasKey(s=>s.Id);slot.ToTable("GuildBankSlots");
            foreach(var type in b.Model.GetEntityTypes().Select(e=>e.ClrType).Where(t=>t!=typeof(Guild)&&t!=typeof(GuildBankSlot)).Distinct().ToArray())b.Ignore(type);
            // The narrow fixture persists slots explicitly, in the same SaveChanges transaction.
            // Remove shadow relationships discovered from native navigation attributes before Ignore.
            foreach(var fk in slot.Metadata.GetForeignKeys().ToArray()) slot.Metadata.RemoveForeignKey(fk);
            foreach(var property in slot.Metadata.GetProperties().Where(p=>p.IsShadowProperty()).ToArray()) slot.Metadata.RemoveProperty(property);
        }
    }
    [Test]
    public void HeadquartersSqliteMigrationAndTransactionalFailurePreserveBankAndProgressTogether()
    {
        using var connection=new SqliteConnection("Data Source=:memory:");connection.Open();
        var options=new DbContextOptionsBuilder().UseSqlite(connection).Options;
        using(var setup=new PersistenceContext(options))
        {
            setup.Database.ExecuteSqlRaw("CREATE TABLE Guilds (Id TEXT PRIMARY KEY, BankSlotsCount INTEGER NOT NULL); CREATE TABLE Players (Id TEXT PRIMARY KEY); CREATE TABLE GuildBankSlots (Id TEXT PRIMARY KEY, ItemId TEXT NOT NULL, Quantity INTEGER NOT NULL);");
            var legacyId=Guid.NewGuid();setup.Database.ExecuteSqlInterpolated($"INSERT INTO Guilds(Id,BankSlotsCount) VALUES ({legacyId},40)");
            var migration=new Intersect.Server.Migrations.Sqlite.Player.GuildHeadquarters();
            foreach(var command in setup.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations))setup.Database.ExecuteSqlRaw(command.CommandText);
            Assert.That(setup.Set<Guild>().Single().BankSlotsCount,Is.EqualTo(40));
            Assert.That(setup.Set<Guild>().Single().HeadquartersJson,Is.Null);
            setup.Database.ExecuteSqlRaw("DELETE FROM Guilds");
        }
        var item=Guid.NewGuid();var guild=GuildWithFunds(item);var cfg=Config(item);
        using(var seed=new PersistenceContext(options)){seed.Add(guild);seed.AddRange(guild.Bank);seed.SaveChanges();}
        void Save()
        {using var db=new PersistenceContext(options);db.Update(guild);db.UpdateRange(guild.Bank);db.SaveChanges();}
        using(var fail=new PersistenceContext(options))fail.Database.ExecuteSqlRaw("CREATE TRIGGER reject_upgrade BEFORE UPDATE OF Headquarters ON Guilds WHEN NEW.Headquarters IS NOT NULL BEGIN SELECT RAISE(ABORT,'test disk failure'); END;");
        Assert.Throws<DbUpdateException>(()=>HeadquartersPurchase.Buy(guild,cfg,cfg.Tiers[1].Id,0,0,Save));
        using(var verify=new PersistenceContext(options))
        {Assert.That(verify.Set<Guild>().Single().HeadquartersJson,Is.Null);Assert.That(verify.Set<GuildBankSlot>().Sum(s=>s.Quantity),Is.EqualTo(100));verify.Database.ExecuteSqlRaw("DROP TRIGGER reject_upgrade;");}
        HeadquartersPurchase.Buy(guild,cfg,cfg.Tiers[1].Id,0,0,Save);
        using(var restart=new PersistenceContext(options))
        {var reloaded=restart.Set<Guild>().Single();Assert.That(HeadquartersProfile.Read(reloaded.HeadquartersJson).TierIndex(cfg),Is.EqualTo(1));Assert.That(reloaded.BankSlotsCount,Is.GreaterThanOrEqualTo(80));Assert.That(restart.Set<GuildBankSlot>().Sum(s=>s.Quantity),Is.EqualTo(25));}
    }
}
