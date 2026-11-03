using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Server.Entities.Events;
using NUnit.Framework;

namespace Intersect.Tests.Server;

[TestFixture]
public sealed class CinematicActorTests
{
    [TestCase(EventMovementType.Random)]
    [TestCase(EventMovementType.None)]
    [TestCase(EventMovementType.MoveRoute)]
    public void ReservationStopsAutonomousMovementAndRestoresNativeState(EventMovementType movement)
    {
        // Isolate movement state from constructors that spawn entities and send packets.
        var actor = (EventPageInstance)RuntimeHelpers.GetUninitializedObject(typeof(EventPageInstance));
        actor.MovementType = movement;
        actor.MovementSpeed = EventMovementSpeed.Slower;
        actor.Speed = 10; // Native SetMovementSpeed can differ from the enum property setter.
        actor.MovementFreq = (EventMovementFrequency)2;
        actor.MoveRoute = new EventMoveRoute
        {
            Complete = true, ActionIndex = 1, RepeatRoute = true,
            Actions = [new() { Type = MoveRouteEnum.MoveLeft }],
        };
        var directionFix = typeof(EventPageInstance).GetField("mDirectionFix", BindingFlags.Instance | BindingFlags.NonPublic)!;
        directionFix.SetValue(actor, true);
        var restore = (Action)typeof(EventPageInstance).GetMethod("CaptureCinematicMovement", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(actor, null)!;
        Assert.That(actor.MovementType, Is.EqualTo(EventMovementType.None), "Actor must remain at its anchor before the first clip.");

        actor.MovementType = EventMovementType.MoveRoute;
        actor.MovementSpeed = EventMovementSpeed.Fastest;
        actor.MovementFreq = 0;
        directionFix.SetValue(actor, false);
        actor.MoveRoute.Actions.Clear(); actor.MoveRoute.Complete = false; actor.MoveRoute.ActionIndex = 0;
        restore();

        Assert.Multiple(() =>
        {
            Assert.That(actor.MovementType, Is.EqualTo(movement));
            Assert.That(actor.MovementSpeed, Is.EqualTo(EventMovementSpeed.Slower));
            Assert.That(actor.Speed, Is.EqualTo(10));
            Assert.That(actor.MovementFreq, Is.EqualTo((EventMovementFrequency)2));
            Assert.That(directionFix.GetValue(actor), Is.True);
            Assert.That(actor.MoveRoute.Complete, Is.True);
            Assert.That(actor.MoveRoute.ActionIndex, Is.EqualTo(1));
            Assert.That(actor.MoveRoute.RepeatRoute, Is.True);
            Assert.That(actor.MoveRoute.Actions.Single().Type, Is.EqualTo(MoveRouteEnum.MoveLeft));
        });
    }
}
