using System.Reflection;
using Intersect;
using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Mounts;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.Maps;
using Intersect.Server.Mounts;
using MessagePack;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Intersect.Tests.Server;
[TestFixture, NonParallelizable]
public sealed class MountTests
{
    private static readonly FieldInfo Cache = typeof(MountRuntime).GetField("_current", BindingFlags.Static | BindingFlags.NonPublic)!;
    [Test]
    public void AnimationTransitionsAndPlacementRemainDeterministic()
    {
        var mount = MountConfiguration.Prototype().Mounts[0];
        var clock = new MountAnimationClock();
        Assert.That(clock.Sample(mount, false, 0), Is.EqualTo((MountPhase.Idle, 0)));
        Assert.That(clock.Sample(mount, true, 100), Is.EqualTo((MountPhase.Start, 0)));
        Assert.That(clock.Sample(mount, true, 699), Is.EqualTo((MountPhase.Start, 5)));
        Assert.That(clock.Sample(mount, true, 700), Is.EqualTo((MountPhase.Loop, 0)));
        Assert.That(clock.Sample(mount, true, 2000), Is.EqualTo((MountPhase.Loop, 1)));
        Assert.That(clock.Sample(mount, false, 2100), Is.EqualTo((MountPhase.End, 0)));
        Assert.That(clock.Sample(mount, false, 2700), Is.EqualTo((MountPhase.Idle, 0)));
        Assert.That(clock.Sample(mount, true, 2750), Is.EqualTo((MountPhase.Start, 0)));
        mount.Bob = 0; mount.RiderScale = 1.5;
        mount.FrameOffsets = [new() { Phase = MountPhase.Loop, Direction = 2, Frame = 1, X = 3, Y = -2 }];
        var t = MountRiderTransform.AtSeat(mount, 2, MountPhase.Loop, 1, 100, 200, 10, 20, 8, 5, 24, 40);
        Assert.That((10 + 8 + 12) * t.Scale + t.OffsetX, Is.EqualTo(167).Within(.001));
        Assert.That((20 + 5 + 40 * mount.HipRatio) * t.Scale + t.OffsetY, Is.EqualTo(254).Within(.001));
        Assert.That(t.ClipY, Is.EqualTo(265));
        mount.ClipLegs = false;
        Assert.That(MountRiderTransform.AtSeat(mount, 0, MountPhase.Idle, 0, 0, 0, 0, 0, 0, 0, 32, 64).ClipY, Is.EqualTo(double.PositiveInfinity));
    }

    [Test]
    public void ConfigurationRejectsInvalidAssetsAndNumbersAndPacketsRoundTrip()
    {
        var config = MountConfiguration.Prototype();
        Assert.That(MountConfiguration.FromJson(config.ToJson()).Mounts[0].LoopFrames, Is.EqualTo(6));
        var mount = config.Mounts[0];
        foreach (var path in new[] { "../secret.png", "C:/secret.png", "/secret.png", "mounts/../secret.png", "bad\\file.png", "image.jpg" })
        { mount.Idle = path; Assert.That(config.IsValid, Is.False, path); }
        mount.Idle = "mounts/horse.png";
        mount.SpeedMultiplier = double.NaN; Assert.That(config.IsValid, Is.False);
        mount.SpeedMultiplier = 2.01; Assert.That(config.IsValid, Is.False);
        mount.SpeedMultiplier = 1.35; mount.FrameOffsets = [new() { Frame = 100 }];
        Assert.That(config.IsValid, Is.False);
        mount.FrameOffsets = []; mount.Seats = [new()];
        Assert.That(config.IsValid, Is.False);
        var request = new MountRequestPacket { Action = MountAction.Toggle, MountId = Guid.NewGuid() };
        var copied = MessagePackSerializer.Deserialize<MountRequestPacket>(MessagePackSerializer.Serialize(request));
        Assert.That(copied.MountId, Is.EqualTo(request.MountId));
        var packet = new MountStatePacket { ConfigurationJson = MountConfiguration.Prototype().ToJson(), Owned = [request.MountId], OpenWindow = true };
        var state = MessagePackSerializer.Deserialize<MountStatePacket>(MessagePackSerializer.Serialize(packet));
        Assert.That(state.Owned, Is.EqualTo(packet.Owned));
        Assert.That(MountConfiguration.FromJson(state.ConfigurationJson).IsValid, Is.True);
        Assert.That(new PlayerEntityPacket().MountJson, Is.Empty, "Older entity packets default to walking.");
    }

    [Test]
    public void MountedSpeedOwnershipAndDismountRestrictionsAreAuthoritative()
    {
        Options.EnsureCreated();
        var previous = Cache.GetValue(null);
        var config = MountConfiguration.Prototype(); Cache.SetValue(null, config);
        var map = new MapController(Guid.NewGuid()); MapController.Lookup.Set(map.Id, map);
        var player = new Player { Id = Guid.NewGuid(), MapId = map.Id, MapInstanceId = MapInstance.OverworldInstanceId, InGame = true };
        GC.SuppressFinalize(player);
        var mount = config.Mounts[0];
        void ResetCooldown() => typeof(Player).GetField("_lastMountAction", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(player, 0L);
        void Ride() { ResetCooldown(); player.HandleMountAction(new() { Action = MountAction.Toggle, MountId = mount.Id }); }
        try
        {
            player.BaseStats[(int)Stat.Speed] = 20;
            var walking = player.GetMovementTime();
            Assert.That(player.CanRide(mount), Is.True);
            Ride(); Assert.That(player.ActiveMountId, Is.EqualTo(mount.Id));
            Assert.That(player.GetMovementTime(), Is.EqualTo(walking / (float)mount.SpeedMultiplier).Within(.01));
            var second = MountConfiguration.Prototype().Mounts[0]; second.Id = Guid.NewGuid(); second.SpeedMultiplier = 1.6;
            config.Mounts = [mount, second]; ResetCooldown();
            player.HandleMountAction(new() { Action = MountAction.Toggle, MountId = second.Id });
            Assert.That(player.ActiveMountId, Is.EqualTo(second.Id), "Selecting another owned mount switches directly to it.");
            Assert.That(player.GetMovementTime(), Is.EqualTo(walking / 1.6f).Within(.01));
            Ride(); Assert.That(player.ActiveMountId, Is.EqualTo(mount.Id));
            player.CombatTimer = long.MaxValue; player.UpdateMount();
            Assert.That(player.ActiveMountId, Is.EqualTo(Guid.Empty));
            Assert.That(player.GetMovementTime(), Is.EqualTo(walking));
            Ride(); Assert.That(player.ActiveMountId, Is.EqualTo(Guid.Empty));
            player.CombatTimer = 0; map.IsIndoors = true;
            Ride(); Assert.That(player.ActiveMountId, Is.EqualTo(Guid.Empty));
            mount.AllowIndoors = true; Ride(); Assert.That(player.ActiveMountId, Is.EqualTo(mount.Id));
            player.Dismount(); map.IsIndoors = false;
            player.MapInstanceId = Guid.NewGuid(); Ride(); Assert.That(player.ActiveMountId, Is.EqualTo(Guid.Empty));
            player.MapInstanceId = MapInstance.OverworldInstanceId; player.CaptainedShipId = Guid.NewGuid();
            Ride(); Assert.That(player.ActiveMountId, Is.EqualTo(Guid.Empty));
            player.CaptainedShipId = Guid.Empty; player.IsDead = true;
            Ride(); Assert.That(player.ActiveMountId, Is.EqualTo(Guid.Empty));
            player.IsDead = false; mount.Starter = false;
            Ride(); Assert.That(player.ActiveMountId, Is.EqualTo(Guid.Empty));
            player.PetCollection.Mounts.Owned.Add(mount.Id); Ride();
            Assert.That(player.ActiveMountId, Is.EqualTo(mount.Id));
            config.Enabled = false; player.UpdateMount(); Assert.That(player.ActiveMountId, Is.EqualTo(Guid.Empty));
            config.Enabled = true;
            player.PetCollection.Mounts.Favorite = Guid.Empty; ResetCooldown();
            player.HandleMountAction(new() { Action = MountAction.Favorite, MountId = mount.Id });
            Assert.That(player.PetCollection.Mounts.Favorite, Is.EqualTo(Guid.Empty), "A failed persistence attempt rolls back the favorite.");
            mount.UnlockItemId = Guid.NewGuid(); player.PetCollection.Mounts.Owned.Clear();
            Assert.That(player.TryActivateMountItem(mount.UnlockItemId, () => false), Is.True);
            Assert.That(player.PetCollection.Mounts.Owned, Is.Empty, "Failed unlock saves roll back ownership.");
            player.TryActivateMountItem(mount.UnlockItemId, () => throw new IOException());
            Assert.That(player.PetCollection.Mounts.Owned, Is.Empty, "Save exceptions also roll back ownership.");
            player.TryActivateMountItem(mount.UnlockItemId, () => true);
            Assert.That(player.PetCollection.Mounts.Owned, Does.Contain(mount.Id));
            var writes = 0; player.TryActivateMountItem(mount.UnlockItemId, () => { writes++; return true; });
            Assert.That(writes, Is.Zero, "Reusing an unlocked license does not create duplicate writes.");
        }
        finally { Cache.SetValue(null, previous); MapController.Lookup.Delete(map); }
    }

    [Test]
    public void CollectionPersistenceKeepsPetsAndConfigurationReloadsWithoutOverwritingCorruptFiles()
    {
        Options.EnsureCreated();
        var previous = Cache.GetValue(null); var directory = Environment.CurrentDirectory;
        var fixture = Path.Combine(Path.GetTempPath(), "mount-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        var player = new Player(); GC.SuppressFinalize(player);
        try
        {
            player.PetsJson = "{\"OwnedPets\":{},\"ActivePetId\":\"00000000-0000-0000-0000-000000000000\"}";
            Assert.That(player.PetCollection.Mounts.Owned, Is.Empty);
            var id = Guid.NewGuid(); player.PetCollection.Mounts.Owned.Add(id); player.PetCollection.Mounts.Favorite = id;
            var other = new Player(); GC.SuppressFinalize(other); other.PetsJson = player.PetsJson;
            Assert.That(other.PetCollection.Mounts.Favorite, Is.EqualTo(id));
            Assert.That(other.PetCollection.Mounts.Owned, Does.Contain(id));
            Assert.That(other.PetCollection.OwnedPets, Is.Not.Null);
            Environment.CurrentDirectory = fixture;
            MountRuntime.Save(MountConfiguration.Prototype().ToJson()); Cache.SetValue(null, null);
            Assert.That(MountRuntime.Current.Mounts.Length, Is.EqualTo(1));
            File.WriteAllText("resources/mounts.json", "{broken"); Cache.SetValue(null, null);
            Assert.That(MountRuntime.Current.Enabled, Is.False);
            Assert.That(File.ReadAllText("resources/mounts.json"), Is.EqualTo("{broken"));
        }
        finally { Environment.CurrentDirectory = directory; Cache.SetValue(null, previous); Directory.Delete(fixture, true); }
    }
}
