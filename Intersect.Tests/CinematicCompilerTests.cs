using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Intersect.Tests;

[TestFixture]
public sealed class CinematicCompilerTests
{
    private static CinematicScene Scene() => new()
    {
        Name = "Test", MapId = Guid.NewGuid(), DurationMs = 5000,
        Actors = [new() { Name = "Player", StartX = 2, StartY = 3 }],
    };
    private static CinematicClip Path(int start = 0) => new()
    {
        Track = CinematicTrack.Movement, StartMs = start, DurationMs = 1000,
        Points = [new() { X = 2, Y = 3 }, new() { X = 4, Y = 4 }],
    };
    private static List<EventCommand> Commands(EventPage page) => page.CommandLists.Values.First();

    [Test]
    public void VisualPathUsesNativeCardinalDirectionsAndFiniteCollisionAwareRoutes()
    {
        var scene = Scene(); scene.Clips.Add(Path());
        var root = Commands(CinematicCompiler.Compile(scene, 32, 32));
        var route = root.OfType<SetMoveRouteCommand>().Single().Route;
        Assert.That(route.Actions.Select(a => a.Type), Is.EqualTo(new[] { MoveRouteEnum.MoveRight, MoveRouteEnum.MoveRight, MoveRouteEnum.MoveDown }));
        Assert.That(route.RepeatRoute, Is.False); Assert.That(route.IgnoreIfBlocked, Is.False);
        Assert.That(root.OfType<WaitForRouteCommand>().Single().TimeoutMs, Is.GreaterThan(0));
        Assert.That(root.First(), Is.TypeOf<BeginCinematicCommand>());
        Assert.That(root[1], Is.TypeOf<HoldPlayerCommand>());
        Assert.That(root.Last(), Is.TypeOf<CinematicCameraCommand>());
        Assert.That(((CinematicCameraCommand)root.Last()).Reset, Is.True);
    }

    [Test]
    public void RouteCompletionPrecedesDialogueAtSameTimeAndTimelineResumesAfterResponse()
    {
        var scene = Scene(); scene.Clips.Add(Path());
        scene.Clips.Add(new() { Track = CinematicTrack.Dialogue, StartMs = 1000, DurationMs = 1000, Text = "Hello" });
        scene.Clips.Add(new() { Track = CinematicTrack.Sound, StartMs = 2000, DurationMs = 1000, AudioFile = "existing.ogg", VoiceSound = true });
        var root = Commands(CinematicCompiler.Compile(scene, 32, 32));
        var waitRoute = root.FindIndex(c => c is WaitForRouteCommand);
        var dialogue = root.FindIndex(c => c is ShowTextCommand);
        Assert.That(waitRoute, Is.LessThan(dialogue));
        Assert.That(root[dialogue + 1], Is.TypeOf<WaitCommand>());
        Assert.That(((WaitCommand)root[dialogue + 1]).Time, Is.EqualTo(1000));
        Assert.That(root.OfType<PlaySoundCommand>().Single().VoiceSound, Is.True);
    }

    [Test]
    public void ChoicesUseExistingServerBranchesAndExistingCommonEvents()
    {
        var scene = Scene(); var eventId = Guid.NewGuid();
        scene.Clips.Add(new() { Track = CinematicTrack.Choice, Text = "Choose", Option1 = "A", Option2 = "B", ChoiceEvent2 = eventId });
        var page = CinematicCompiler.Compile(scene, 32, 32, validEvent: id => id == eventId);
        var choice = Commands(page).OfType<ShowOptionsCommand>().Single();
        Assert.That(choice.BranchIds.Distinct().Count(), Is.EqualTo(4));
        Assert.That(page.CommandLists[choice.BranchIds[0]], Is.Empty);
        Assert.That(page.CommandLists[choice.BranchIds[1]].OfType<StartCommmonEventCommand>().Single().EventId, Is.EqualTo(eventId));
    }

    [Test]
    public void GameplayInteractionReleasesAndRestoresControlAroundNativeCommonEvent()
    {
        var scene = Scene(); scene.Clips.Add(new() { Track = CinematicTrack.Event, ResourceId = Guid.NewGuid(), AllowPlayerControl = true });
        var root = Commands(CinematicCompiler.Compile(scene, 32, 32));
        var i = root.FindIndex(c => c is StartCommmonEventCommand);
        Assert.That(root[i - 1], Is.TypeOf<ReleasePlayerCommand>());
        Assert.That(root[i + 1], Is.TypeOf<HoldPlayerCommand>());
    }

    [Test]
    public void BlockedTilesAndInvalidAnchorsAreRejectedWithoutChangingTheScene()
    {
        var scene = Scene(); scene.Clips.Add(Path()); var before = JsonConvert.SerializeObject(scene);
        Assert.Throws<InvalidOperationException>(() => CinematicCompiler.Compile(scene, 32, 32, (x, y) => x == 3 && y == 3));
        Assert.That(JsonConvert.SerializeObject(scene), Is.EqualTo(before));
        scene.Clips[0].Points[0].X = 1;
        Assert.Throws<InvalidOperationException>(() => CinematicCompiler.Compile(scene, 32, 32));
    }

    [Test]
    public void RepeatedPathsMustStartAtThePreviousEndpointAndCannotOverlap()
    {
        var scene = Scene(); scene.Clips.Add(Path()); var second = Path(500); scene.Clips.Add(second);
        Assert.Throws<InvalidOperationException>(() => CinematicCompiler.Compile(scene, 32, 32));
        second.StartMs = 1000; second.Points = [new() { X = 4, Y = 4 }, new() { X = 5, Y = 4 }];
        Assert.DoesNotThrow(() => CinematicCompiler.Compile(scene, 32, 32));
    }

    [TestCase("../voice.ogg")]
    [TestCase("..\\voice.ogg")]
    [TestCase("C:\\voice.ogg")]
    public void AudioResourcesMustBeExistingCatalogFilenames(string filename)
    {
        var scene = Scene(); scene.Clips.Add(new() { Track = CinematicTrack.Sound, AudioFile = filename });
        Assert.Throws<InvalidOperationException>(() => CinematicCompiler.Compile(scene, 32, 32));
    }

    [Test]
    public void MissingResourcesGlobalActorsUnknownVersionsAndOverlappingCameraAreRejected()
    {
        var scene = Scene(); scene.Clips.Add(new() { Track = CinematicTrack.Animation, ResourceId = Guid.NewGuid() });
        Assert.Throws<InvalidOperationException>(() => CinematicCompiler.Compile(scene, 32, 32, validAnimation: _ => false));
        scene.Clips.Clear(); scene.Actors[0].TargetId = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => CinematicCompiler.Compile(scene, 32, 32, validActor: _ => false));
        scene.Actors.Clear(); scene.Version = 999;
        Assert.Throws<InvalidOperationException>(() => CinematicCompiler.Compile(scene, 32, 32));
        scene.Version = 1; scene.Clips.Add(new() { Track = CinematicTrack.Camera }); scene.Clips.Add(new() { Track = CinematicTrack.Camera });
        Assert.Throws<InvalidOperationException>(() => CinematicCompiler.Compile(scene, 32, 32));
    }

    [Test]
    public void SourceAndGeneratedCommandsRoundTripThroughExistingEventSerialization()
    {
        var scene = Scene(); scene.Clips.Add(Path());
        var evt = new EventDescriptor(Guid.NewGuid(), scene.MapId, 0, 0) { Pages = [CinematicCompiler.Compile(scene, 32, 32)] };
        var copy = new EventDescriptor(evt.Id, evt.JsonData);
        Assert.That(copy.Pages.Single().Cinematic!.MapId, Is.EqualTo(scene.MapId));
        Assert.That(Commands(copy.Pages[0]).OfType<SetMoveRouteCommand>().Single().Route.Actions.Count, Is.EqualTo(3));
        Assert.That(Commands(copy.Pages[0]).First(), Is.TypeOf<BeginCinematicCommand>());
    }

    [Test]
    public void HistoricalCommandNumbersAndDefaultRouteWaitBehaviorAreUnchanged()
    {
        Assert.That((int)MoveRouteEnum.MoveUp, Is.EqualTo(1));
        Assert.That((int)MoveRouteEnum.SetGraphic, Is.EqualTo(44));
        Assert.That((int)EventCommandType.StartMiniGame, Is.EqualTo(1000));
        Assert.That((int)EventCommandType.ForceRain, Is.EqualTo(1063));
        Assert.That(new WaitForRouteCommand().TimeoutMs, Is.Zero);
        Assert.That(new EventPage().Cinematic, Is.Null);
    }

    [Test]
    public void SavedCommandsAndSourceAreDetachedFromFurtherInspectorEdits()
    {
        var scene = Scene(); scene.Clips.Add(Path());
        var page = CinematicCompiler.Compile(scene, 32, 32);
        scene.Actors[0].StartX = 20; scene.Clips[0].Points[1].X = 25;
        Assert.That(page.Cinematic!.Actors[0].StartX, Is.EqualTo(2));
        Assert.That(page.Cinematic.Clips[0].Points[1].X, Is.EqualTo(4));
        Assert.That(Commands(page).OfType<BeginCinematicCommand>().Single().Actors[0].StartX, Is.EqualTo(2));
    }

    [Test]
    public void EventSpeedCommandsAreNeverEmittedForPlayerRoutes()
    {
        var scene = Scene(); var path = Path(); scene.Clips.Add(path);
        var route = Commands(CinematicCompiler.Compile(scene, 32, 32)).OfType<SetMoveRouteCommand>().Single().Route;
        Assert.That(route.Actions.All(a => a.Type is MoveRouteEnum.MoveRight or MoveRouteEnum.MoveDown), Is.True);
        path.Speed = 4;
        Assert.Throws<InvalidOperationException>(() => CinematicCompiler.Compile(scene, 32, 32));
        var id = Guid.NewGuid(); scene.Actors[0].TargetId = id; path.ActorId = id;
        var eventRoute = Commands(CinematicCompiler.Compile(scene, 32, 32)).OfType<SetMoveRouteCommand>().Single().Route;
        Assert.That(eventRoute.Actions[0].Type, Is.EqualTo(MoveRouteEnum.SetSpeedFastest));
    }

    [Test]
    public void EmptyMovementAndOutsideMapPathsAreRejected()
    {
        var scene = Scene(); var path = Path(); scene.Clips.Add(path);
        path.Points[1].X = 2; path.Points[1].Y = 3;
        Assert.Throws<InvalidOperationException>(() => CinematicCompiler.Compile(scene, 32, 32));
        path.Points[1].X = 32;
        Assert.Throws<InvalidOperationException>(() => CinematicCompiler.Compile(scene, 32, 32));
    }

    [Test]
    public void CameraPacketRoundTripsThroughTheEnginePacketRegistry()
    {
        var packet = new Intersect.Network.Packets.Server.CinematicCameraPacket
        { EventId = Guid.NewGuid(), MapId = Guid.NewGuid(), X = 4, Y = 5, Zoom = 2, DurationMs = 1500, Ease = CinematicEase.EaseOut };
        var bytes = Intersect.Network.MessagePacker.Instance.Serialize(packet);
        var result = Intersect.Network.MessagePacker.Instance.Deserialize(bytes) as Intersect.Network.Packets.Server.CinematicCameraPacket;
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.EventId, Is.EqualTo(packet.EventId));
        Assert.That(result.Zoom, Is.EqualTo(2)); Assert.That(result.Ease, Is.EqualTo(CinematicEase.EaseOut));
    }

    [TestCase(CinematicEase.Linear)]
    [TestCase(CinematicEase.EaseIn)]
    [TestCase(CinematicEase.EaseOut)]
    [TestCase(CinematicEase.EaseInOut)]
    public void CameraInterpolationIsBoundedAndMatchesEndpoints(CinematicEase ease)
    {
        var from = new CinematicCameraPose(2, 3, 1); var to = new CinematicCameraPose(8, 9, 2);
        Assert.That(CinematicInterpolation.Lerp(from, to, -1, ease), Is.EqualTo(from));
        Assert.That(CinematicInterpolation.Lerp(from, to, 2, ease), Is.EqualTo(to));
        Assert.That(CinematicInterpolation.Lerp(from, to, .5f, ease).Zoom, Is.InRange(1, 2));
    }

    [Test]
    public void ScrubbingUsesCardinalPathsAndCameraEasing()
    {
        var scene = Scene(); scene.Clips.Add(Path());
        Assert.That(CinematicPreview.ActorPosition(scene, scene.Actors[0], 0), Is.EqualTo((2f, 3f)));
        Assert.That(CinematicPreview.ActorPosition(scene, scene.Actors[0], 1000), Is.EqualTo((4f, 4f)));
        Assert.That(CinematicPreview.ActorPosition(scene, scene.Actors[0], 500), Is.EqualTo((3.5f, 3f)));
    }
}
