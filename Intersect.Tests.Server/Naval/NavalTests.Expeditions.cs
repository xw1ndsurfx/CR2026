using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;
using Intersect.Framework.Core.Naval;
using Intersect.Network;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Entities;
using Intersect.Server.Naval;
using MessagePack;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Intersect.Tests.Server.Naval;

public sealed partial class NavalTests
{
    private static NavalExpeditionDefinition ExpeditionDefinition(Guid map) => new()
    {
        Enabled = true, Name = "Ocean trial", Stages = [new() { MapId = map, X = 10, Y = 10 }],
    };

    [TestCase(NavalExpeditionMode.Either, 1, true)] [TestCase(NavalExpeditionMode.Either, 2, true)]
    [TestCase(NavalExpeditionMode.SingleShip, 1, true)] [TestCase(NavalExpeditionMode.SingleShip, 2, false)]
    [TestCase(NavalExpeditionMode.Fleet, 1, false)] [TestCase(NavalExpeditionMode.Fleet, 2, true)]
    public void ExpeditionsSupportSingleCrewsAndFleetsWithinAuthoredLimits(NavalExpeditionMode mode, int ships, bool allowed)
    {
        var definition = ExpeditionDefinition(Map); definition.Mode = mode;
        Assert.That(definition.AcceptsFleet(2, ships), Is.EqualTo(allowed));
        Assert.That(definition.AcceptsFleet(1, ships), Is.False);
        Assert.That(definition.AcceptsFleet(33, ships), Is.False);
        Assert.That(definition.AcceptsFleet(4, 5), Is.False);
        Assert.That(definition.IsValid(32, 26), Is.True);
    }

    [Test]
    public void ExpeditionConfigurationPreservesLegacyAndRejectsIncompleteEnabledObjectives()
    {
        var legacy = JsonConvert.DeserializeObject<NavalConfiguration>("{}")!;
        Assert.That(legacy.Expeditions, Is.Empty); Assert.DoesNotThrow(() => legacy.Validate(32, 26));
        var definition = ExpeditionDefinition(Map); legacy.Expeditions = [definition];
        Assert.DoesNotThrow(() => legacy.Validate(32, 26));
        definition.Stages[0].Objective = NavalExpeditionObjective.DefeatPatrol;
        Assert.Throws<InvalidDataException>(() => legacy.Validate(32, 26));
        definition.Stages[0].TargetId = Guid.NewGuid();
        Assert.DoesNotThrow(() => legacy.Validate(32, 26));
        definition.Stages = [definition.Stages[0], definition.Stages[0]];
        Assert.Throws<InvalidDataException>(() => legacy.Validate(32, 26));
        definition.Stages = []; definition.Enabled = false;
        Assert.DoesNotThrow(() => legacy.Validate(32, 26));
        definition.Enabled = true; Assert.Throws<InvalidDataException>(() => legacy.Validate(32, 26));
    }

    [TestCase(false)] [TestCase(true)]
    public void ExpeditionDepartureRequiresEveryMemberReadyAndOnlyLeaderCanStart(bool fleet)
    {
        using var expedition = new ExpeditionWorld(fleet);
        var run = expedition.Propose();
        Assert.That(run.Ships.Length, Is.EqualTo(fleet ? 2 : 1));
        expedition.Request(0, NavalExpeditionAction.Start, run.Id);
        Assert.That(run.Status, Is.EqualTo(NavalExpeditionStatus.Preparing));
        expedition.Request(0, NavalExpeditionAction.Ready, Guid.NewGuid());
        Assert.That(run.Members.Any(m => m.Ready), Is.False, "A foreign run id cannot change readiness.");
        expedition.Request(0, NavalExpeditionAction.Ready, run.Id);
        expedition.Request(1, NavalExpeditionAction.Ready, run.Id);
        expedition.Request(1, NavalExpeditionAction.Start, run.Id);
        Assert.That(run.Status, Is.EqualTo(NavalExpeditionStatus.Preparing));
        expedition.Request(0, NavalExpeditionAction.Start, run.Id);
        Assert.That(run.Status, Is.EqualTo(NavalExpeditionStatus.Active));
        var persisted = expedition.Store.Load(32, 26)[run.Id];
        Assert.That(persisted.Members.All(m => m.Ready), Is.True);
        Assert.That(persisted.Status, Is.EqualTo(NavalExpeditionStatus.Active));
        expedition.Request(0, NavalExpeditionAction.Claim, run.Id);
        Assert.That(expedition.Players[0].NavalCareer.LastExpeditionRewardRunId, Is.EqualTo(Guid.Empty), "An active run cannot be claimed.");
    }

    [Test]
    public void FleetMustGatherEveryShipAndMissingCrewPausesUntilReturn()
    {
        using var expedition = new ExpeditionWorld(true);
        var run = expedition.Start();
        var second = expedition.World.State(expedition.Boats[1]); second.X = 24;
        expedition.Tick(); Assert.That(run.StageIndex, Is.Zero);
        second.X = 10.5;
        expedition.Players[1].InGame = false;
        expedition.Tick(); Assert.That(run.StageIndex, Is.Zero); Assert.That(run.Members[1].MissingSince, Is.GreaterThan(0));
        expedition.Players[1].InGame = true;
        expedition.Tick(); Assert.That(run.Status, Is.EqualTo(NavalExpeditionStatus.Completed));
        Assert.That(expedition.Store.Load(32, 26)[run.Id].Status, Is.EqualTo(NavalExpeditionStatus.Completed));
    }

    [TestCase(false)] [TestCase(true)]
    public void ExpeditionDisconnectPersistsAndExpiredGraceCannotBeClearedByReturning(bool fleet)
    {
        using var expedition = new ExpeditionWorld(fleet); var run = expedition.Start();
        foreach (var player in expedition.Players)
        {
            NavalSystem.DisconnectExpedition(player); player.InGame = false;
        }
        var restored = expedition.Store.Load(32, 26);
        Assert.That(restored[run.Id].Members.All(m => m.MissingSince > 0), Is.True);
        foreach (var member in restored[run.Id].Members) member.MissingSince -= 601000;
        expedition.SetStatic("Expeditions", restored);
        // No participant ticked during the absence. Both return together after reload.
        foreach (var player in expedition.Players) player.InGame = true;
        expedition.Tick();
        Assert.That(restored[run.Id].Status, Is.EqualTo(NavalExpeditionStatus.Failed));
        Assert.That(restored[run.Id].StageIndex, Is.Zero);
        Assert.That(expedition.Store.Load(32, 26)[run.Id].Status, Is.EqualTo(NavalExpeditionStatus.Failed));
    }

    [TestCase("timeout")] [TestCase("absent")] [TestCase("sink")] [TestCase("abandon")]
    public void ExpeditionFailuresCannotLeaveAClaimableReward(string cause)
    {
        using var expedition = new ExpeditionWorld(false); var run = expedition.Start();
        if (cause == "timeout") run.Deadline = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 1;
        if (cause == "absent") { expedition.Players[1].InGame = false; run.Members[1].MissingSince = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 601000; }
        if (cause == "sink") expedition.World.Set(expedition.Boats[0], "Health", 0);
        if (cause == "abandon") expedition.Request(1, NavalExpeditionAction.Leave, run.Id);
        else expedition.Tick();
        Assert.That(run.Status, Is.EqualTo(NavalExpeditionStatus.Failed));
        Assert.That(run.Failure, Is.Not.Empty);
        expedition.Request(0, NavalExpeditionAction.Claim, run.Id);
        Assert.That(expedition.Players[0].NavalCareer.LastExpeditionRewardRunId, Is.EqualTo(Guid.Empty));
    }

    [Test]
    public void ExpeditionCreditsOnlyActualRegisteredPatrolDeathsAndDeduplicatesCallbacks()
    {
        using var expedition = new ExpeditionWorld(true);
        var enemy = expedition.World.Add(18.5, 10.5, true);
        var stage = expedition.Definition.Stages[0]; stage.Objective = NavalExpeditionObjective.DefeatPatrol;
        stage.TargetId = expedition.World.State(enemy).Id; stage.Quantity = 2;
        var run = expedition.Start();
        var ship = expedition.World.State(expedition.Boats[0]);
        expedition.World.Invoke("CreditEnemySink", enemy, ship.Id, ship.CaptainId);
        Assert.That(run.Progress, Is.Zero, "A live patrol cannot satisfy a victory objective.");
        expedition.World.Set(enemy, "Health", 0);
        expedition.World.Invoke("CreditEnemySink", enemy, ship.Id, ship.CaptainId);
        Assert.That(run.Progress, Is.EqualTo(1));
        expedition.World.Invoke("CreditEnemySink", enemy, ship.Id, ship.CaptainId);
        Assert.That(run.Progress, Is.EqualTo(1));
        var evidence = Guid.NewGuid(); var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Assert.That(run.RecordVictory(NavalExpeditionObjective.DefeatMonster, stage.MapId, stage.TargetId, evidence, 0, now), Is.False);
        Assert.That(run.RecordVictory(stage.Objective, Guid.NewGuid(), stage.TargetId, evidence, 0, now), Is.False);
        Assert.That(run.RecordVictory(stage.Objective, stage.MapId, Guid.NewGuid(), evidence, 0, now), Is.False);
        Assert.That(run.RecordVictory(stage.Objective, stage.MapId, stage.TargetId, evidence, 1, now), Is.False);
        Assert.That(run.RecordVictory(stage.Objective, stage.MapId, stage.TargetId, evidence, 0, run.Deadline), Is.False);
        Assert.That(run.RecordVictory(stage.Objective, stage.MapId, stage.TargetId, evidence, 0, now), Is.True);
        Assert.That(run.Status, Is.EqualTo(NavalExpeditionStatus.Completed));
    }

    [Test]
    public void ExpeditionJournalFailureRollsBackAndCorruptFilesAreNeverReplacedByEmptyRuns()
    {
        using var expedition = new ExpeditionWorld(false); var run = expedition.Propose();
        var before = File.ReadAllText(expedition.Files.Path);
        // An existing directory cannot be replaced as a journal file.
        expedition.SetStatic("ExpeditionStore", new NavalExpeditionStore(expedition.Files.Directory));
        expedition.Request(0, NavalExpeditionAction.Ready, run.Id);
        Assert.That(expedition.Runs[run.Id].Members.Any(m => m.Ready), Is.False);
        Assert.That(File.ReadAllText(expedition.Files.Path), Is.EqualTo(before));
        File.WriteAllText(expedition.Files.Path, "{bad journal");
        Assert.Throws<JsonReaderException>(() => expedition.Store.Load(32, 26));
        Assert.That(File.ReadAllText(expedition.Files.Path), Is.EqualTo("{bad journal"));
    }

    [Test]
    public void ExpeditionRewardPersistsReceiptInventoryAndExperienceTogetherAndRetriesSafely()
    {
        using var world = new EnemyTestWorld();
        var item = new ItemDescriptor(Guid.NewGuid()) { ItemType = ItemType.Currency, Stackable = true, MaxInventoryStack = 100 };
        ItemDescriptor.Lookup.Set(item.Id, item);
        try
        {
            var definition = ExpeditionDefinition(Map); definition.RewardItemId = item.Id; definition.RewardQuantity = 5;
            var runId = Guid.NewGuid(); var player = CargoPlayer(); var before = player.NavalFleetJson;
            Assert.That(NavalSystem.CommitExpeditionReward(player, runId, definition, () => false), Is.False);
            Assert.That(player.NavalFleetJson, Is.EqualTo(before)); Assert.That(player.Items[0].ItemId, Is.EqualTo(Guid.Empty));
            Assert.Throws<IOException>(() => NavalSystem.CommitExpeditionReward(player, runId, definition, () => throw new IOException()));
            Assert.That(player.NavalFleetJson, Is.EqualTo(before));
            Assert.That(NavalSystem.CommitExpeditionReward(player, runId, definition, () => true), Is.True);
            Assert.That(player.Items[0].Quantity, Is.EqualTo(5)); Assert.That(player.SailingExperience, Is.EqualTo(250));
            var restarted = CargoPlayer(); restarted.NavalFleetJson = player.NavalFleetJson; restarted.Items[0].Set(player.Items[0].Clone());
            Assert.That(NavalSystem.CommitExpeditionReward(restarted, runId, definition, () => throw new Exception("Duplicate save")), Is.True);
            Assert.That(restarted.Items[0].Quantity, Is.EqualTo(5)); Assert.That(restarted.SailingExperience, Is.EqualTo(250));
            definition.RewardQuantity = 1_000_000;
            Assert.That(NavalSystem.CommitExpeditionReward(restarted, Guid.NewGuid(), definition, () => true), Is.False);
            Assert.That(restarted.Items[0].Quantity, Is.EqualTo(5)); Assert.That(restarted.NavalCareer.LastExpeditionRewardRunId, Is.EqualTo(runId));
        }
        finally { ItemDescriptor.Lookup.Delete(item); }
    }

    [Test]
    public void ExpeditionResumesSavedStageAndRejectsTheSameVictoryAfterRestart()
    {
        using var expedition = new ExpeditionWorld(true);
        expedition.Definition.Stages = [new() { MapId = expedition.World.Map.Id, Objective = NavalExpeditionObjective.DefeatMonster, TargetId = Guid.NewGuid(), Quantity = 2 }, new() { MapId = expedition.World.Map.Id, X = 10, Y = 10 }];
        var run = expedition.Start(); var stage = run.Stage!; var evidence = Guid.NewGuid();
        Assert.That(run.RecordVictory(stage.Objective, stage.MapId, stage.TargetId, evidence, 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), Is.True);
        expedition.Store.Save(expedition.Runs.Values);
        expedition.SetStatic("Expeditions", new Dictionary<Guid, NavalExpeditionRun>());
        expedition.SetStatic("_expeditionsLoaded", false);
        expedition.Tick(); run = expedition.Runs[run.Id];
        Assert.That(run.Progress, Is.EqualTo(1));
        Assert.That(run.RecordVictory(stage.Objective, stage.MapId, stage.TargetId, evidence, 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), Is.False);
        Assert.That(run.RecordVictory(stage.Objective, stage.MapId, stage.TargetId, Guid.NewGuid(), 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), Is.True);
        expedition.Tick(); Assert.That(run.Status, Is.EqualTo(NavalExpeditionStatus.Completed));
        Assert.That(expedition.Store.Load(32, 26)[run.Id].Status, Is.EqualTo(NavalExpeditionStatus.Completed));
    }

    [TestCase(false)] [TestCase(true)]
    public void ExpeditionRecognizesCrewOnMonsterDeckAndNeverRewardsFatalGrasp(bool tooLate)
    {
        using var expedition = new ExpeditionWorld(true);
        var monster = new NavalMonsterDefinition { MapId = expedition.World.Map.Id, AssaultSeconds = 10 };
        expedition.Definition.Stages[0].Objective = NavalExpeditionObjective.DefeatMonster;
        expedition.Definition.Stages[0].TargetId = monster.Id;
        var run = expedition.Start(); var captain = expedition.Players[0]; var ship = expedition.World.State(expedition.Boats[0]);
        var type = typeof(NavalSystem).GetNestedType("EnemyBoardingRun", BindingFlags.NonPublic)!;
        var encounter = Activator.CreateInstance(type, true)!;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Set(string name, object value) => type.GetField(name, flags)!.SetValue(encounter, value);
        var encounterId = (Guid)type.GetField("Id", flags)!.GetValue(encounter)!;
        Set("Ship", ship.Id); Set("Captain", captain.Id); Set("Starting", false); Set("Monster", monster); Set("MonsterBattle", new NavalMonsterBattle(monster, 100, 0));
        ((HashSet<Guid>)type.GetField("Members", flags)!.GetValue(encounter)!).Add(captain.Id);
        var boardings = (IDictionary)expedition.GetStatic("EnemyBoardings")!;
        var participants = (IDictionary)expedition.GetStatic("BoardingPlayers")!;
        boardings.Add(encounterId, encounter); participants.Add(captain.Id, encounterId);
        captain.CaptainedShipId = Guid.Empty; captain.MapId = Guid.NewGuid();
        try
        {
            expedition.World.Invoke("FinishMonsterAssault", encounter, true, tooLate ? 13000L : 4000L);
            Assert.That(run.Status, Is.EqualTo(tooLate ? NavalExpeditionStatus.Active : NavalExpeditionStatus.Completed));
            Assert.That(run.Evidence.Count, Is.EqualTo(tooLate ? 0 : 1));
        }
        finally
        {
            boardings.Remove(encounterId); participants.Remove(captain.Id);
            ((IDictionary)expedition.GetStatic("MonsterReady")!).Remove(monster.Id);
            ((IDictionary)expedition.GetStatic("MonsterAftermaths")!).Remove(encounterId);
        }
    }

    [Test]
    public void ExpeditionReturnRequiresEveryShipStoppedAtTheConfiguredDock()
    {
        using var expedition = new ExpeditionWorld(true);
        var stage = expedition.Definition.Stages[0]; stage.Objective = NavalExpeditionObjective.ReturnToPort; stage.X = 14; stage.Y = 10; stage.Radius = 6;
        var run = expedition.Start(); var second = expedition.World.State(expedition.Boats[1]);
        second.Speed = 1; expedition.Tick(); Assert.That(run.StageIndex, Is.Zero);
        second.Speed = 0; second.MapId = Guid.NewGuid(); expedition.Tick(); Assert.That(run.StageIndex, Is.Zero);
        second.MapId = expedition.World.Map.Id; expedition.Tick(); Assert.That(run.Status, Is.EqualTo(NavalExpeditionStatus.Completed));
    }

    [Test]
    public void ExpeditionRetriesAValidatedCombatVictoryAfterJournalWriteFailure()
    {
        using var expedition = new ExpeditionWorld(false);
        var enemy = expedition.World.Add(18.5, 10.5, true); var state = expedition.World.State(enemy);
        expedition.Definition.Stages[0].Objective = NavalExpeditionObjective.DefeatPatrol; expedition.Definition.Stages[0].TargetId = state.Id;
        var run = expedition.Start(); var own = expedition.World.State(expedition.Boats[0]);
        expedition.SetStatic("ExpeditionStore", new NavalExpeditionStore(expedition.Files.Directory));
        expedition.World.Set(enemy, "Health", 0); expedition.World.Invoke("CreditEnemySink", enemy, own.Id, own.CaptainId);
        Assert.That(expedition.Runs[run.Id].Status, Is.EqualTo(NavalExpeditionStatus.Active));
        expedition.SetStatic("ExpeditionStore", expedition.Store); expedition.Tick();
        Assert.That(expedition.Runs[run.Id].Status, Is.EqualTo(NavalExpeditionStatus.Completed));
        Assert.That(expedition.Store.Load(32, 26)[run.Id].Evidence.Count, Is.EqualTo(1));
    }

    [Test]
    public void ExpeditionTargetsMustBeExistingOceanPatrolsMonstersAndRealReturnDocks()
    {
        using var expedition = new ExpeditionWorld(false);
        var definition = expedition.Definition; var stage = definition.Stages[0];
        var settings = new NavalConfiguration { Expeditions = [definition] };
        Assert.That(NavalSystem.ValidateExpeditionTargets(settings, new HashSet<Guid>()), Is.False);
        var ocean = new HashSet<Guid> { expedition.World.Map.Id };
        Assert.That(NavalSystem.ValidateExpeditionTargets(settings, ocean), Is.True);
        stage.Objective = NavalExpeditionObjective.ReturnToPort; stage.X = stage.Y = 0;
        Assert.That(NavalSystem.ValidateExpeditionTargets(settings, ocean), Is.False);
        stage.X = 14; stage.Y = 10; Assert.That(NavalSystem.ValidateExpeditionTargets(settings, ocean), Is.True);
        stage.Objective = NavalExpeditionObjective.DefeatPatrol; stage.TargetId = Guid.NewGuid();
        Assert.That(NavalSystem.ValidateExpeditionTargets(settings, ocean), Is.False);
        settings.Herbonia = [new() { Enabled = true, Ship = new() { Id = stage.TargetId, MapId = stage.MapId } }];
        Assert.That(NavalSystem.ValidateExpeditionTargets(settings, ocean), Is.True);
        settings.Herbonia[0].Enabled = false; Assert.That(NavalSystem.ValidateExpeditionTargets(settings, ocean), Is.False);
        stage.Objective = NavalExpeditionObjective.DefeatMonster;
        settings.Monsters = [new() { Enabled = true, Id = stage.TargetId, MapId = stage.MapId }];
        Assert.That(NavalSystem.ValidateExpeditionTargets(settings, ocean), Is.True);
        definition.RewardItemId = Guid.NewGuid(); Assert.That(NavalSystem.ValidateExpeditionTargets(settings, ocean), Is.False);
    }

    [Test]
    public void ExpeditionCatalogPreservesEnglishFrenchSpanishAndAllFormattingTokens()
    {
        foreach (var entry in NavalSetupText.Catalog.Entries)
            foreach (var language in new[] { "en", "fr", "es" })
            {
                Assert.That(entry.Value.ContainsKey(language), Is.True, entry.Key + ": " + language);
                Assert.That(Intersect.Localization.GameTextTokens.Compatible(entry.Key, entry.Value[language]), Is.True, entry.Key + ": " + language);
            }
        foreach (var key in Enum.GetNames<NavalExpeditionMode>().Concat(Enum.GetNames<NavalExpeditionObjective>()).Concat(Enum.GetNames<NavalExpeditionStatus>())
            .Concat(new[] { "Naval expeditions", "Ready for departure", "Depart together", "Claim my reward", "Confirm abandon for everyone" }))
            Assert.That(NavalSetupText.Catalog.Entries.ContainsKey(key), Is.True, key);
        Assert.That(NavalSetupText.Get("Naval expeditions", "unsupported"), Is.EqualTo("Naval expeditions"));
    }

    [Test]
    public void ExpeditionPacketsUseRealPolymorphicProtocolWithoutProgressCommands()
    {
        var registry = new PacketTypeRegistry(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, typeof(IntersectPacket).Assembly);
        Assert.That(registry.TryRegisterBuiltIn(), Is.True);
        PackedIntersectPacket.AddKnownTypes(registry.Types);
        foreach (var action in Enum.GetValues<NavalExpeditionAction>())
        {
            IntersectPacket packet = new NavalExpeditionRequestPacket { Action = action, Id = Guid.NewGuid() };
            var restored = MessagePacker.Instance.Deserialize<NavalExpeditionRequestPacket>(MessagePacker.Instance.Serialize(packet))!;
            Assert.That((restored.Action, restored.Id), Is.EqualTo((action, ((NavalExpeditionRequestPacket)packet).Id)));
        }
        IntersectPacket state = new NavalExpeditionPacket { Open = true, Json = JsonConvert.SerializeObject(new NavalExpeditionView { Definition = ExpeditionDefinition(Map) }) };
        var copy = MessagePacker.Instance.Deserialize<NavalExpeditionPacket>(MessagePacker.Instance.Serialize(state))!;
        Assert.That(copy.Open, Is.True); Assert.That(JsonConvert.DeserializeObject<NavalExpeditionView>(copy.Json)!.Definition!.Name, Is.EqualTo("Ocean trial"));
    }

    private sealed class ExpeditionWorld : IDisposable
    {
        private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Static;
        internal readonly EnemyTestWorld World = new();
        internal readonly NavalSetupFiles Files = new();
        internal readonly NavalExpeditionStore Store;
        internal readonly Player[] Players;
        internal readonly object[] Boats;
        internal readonly NavalExpeditionDefinition Definition;
        internal Dictionary<Guid, NavalExpeditionRun> Runs => (Dictionary<Guid, NavalExpeditionRun>)GetStatic("Expeditions")!;
        private readonly Dictionary<string, object?> _before = [];
        private readonly IDictionary _online = (IDictionary)typeof(Player).GetField("OnlinePlayersById", Flags)!.GetValue(null)!;
        private long _time = 10000;
        internal object? GetStatic(string name) => typeof(NavalSystem).GetField(name, Flags)!.GetValue(null);
        internal void SetStatic(string name, object value) => typeof(NavalSystem).GetField(name, Flags)!.SetValue(null, value);
        internal ExpeditionWorld(bool fleet)
        {
            foreach (var name in new[] { "ExpeditionStore", "Expeditions", "_expeditionsLoaded", "_expeditionLoadRetry" }) _before[name] = GetStatic(name);
            Store = new(Files.Path); SetStatic("ExpeditionStore", Store); SetStatic("Expeditions", new Dictionary<Guid, NavalExpeditionRun>()); SetStatic("_expeditionsLoaded", true);
            Boats = fleet ? [World.Add(10.5, 10.5, false), World.Add(10.5, 12.5, false)] : [World.Add(10.5, 10.5, false)];
            World.Map.Attributes[14, 10] = new MapDockAttribute(); World.Map.Attributes[14, 12] = new MapDockAttribute();
            Players = [new Player { Id = World.State(Boats[0]).CaptainId }, new Player { Id = fleet ? World.State(Boats[1]).CaptainId : Guid.NewGuid() }];
            foreach (var player in Players)
            {
                GC.SuppressFinalize(player); player.Name = "Sailor " + Array.IndexOf(Players, player); player.InGame = true; player.MapId = World.Map.Id; player.Party = [..Players]; player.NavalFleetJson = "[]";
                typeof(Player).GetProperty("User")!.SetValue(player, RuntimeHelpers.GetUninitializedObject(typeof(User)));
                _online.Add(player.Id, player);
            }
            Players[0].CaptainedShipId = World.State(Boats[0]).Id;
            if (fleet) Players[1].CaptainedShipId = World.State(Boats[1]).Id;
            else { Players[1].BoardedShipId = World.State(Boats[0]).Id; AddCrewPresence(World, Boats[0], Players[1].Id, 10000); }
            Definition = ExpeditionDefinition(World.Map.Id); Definition.Mode = fleet ? NavalExpeditionMode.Fleet : NavalExpeditionMode.SingleShip;
            World.Configure(new() { Enabled = true, CannonsEnabled = true, Expeditions = [Definition] });
        }
        internal void Request(int player, NavalExpeditionAction action, Guid id) => NavalSystem.Expedition(Players[player], new() { Action = action, Id = id }, _time += 1000);
        internal NavalExpeditionRun Propose() { Request(0, NavalExpeditionAction.Propose, Definition.Id); Assert.That(Runs.Count, Is.EqualTo(1)); return Runs.Values.Single(); }
        internal NavalExpeditionRun Start() { var run = Propose(); Request(0, NavalExpeditionAction.Ready, run.Id); Request(1, NavalExpeditionAction.Ready, run.Id); Request(0, NavalExpeditionAction.Start, run.Id); Assert.That(run.Status, Is.EqualTo(NavalExpeditionStatus.Active)); return run; }
        internal void Tick() => World.Invoke("UpdateNavalExpeditions", Players[0], _time += 1000);
        public void Dispose()
        {
            foreach (var player in Players)
            {
                _online.Remove(player.Id); player.Party.Clear(); player.CaptainedShipId = player.BoardedShipId = Guid.Empty;
                typeof(Player).GetProperty("User")!.SetValue(player, null); player.Dispose();
            }
            foreach (var pair in _before) typeof(NavalSystem).GetField(pair.Key, Flags)!.SetValue(null, pair.Value);
            World.Dispose(); Files.Dispose();
        }
    }
}
