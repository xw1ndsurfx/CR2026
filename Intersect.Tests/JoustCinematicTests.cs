using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.Jousting;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using NUnit.Framework;

namespace Intersect.Tests;
[TestFixture]
public sealed class JoustCinematicTests
{
    [TestCase(JoustCinematicKind.Introduction, false)]
    [TestCase(JoustCinematicKind.Charge, true)]
    [TestCase(JoustCinematicKind.Impact, true)]
    [TestCase(JoustCinematicKind.Unhorse, true)]
    [TestCase(JoustCinematicKind.Victory, false)]
    public void JoustPresetsCompileThroughNativeDirectorWithCorrectInputOwnership(JoustCinematicKind kind, bool interactive)
    {
        var arena = new JoustArena { MapId = Guid.NewGuid() };
        var source = JoustCinematics.Create(arena, kind, s => s);
        var page = CinematicCompiler.Compile(source, 32, 26);
        var commands = page.CommandLists.Values.First();
        Assert.Multiple(() => {
            Assert.That(page.Cinematic, Is.Not.Null);
            Assert.That(source.AllowPlayerControl, Is.EqualTo(interactive));
            Assert.That(commands.OfType<HoldPlayerCommand>().Any(), Is.EqualTo(!interactive));
            Assert.That(commands.OfType<CinematicCameraCommand>().Any(c => !c.Reset), Is.True);
            Assert.That(commands.OfType<SetMoveRouteCommand>().Any(), Is.False, "The server session owns the two horses.");
        });
    }
    [Test]
    public void InteractiveSceneDoesNotReacquireInputAfterAnEventClip()
    {
        var id = Guid.NewGuid();
        var scene = JoustCinematics.Create(new JoustArena { MapId = Guid.NewGuid() }, JoustCinematicKind.Charge, s => s);
        scene.Clips.Add(new() { Track = CinematicTrack.Event, ResourceId = id, AllowPlayerControl = true, DurationMs = 100 });
        var page = CinematicCompiler.Compile(scene, 32, 26, validEvent: i => i == id);
        Assert.That(page.CommandLists.Values.First().OfType<HoldPlayerCommand>(), Is.Empty);
        Assert.That(new CinematicScene().AllowPlayerControl, Is.False, "Existing scenes keep their historical input lock.");
    }
}
