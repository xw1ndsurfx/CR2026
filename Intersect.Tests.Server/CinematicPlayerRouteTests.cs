using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Server.Entities;
using NUnit.Framework;
using GameEvent = Intersect.Server.Entities.Events.Event;
using EventPageInstance = Intersect.Server.Entities.Events.EventPageInstance;

namespace Intersect.Tests.Server;

[TestFixture]
public sealed class CinematicPlayerRouteTests
{
    private sealed class RoutePlayer : Player
    {
        public bool TerrainBlocked, ThrowOnMove, CheckConcurrentInput, ConcurrentInputBlocked;
        public int Moves;
        public bool RunNativeRoute() => base.ProcessMoveRoute(this, Environment.TickCount64);
        // Substitute only terrain; retain the real Player three-argument overload
        // (death/crafting/Event holds), native route interpreter and action completion.
        public override bool CanMoveInDirection(Direction direction, out MovementBlockerType blockerType,
            out EntityType entityType, out Entity? blockingEntity)
        { blockerType = default; entityType = default; blockingEntity = null; return !TerrainBlocked; }
        public override float GetMovementTime() => 0;
        public override void Move(Direction direction, Player forPlayer, bool doNotUpdate = false, bool correction = false)
        {
            if (ThrowOnMove) throw new InvalidOperationException("movement fixture failure");
            if (CheckConcurrentInput)
            {
                var thread = new Thread(() => ConcurrentInputBlocked = !CanMoveInDirection(Direction.Down));
                thread.Start();
                if (!thread.Join(TimeSpan.FromSeconds(10))) throw new TimeoutException("Concurrent input fixture timed out.");
            }
            Moves++; Y++; MoveTimer = long.MaxValue;
        }
    }

    private static (RoutePlayer Player, GameEvent Event) Create(bool cinematic = true)
    {
        Options.EnsureCreated();
        var player = (RoutePlayer)RuntimeHelpers.GetUninitializedObject(typeof(RoutePlayer));
        GC.SuppressFinalize(player);
        player.EntityLock = new(); player.EventLookup = new(); player.Variables = [];
        player.Id = Guid.NewGuid(); player.MapId = Guid.NewGuid(); player.X = 2; player.Y = 3;
        var descriptor = new EventDescriptor(Guid.NewGuid(), player.MapId, 0, 0);
        var evt = new GameEvent(Guid.NewGuid(), null, player, descriptor) { MapId = player.MapId };
        var page = (EventPageInstance)RuntimeHelpers.GetUninitializedObject(typeof(EventPageInstance));
        GC.SuppressFinalize(page); page.MyEventIndex = evt; page.MyPage = descriptor.Pages[0]; evt.PageInstance = page;
        evt.CallStack.Push(new(descriptor.Pages[0])); player.EventLookup[evt.Id] = evt;
        if (cinematic)
        {
            var begin = new BeginCinematicCommand { MapId = player.MapId };
            Assert.That((bool)typeof(GameEvent).GetMethod("TryBeginCinematic", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(evt, [begin])!, Is.True);
        }
        evt.HoldingPlayer = true;
        player.MoveRoute = new() { Actions = [new() { Type = MoveRouteEnum.MoveDown }] };
        player.MoveRouteSetter = page;
        return (player, evt);
    }

    [Test]
    public void NativeSceneRouteAdvancesWhileManualMovementRemainsHeld()
    {
        var (player, evt) = Create();
        Assert.That(player.CanMoveInDirection(Direction.Down), Is.False);
        player.RunNativeRoute();
        Assert.Multiple(() =>
        {
            Assert.That(player.Y, Is.EqualTo(4)); Assert.That(player.Moves, Is.EqualTo(1));
            Assert.That(player.MoveRoute.Complete, Is.True); Assert.That(player.MoveRoute.ActionIndex, Is.EqualTo(1));
            Assert.That(evt.HoldingPlayer, Is.True);
            Assert.That(player.CanMoveInDirection(Direction.Down), Is.False);
        });
    }

    [Test]
    public void ConcurrentClientMovementCannotBorrowTheServerRouteScope()
    {
        var (player, _) = Create(); player.CheckConcurrentInput = true;
        player.RunNativeRoute();
        Assert.That(player.Moves, Is.EqualTo(1));
        Assert.That(player.ConcurrentInputBlocked, Is.True);
    }

    [TestCase("terrain")]
    [TestCase("dead")]
    [TestCase("other-hold")]
    [TestCase("legacy")]
    public void CollisionDeathOtherEventsAndHistoricalHoldsRemainAuthoritative(string reason)
    {
        var (player, _) = Create(cinematic: reason != "legacy");
        player.TerrainBlocked = reason == "terrain"; player.IsDead = reason == "dead";
        if (reason == "other-hold")
        {
            var blocker = new GameEvent(Guid.NewGuid(), null, player, new EventDescriptor(Guid.NewGuid())) { HoldingPlayer = true };
            player.EventLookup[blocker.Id] = blocker;
        }
        player.RunNativeRoute();
        Assert.That(player.Moves, Is.Zero); Assert.That(player.MoveRoute.Complete, Is.False);
    }

    [Test]
    public void FailedMovementNeverLeavesTheCinematicPermissionEnabled()
    {
        var (player, _) = Create(); player.ThrowOnMove = true;
        Assert.Throws<InvalidOperationException>(() => player.RunNativeRoute());
        Assert.That(player.CanMoveInDirection(Direction.Down), Is.False);
    }

    [TestCase("map")]
    [TestCase("instance")]
    [TestCase("page")]
    public void StaleSceneRoutesCannotMoveEvenWhenInputWasReleased(string reason)
    {
        var (player, evt) = Create(); evt.HoldingPlayer = false;
        if (reason == "map") player.MapId = Guid.NewGuid();
        if (reason == "instance") player.MapInstanceId = Guid.NewGuid();
        if (reason == "page") evt.PageInstance = null;
        player.RunNativeRoute();
        Assert.That(player.Moves, Is.Zero);
        Assert.That(player.MoveRoute.Complete, Is.False);
    }
}
