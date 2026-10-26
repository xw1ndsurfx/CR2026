using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Framework.Core.Archery;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Archery;
using Intersect.Server.Entities;
using Intersect.Server.Maps;
using Intersect.Server.Networking;
using MessagePack;
using NUnit.Framework;

namespace Intersect.Tests.Server;

[TestFixture, NonParallelizable]
public sealed partial class RoyalCombatTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo Config = typeof(RoyalCombatRuntime).GetField("_current", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static ArcheryMatch? Match(Player player) => (ArcheryMatch?)typeof(Player).GetField("_archery", Hidden)!.GetValue(player);
    private static void Tick(Player player, long now) => typeof(Player).GetMethod("UpdateArchery", Hidden)!.Invoke(player, [(long?)now]);
    private sealed class World : IDisposable
    {
        private readonly object? _previous = Config.GetValue(null);
        internal readonly MapController Map;
        internal readonly Player Player;
        internal readonly ArcheryRange Range;
        internal World()
        {
            Options.EnsureCreated(); Map = new(Guid.NewGuid()); MapController.Lookup.Set(Map.Id, Map);
            Range = new() { MapId = Map.Id, StartX = 4, StartY = 4, Enabled = true };
            Config.SetValue(null, new RoyalCombatConfiguration { Ranges = [Range] });
            Player = new() { MapId = Map.Id, MapInstanceId = MapInstance.OverworldInstanceId, InGame = true,
                X = 4, Y = 4, Client = (Client)RuntimeHelpers.GetUninitializedObject(typeof(Client)) };
            GC.SuppressFinalize(Player);
        }
        internal void Start() => Player.HandleArcheryRequest(new() { Action = ArcheryAction.Start, RangeId = Range.Id });
        public void Dispose()
        {
            if (Match(Player) is { } match) Player.HandleArcheryRequest(new() { Action = ArcheryAction.Leave, MatchId = match.Id });
            Player.Client = null; MapController.Lookup.Delete(Map); Config.SetValue(null, _previous);
        }
    }
    [Test]
    public void StartValidatesThePositionAndLocksWorldActionsUntilLeave()
    {
        using var world = new World(); world.Player.X++; world.Start(); Assert.That(Match(world.Player), Is.Null);
        typeof(Player).GetField("_archeryStartAt", Hidden)!.SetValue(world.Player, 0L);
        world.Player.X--; world.Start(); var match = Match(world.Player)!;
        Assert.That(match, Is.Not.Null);
        Assert.That(world.Player.CanUseLobbyTravel, Is.False);
        Assert.That(world.Player.CanAttack(world.Player, null), Is.False);
        world.Player.HandleArcheryRequest(new() { Action = ArcheryAction.Leave, MatchId = Guid.NewGuid() });
        Assert.That(match.Done, Is.False, "Wrong-session packets cannot terminate training.");
        world.Player.HandleArcheryRequest(new() { Action = ArcheryAction.Leave, MatchId = match.Id });
        Assert.That(match.Phase, Is.EqualTo(ArcheryPhase.Abandoned));
        Assert.That(world.Player.IsArcheryActive, Is.False);
    }
    [TestCase("disconnect")]
    [TestCase("map")]
    [TestCase("instance")]
    [TestCase("position")]
    [TestCase("timeout")]
    public void InterruptionsEndTheRealServerSession(string reason)
    {
        using var world = new World(); world.Start(); var match = Match(world.Player)!;
        Assert.That(match, Is.Not.Null);
        switch (reason)
        {
            case "disconnect": world.Player.Client = null; break;
            case "map": world.Player.MapId = Guid.NewGuid(); break;
            case "instance": world.Player.MapInstanceId = Guid.NewGuid(); break;
            case "position": world.Player.Y++; break;
        }
        Tick(world.Player, match.Started + (reason == "timeout" ? 300000 : 1));
        Assert.That(match.Phase, Is.EqualTo(reason == "timeout" ? ArcheryPhase.Finished : ArcheryPhase.Abandoned));
        Assert.That(world.Player.IsArcheryActive, Is.False);
    }
    [Test]
    public void SqliteProgressSurvivesReloadSeparatesCharactersAndCategoriesAndCapsAwards()
    {
        var previous = RoyalCombatRuntime.ProgressPath;
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
        RoyalCombatRuntime.ProgressPath = path;
        try
        {
            var character = Guid.NewGuid(); var other = Guid.NewGuid(); var category = new MasteryCategory();
            Assert.That(RoyalCombatRuntime.Load(character), Is.Empty);
            Assert.That(RoyalCombatRuntime.Award(character, category, 100), Is.EqualTo(100));
            Assert.That(RoyalCombatRuntime.Load(character)[category.Key], Is.EqualTo(100));
            Assert.That(RoyalCombatRuntime.Load(other), Is.Empty);
            RoyalCombatRuntime.Award(character, new() { Key = "swords" }, 5);
            Assert.That(RoyalCombatRuntime.Load(character)[category.Key], Is.EqualTo(100));
            category.Cap = 2;
            Assert.That(RoyalCombatRuntime.Award(character, category, 5000), Is.EqualTo(100));
            Assert.Throws<ArgumentOutOfRangeException>(() => RoyalCombatRuntime.Award(character, category, -1));
        }
        finally { RoyalCombatRuntime.ProgressPath = previous; if (File.Exists(path)) File.Delete(path); }
    }
    [Test]
    public void MasteryUsesTheSourceWeaponMappingRatherThanCurrentlyEquippedWeapon()
    {
        using var world = new World();
        var weapon = new ItemDescriptor(Guid.NewGuid()); var other = new ItemDescriptor(Guid.NewGuid());
        Config.SetValue(null, new RoyalCombatConfiguration { Masteries = [new() { WeaponIds = [weapon.Id] }] });
        typeof(Player).GetField("_masteryExperience", Hidden)!.SetValue(world.Player, new Dictionary<string, long> { ["archery"] = 100 });
        Assert.That(world.Player.MasteryMultiplier(weapon), Is.EqualTo(1.005).Within(.00001));
        Assert.That(world.Player.MasteryMultiplier(other), Is.EqualTo(1));
        Assert.That(world.Player.MasteryMultiplier(null), Is.EqualTo(1));
    }
    [Test]
    public void NewPacketsRoundTripWithoutClientScoreOrExperienceFields()
    {
        var packet = new ArcheryRequestPacket { Action = ArcheryAction.Release, MatchId = Guid.NewGuid(), Sequence = 3, Yaw = .02, Pitch = .1,
            CategoryKey = "archery", TalentKey = "steady-aim", ItemId = Guid.NewGuid(), WeaponId = Guid.NewGuid(), RequestId = Guid.NewGuid(), Round = 2, Shot = 7 };
        var received = MessagePackSerializer.Deserialize<ArcheryRequestPacket>(MessagePackSerializer.Serialize(packet));
        Assert.That(received.MatchId, Is.EqualTo(packet.MatchId));
        Assert.That(received.Sequence, Is.EqualTo(3));
        Assert.That(received.CategoryKey, Is.EqualTo(packet.CategoryKey));
        Assert.That(received.ItemId, Is.EqualTo(packet.ItemId));
        Assert.That(received.WeaponId, Is.EqualTo(packet.WeaponId));
        Assert.That(received.RequestId, Is.EqualTo(packet.RequestId));
        Assert.That(received.Round, Is.EqualTo(2)); Assert.That(received.Shot, Is.EqualTo(7));
        var state = new ArcheryStatePacket { ViewJson = "{}", MasteriesJson = "{}", MasteryRanksJson = "{\"archery\":{}}", AmmunitionJson = "{\"Entries\":[]}" };
        var decoded = MessagePackSerializer.Deserialize<ArcheryStatePacket>(MessagePackSerializer.Serialize(state));
        Assert.That(decoded.MasteriesJson, Is.EqualTo(state.MasteriesJson));
        Assert.That(decoded.MasteryRanksJson, Is.EqualTo(state.MasteryRanksJson));
        Assert.That(decoded.AmmunitionJson, Is.EqualTo(state.AmmunitionJson));
    }
}
