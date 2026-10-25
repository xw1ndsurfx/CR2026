using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Client.Entities;
using Intersect.Framework.Core.Mounts;
using NUnit.Framework;

namespace Intersect.Tests.Client;
[TestFixture]
public sealed class JoustClientTests
{
    [Test]
    public void JoustingLobbyActivityPreservesExistingWireIdsAndIsTranslated()
    {
        Assert.That((int)Intersect.Network.Packets.PartyLobbies.LobbyActivity.PvP, Is.EqualTo(12));
        Assert.That((int)Intersect.Network.Packets.PartyLobbies.LobbyActivity.Jousting, Is.EqualTo(13));
        foreach (var locale in new[] { "en", "fr", "es" })
            Assert.That(Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get("Royal Jousting", locale), Is.Not.Empty);
    }
    [Test]
    public void SharedArenaActorReusesValidatedIntegratedMountsAndRejectsOtherSheets()
    {
        var entity = (Entity)RuntimeHelpers.GetUninitializedObject(typeof(Entity));
        var field = typeof(Entity).GetField("_arenaMount", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var config = MountConfiguration.Prototype();
        entity.ApplyArenaMount(config.ToJson());
        Assert.That(field.GetValue(entity), Is.Null, "An ordinary riderless mount is not an arena actor.");
        config.Mounts[0].IntegratedRider = true;
        entity.ApplyArenaMount(config.ToJson());
        Assert.That(((MountDefinition)field.GetValue(entity)!).Id, Is.EqualTo(config.Mounts[0].Id));
        entity.ApplyArenaMount("{malformed"); Assert.That(field.GetValue(entity), Is.Null);
        entity.ApplyArenaMount(new string('x', 20001)); Assert.That(field.GetValue(entity), Is.Null);
        entity.ApplyArenaMount(""); Assert.That(field.GetValue(entity), Is.Null);
    }
    [Test]
    public void ScriptedMovementUsesServerDurationWithoutApplyingMountSpeedTwice()
    {
        var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player)); GC.SuppressFinalize(player);
        player.ApplyMountJson(MountConfiguration.Prototype().ToJson());
        player.ServerMovementMilliseconds = 564;
        Assert.That(player.GetMovementTime(), Is.EqualTo(564));
        var actor = (Entity)RuntimeHelpers.GetUninitializedObject(typeof(Entity));
        actor.ServerMovementMilliseconds = 236;
        Assert.That(actor.GetMovementTime(), Is.EqualTo(236));
        var clock = new MountAnimationClock(); var mount = MountConfiguration.Prototype().Mounts[0];
        Assert.That(clock.Sample(mount, actor.MountMovementActive, 0).Phase, Is.EqualTo(MountPhase.Start));
        actor.IsMoving = false; // A network interpolation gap must not restart the six-frame launch.
        Assert.That(clock.Sample(mount, actor.MountMovementActive, 700).Phase, Is.EqualTo(MountPhase.Loop));
        actor.ServerMovementMilliseconds = 0;
        Assert.That(clock.Sample(mount, actor.MountMovementActive, 800).Phase, Is.EqualTo(MountPhase.End));
    }
}
