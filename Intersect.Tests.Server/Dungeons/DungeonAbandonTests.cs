using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Dungeons;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Localization;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Dungeons;
using Intersect.Server.Entities;
using MessagePack;
using NUnit.Framework;

namespace Intersect.Tests.Server.Dungeons;

[TestFixture, NonParallelizable]
public sealed class DungeonAbandonTests
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly Type RunType = typeof(DungeonRunRuntime).GetNestedType("Run", BindingFlags.NonPublic)!;
    private static IDictionary Runs => (IDictionary)typeof(DungeonRunRuntime).GetField("RunsByInstance", Static)!.GetValue(null)!;

    private sealed class AbandonPlayer : Player
    {
        public int ExitWarps;
        public override void Warp(Guid map, float x, float y, Direction direction, bool adminWarp = false,
            int zOverride = 0, bool mapSave = false, bool fromWarpEvent = false,
            MapInstanceType? mapInstanceType = null, bool fromLogin = false, bool forceInstanceChange = false)
        {
            ExitWarps++;
            MapId = map;
            MapInstanceId = Guid.Empty;
            InstanceType = mapInstanceType ?? MapInstanceType.Overworld;
        }
    }

    [Test]
    public void AbandonRemovesOnlyTheRequesterAndLastDepartureCleansTheRun()
    {
        Options.EnsureCreated();
        var dungeon = new DungeonDefinition { ExitMapId = Guid.NewGuid(), MapIds = [Guid.NewGuid()] };
        var instance = Guid.NewGuid();
        var run = Activator.CreateInstance(RunType, true)!;
        RunType.GetProperty("Dungeon")!.SetValue(run, dungeon);
        RunType.GetProperty("MapInstanceId")!.SetValue(run, instance);
        RunType.GetProperty("LivesRemaining")!.SetValue(run, 3);
        var runId = (Guid)RunType.GetProperty("Id")!.GetValue(run)!;
        var participants = (ConcurrentDictionary<Guid, byte>)RunType.GetProperty("Participants")!.GetValue(run)!;
        AbandonPlayer CreatePlayer()
        {
            var player = (AbandonPlayer)RuntimeHelpers.GetUninitializedObject(typeof(AbandonPlayer));
            GC.SuppressFinalize(player);
            player.EntityLock = new(); player.Variables = []; player.Id = Guid.NewGuid();
            player.MapId = dungeon.MapIds[0]; player.MapInstanceId = instance;
            participants[player.Id] = 0;
            return player;
        }
        var first = CreatePlayer(); var second = CreatePlayer();
        var loot = (IDictionary)RunType.GetField("ChestLoot")!.GetValue(run)!;
        loot[first.Id] = new (Guid, int)[] { (Guid.NewGuid(), 5) };
        var config = typeof(DungeonConfigurationRuntime).GetField("_current", Static)!;
        var previous = config.GetValue(null);
        config.SetValue(null, new DungeonConfiguration { Dungeons = [dungeon] });
        Runs.Add(instance, run);
        try
        {
            Assert.That(DungeonRunRuntime.TryAbandon(first, Guid.NewGuid()), Is.False, "Reject a stale confirmation.");
            first.MapId = Guid.NewGuid();
            Assert.That(DungeonRunRuntime.TryAbandon(first, runId), Is.False, "Require the player to be in this dungeon.");
            first.MapId = dungeon.MapIds[0];
            participants.TryRemove(first.Id, out _);
            Assert.That(DungeonRunRuntime.TryAbandon(first, runId), Is.False, "An outsider cannot leave another player's run.");
            participants[first.Id] = 0;
            RunType.GetProperty("Finished")!.SetValue(run, true);
            Assert.That(DungeonRunRuntime.TryAbandon(first, runId), Is.False, "A completed run cannot be abandoned.");
            RunType.GetProperty("Finished")!.SetValue(run, false);
            RunType.GetField("CompletionStarted")!.SetValue(run, true);
            Assert.That(DungeonRunRuntime.TryAbandon(first, runId), Is.True);
            Assert.That(loot.Contains(first.Id), Is.False, "Unclaimed rewards are discarded for the departing player.");
            Assert.That(first.MapId, Is.EqualTo(dungeon.ExitMapId));
            Assert.That(first.ExitWarps, Is.EqualTo(1));
            Assert.That(participants.Keys, Is.EquivalentTo(new[] { second.Id }));
            Assert.That(Runs.Contains(instance), Is.True);
            Assert.That(RunType.GetProperty("Finished")!.GetValue(run), Is.False);
            Assert.That(RunType.GetProperty("LivesRemaining")!.GetValue(run), Is.EqualTo(3));
            Assert.That(second.MapInstanceId, Is.EqualTo(instance));
            Assert.That(second.ExitWarps, Is.Zero);
            Assert.That(DungeonRunRuntime.TryAbandon(first, runId), Is.False, "A duplicate request has no effects.");
            Assert.That(first.ExitWarps, Is.EqualTo(1));
            Assert.That(DungeonRunRuntime.ClaimChest(first, instance), Is.False, "Abandoned players cannot collect rewards.");
            Assert.That(DungeonRunRuntime.TryAbandon(second, runId), Is.True);
            Assert.That(Runs.Contains(instance), Is.False);
            Assert.That(RunType.GetProperty("Finished")!.GetValue(run), Is.True);
            Assert.That(second.MapId, Is.EqualTo(dungeon.ExitMapId));
        }
        finally { Runs.Remove(instance); config.SetValue(null, previous); }
    }

    [Test]
    public void AbandonPacketsRoundTripAndAllNewTextsHaveCompatibleTranslations()
    {
        var id = Guid.NewGuid();
        var request = MessagePackSerializer.Deserialize<AbandonDungeonPacket>(
            MessagePackSerializer.Serialize(new AbandonDungeonPacket { RunId = id }));
        Assert.That(request.RunId, Is.EqualTo(id));
        var state = MessagePackSerializer.Deserialize<DungeonRunStatePacket>(
            MessagePackSerializer.Serialize(new DungeonRunStatePacket { RunId = id, Status = DungeonRunStatus.Abandoned }));
        Assert.That(state.RunId, Is.EqualTo(id));
        Assert.That(state.Status, Is.EqualTo(DungeonRunStatus.Abandoned));
        foreach (var text in new[] { "Abandon dungeon", "DUNGEON ABANDONED", "You abandoned the dungeon.",
            "Leave this dungeon? You will return to the exit and forfeit unclaimed rewards. Other players can continue." })
        {
            foreach (var language in GameTextCatalog.Languages)
            {
                Assert.That(QualityOfLifeText.Catalog.Entries[text].ContainsKey(language), Is.True);
                Assert.That(GameTextTokens.Compatible(text, QualityOfLifeText.Get(text, language)), Is.True);
            }
            Assert.That(QualityOfLifeText.Get(text, "unknown"), Is.EqualTo(text));
        }
    }
}
