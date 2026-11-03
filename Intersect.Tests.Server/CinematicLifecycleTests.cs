using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.Entities;
using Intersect.Server.Entities.Events;
using NUnit.Framework;
using GameEvent = Intersect.Server.Entities.Events.Event;

namespace Intersect.Tests.Server;

[TestFixture]
public sealed class CinematicLifecycleTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static (Player Player, GameEvent Event, BeginCinematicCommand Begin) Create(Guid? map = null)
    {
        var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
        GC.SuppressFinalize(player);
        player.EntityLock = new(); player.EventLookup = new(); player.Variables = [];
        player.Id = Guid.NewGuid(); player.MapId = map ?? Guid.NewGuid(); player.MapInstanceId = Guid.NewGuid();
        player.X = 2; player.Y = 3;
        var descriptor = new EventDescriptor(Guid.NewGuid(), player.MapId, 0, 0);
        var evt = new GameEvent(Guid.NewGuid(), null, player, descriptor) { MapId = player.MapId };
        evt.PageInstance = (EventPageInstance)RuntimeHelpers.GetUninitializedObject(typeof(EventPageInstance));
        GC.SuppressFinalize(evt.PageInstance);
        evt.CallStack.Push(new(descriptor.Pages[0]));
        player.EventLookup[evt.Id] = evt;
        return (player, evt, new() { MapId = player.MapId, Actors = [new() { StartX = 2, StartY = 3 }] });
    }
    private static bool Begin(GameEvent evt, BeginCinematicCommand command) =>
        (bool)typeof(GameEvent).GetMethod("TryBeginCinematic", Private)!.Invoke(evt, [command])!;
    private static bool Active(GameEvent evt) => (bool)typeof(GameEvent).GetProperty("CinematicActive", Private)!.GetValue(evt)!;
    private static void Check(GameEvent evt) => typeof(GameEvent).GetMethod("CheckCinematicLifetime", Private)!.Invoke(evt, null);

    [TestCase("map")]
    [TestCase("instance")]
    [TestCase("death")]
    [TestCase("deadline")]
    [TestCase("stack")]
    public void InterruptedSceneClearsItsOwnedRouteAndControls(string interruption)
    {
        var (player, evt, command) = Create();
        Assert.That(Begin(evt, command), Is.True);
        evt.HoldingPlayer = true;
        player.MoveRoute = new() { Complete = false }; player.MoveRouteSetter = evt.PageInstance;
        switch (interruption)
        {
            case "map": player.MapId = Guid.NewGuid(); break;
            case "instance": player.MapInstanceId = Guid.NewGuid(); break;
            case "death": evt.PlayerHasDied = true; break;
            case "deadline": typeof(GameEvent).GetField("_cinematicDeadline", Private)!.SetValue(evt, Environment.TickCount64 - 1); break;
            case "stack": evt.CallStack.Clear(); break;
        }
        Check(evt);
        Assert.Multiple(() =>
        {
            Assert.That(Active(evt), Is.False);
            Assert.That(evt.HoldingPlayer, Is.False);
            Assert.That(evt.CallStack, Is.Empty);
            Assert.That(player.MoveRoute, Is.Null);
            Assert.That(player.MoveRouteSetter, Is.Null);
        });
    }

    [Test]
    public void CleanupDoesNotCancelAnUnrelatedPlayerRoute()
    {
        var (player, evt, command) = Create(); Assert.That(Begin(evt, command), Is.True);
        var otherOwner = (EventPageInstance)RuntimeHelpers.GetUninitializedObject(typeof(EventPageInstance));
        GC.SuppressFinalize(otherOwner);
        var otherRoute = new EventMoveRoute(); player.MoveRoute = otherRoute; player.MoveRouteSetter = otherOwner;
        evt.CallStack.Clear(); Check(evt);
        Assert.That(player.MoveRoute, Is.SameAs(otherRoute));
        Assert.That(player.MoveRouteSetter, Is.SameAs(otherOwner));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NormalFinishRestoresTheCallersPreviousHold(bool wasHolding)
    {
        var (_, evt, command) = Create(); evt.HoldingPlayer = wasHolding;
        Assert.That(Begin(evt, command), Is.True);
        evt.HoldingPlayer = !wasHolding;
        typeof(GameEvent).GetMethod("FinishCinematic", Private)!.Invoke(evt, [false]);
        Assert.That(evt.HoldingPlayer, Is.EqualTo(wasHolding));
        Assert.That(Active(evt), Is.False);
    }

    [Test]
    public void ConcurrentPlayersKeepIndependentScenesAndInteractionVariables()
    {
        var first = Create(); var second = Create(first.Player.MapId);
        Assert.That(Begin(first.Event, first.Begin), Is.True);
        Assert.That(Begin(second.Event, second.Begin), Is.True);
        var variableId = Guid.NewGuid();
        first.Player.Variables.Add(new PlayerVariable(variableId) { Value = new() { Value = false } });
        second.Player.Variables.Add(new PlayerVariable(variableId) { Value = new() { Value = false } });
        var condition = new VariableIsCondition { VariableId = variableId,
            Comparison = new BooleanVariableComparison { ComparingEqual = true, Value = true } };
        Assert.That(Conditions.MeetsCondition(condition, first.Player, first.Event, null), Is.False);
        first.Player.Variables[0].Value.Value = true;
        Assert.That(Conditions.MeetsCondition(condition, first.Player, first.Event, null), Is.True);
        Assert.That(Conditions.MeetsCondition(condition, second.Player, second.Event, null), Is.False);
        first.Event.CallStack.Clear(); Check(first.Event);
        Assert.That(Active(first.Event), Is.False);
        Assert.That(Active(second.Event), Is.True);
        Assert.That(second.Event.CallStack.Count, Is.EqualTo(1));
    }

    [Test]
    public void WrongAnchorAndOverlappingSceneAreRejected()
    {
        var (player, evt, command) = Create(); player.X++;
        Assert.That(Begin(evt, command), Is.False);
        Assert.That(Active(evt), Is.False);
        player.X--; Assert.That(Begin(evt, command), Is.True);
        Assert.That(Begin(evt, command), Is.False);
    }

    [Test]
    public void AnotherHoldingEventBlocksSceneStart()
    {
        var (player, evt, command) = Create();
        var blocker = new GameEvent(Guid.NewGuid(), null, player, new EventDescriptor(Guid.NewGuid())) { HoldingPlayer = true };
        player.EventLookup[blocker.Id] = blocker;
        Assert.That(Begin(evt, command), Is.False);
        Assert.That(blocker.HoldingPlayer, Is.True);
    }
}
