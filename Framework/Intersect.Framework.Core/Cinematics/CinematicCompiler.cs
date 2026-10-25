using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Newtonsoft.Json;

namespace Intersect.Framework.Core.Cinematics;

/// <summary>Compiles dedicated scenes into the existing authoritative Event interpreter.</summary>
public static class CinematicCompiler
{
    public const int MaximumDurationMs = 600000;
    public const int MaximumSteps = 4096;

    public static List<MoveRouteAction> BuildPath(IEnumerable<CinematicPoint> points, Func<int, int, bool>? blocked = null)
    {
        var result = new List<MoveRouteAction>();
        void Append(MoveRouteEnum type)
        {
            if (result.Count >= MaximumSteps) throw new InvalidOperationException("path-too-long");
            result.Add(new() { Type = type });
        }
        CinematicPoint? previous = null;
        foreach (var point in points)
        {
            if (previous != null)
            {
                var x = previous.X; var y = previous.Y;
                // Cardinal steps preserve native collision rules. No diagonal corner cutting.
                while (x != point.X || y != point.Y)
                {
                    if (result.Count >= MaximumSteps) throw new InvalidOperationException("path-too-long");
                    MoveRouteEnum direction;
                    if (x != point.X) { direction = x < point.X ? MoveRouteEnum.MoveRight : MoveRouteEnum.MoveLeft; x += Math.Sign(point.X - x); }
                    else { direction = y < point.Y ? MoveRouteEnum.MoveDown : MoveRouteEnum.MoveUp; y += Math.Sign(point.Y - y); }
                    if (blocked?.Invoke(x, y) == true) throw new InvalidOperationException($"blocked:{x},{y}");
                    Append(direction);
                }
            }
            if (point.PauseMs is < 0 or > 60000 || point.PauseMs % 100 != 0 || point.SpeedOverride is < -1 or > 4 || !Enum.IsDefined(point.Facing))
                throw new InvalidOperationException("point-action-range");
            if (point.SpeedOverride >= 0) Append((MoveRouteEnum)((int)MoveRouteEnum.SetSpeedSlowest + point.SpeedOverride));
            if (point.Facing != CinematicFacing.Unchanged)
                Append(point.Facing switch { CinematicFacing.Up => MoveRouteEnum.FaceUp, CinematicFacing.Down => MoveRouteEnum.FaceDown,
                    CinematicFacing.Left => MoveRouteEnum.FaceLeft, CinematicFacing.Right => MoveRouteEnum.FaceRight, _ => MoveRouteEnum.FacePlayer });
            var pause = point.PauseMs;
            while (pause >= 1000) { Append(MoveRouteEnum.Wait1000); pause -= 1000; }
            if (pause >= 500) { Append(MoveRouteEnum.Wait500); pause -= 500; }
            while (pause > 0) { Append(MoveRouteEnum.Wait100); pause -= 100; }
            previous = point;
        }
        return result;
    }

    public static EventPage Compile(CinematicScene scene, int mapWidth, int mapHeight,
        Func<int, int, bool>? blocked = null, Func<Guid, bool>? validActor = null,
        Func<Guid, bool>? validAnimation = null, Func<Guid, bool>? validEvent = null,
        Func<string, bool, bool>? validAudio = null)
    {
        void Require(bool valid, string error) { if (!valid) throw new InvalidOperationException(error); }
        Require(scene != null && scene.Actors != null && scene.Clips != null && scene.Markers != null, "scene-data");
        Require(scene!.Actors!.All(a => a != null) && scene.Clips!.All(c => c != null && c.Points != null && c.Points.All(p => p != null)), "scene-data");
        // Detach generated commands and saved source from the mutable authoring inspector.
        scene = JsonConvert.DeserializeObject<CinematicScene>(JsonConvert.SerializeObject(scene),
            new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None, MaxDepth = 32 })!;
        Require(scene.Version == CinematicScene.CurrentVersion, "scene-version");
        Require(scene.MapId != Guid.Empty && mapWidth > 0 && mapHeight > 0, "scene-map");
        Require(scene.DurationMs is > 0 and <= MaximumDurationMs, "scene-duration");
        Require(scene.Clips.Count <= 512 && scene.Actors.Count <= 64, "scene-size");
        Require(scene.Markers.Count <= 128 && scene.Markers.All(m => m != null && m.TimeMs >= 0 && m.TimeMs <= scene.DurationMs), "marker-range");
        bool InMap(int x, int y) => x >= 0 && y >= 0 && x < mapWidth && y < mapHeight;
        Require(scene.Actors.Select(a => a.TargetId).Distinct().Count() == scene.Actors.Count, "actor-duplicate");
        foreach (var actor in scene.Actors)
        {
            Require(InMap(actor.StartX, actor.StartY), "actor-position");
            Require(actor.TargetId == Guid.Empty || validActor?.Invoke(actor.TargetId) != false, "actor-unavailable");
        }
        var page = new EventPage { Cinematic = scene };
        var commands = page.CommandLists.Values.First();
        commands.Add(new BeginCinematicCommand { MapId = scene.MapId,
            Actors = scene.Actors.Where(a => scene.Clips.Any(c => c.Track == CinematicTrack.Movement && c.ActorId == a.TargetId)).ToList() });
        commands.Add(new HoldPlayerCommand());
        var scheduled = new List<(int Time, int Priority, int Order, EventCommand Command)>();
        var order = 0;
        var actorEnds = scene.Actors.ToDictionary(a => a.TargetId, a => (Time: -1, X: a.StartX, Y: a.StartY));
        var cameraEnd = -1;
        var presentationEnd = -1;
        foreach (var clip in scene.Clips.OrderBy(c => c.StartMs))
        {
            Require(Enum.IsDefined(clip.Track), "clip-track");
            Require(clip.StartMs >= 0 && clip.DurationMs > 0 && clip.StartMs <= scene.DurationMs - clip.DurationMs, "clip-time");
            var end = clip.StartMs + clip.DurationMs;
            void Add(EventCommand command, int? time = null, int priority = 1) => scheduled.Add((time ?? clip.StartMs, priority, order++, command));
            switch (clip.Track)
            {
                case CinematicTrack.Camera:
                    Require(clip.StartMs >= cameraEnd, "camera-overlap"); cameraEnd = end;
                    Require(InMap(clip.X, clip.Y) && float.IsFinite(clip.Zoom) && clip.Zoom is >= .25f and <= 4f && Enum.IsDefined(clip.Ease), "camera-position");
                    Require(float.IsFinite(clip.OffsetX) && float.IsFinite(clip.OffsetY) && Math.Abs(clip.OffsetX) <= mapWidth && Math.Abs(clip.OffsetY) <= mapHeight, "camera-position");
                    Require(!clip.FollowActor || scene.Actors.Any(a => a.TargetId == clip.ActorId), "actor-unavailable");
                    Add(new CinematicCameraCommand { MapId = scene.MapId, X = clip.X, Y = clip.Y, Zoom = clip.Zoom,
                        DurationMs = clip.DurationMs, Ease = clip.Ease, FollowActor = clip.FollowActor,
                        ActorId = clip.ActorId, OffsetX = clip.OffsetX, OffsetY = clip.OffsetY });
                    break;
                case CinematicTrack.Presentation:
                    Require(clip.StartMs >= presentationEnd, "presentation-overlap"); presentationEnd = end;
                    Require(Enum.IsDefined(clip.Ease) && float.IsFinite(clip.Letterbox) && clip.Letterbox is >= 0 and <= .25f &&
                        float.IsFinite(clip.FadeOpacity) && clip.FadeOpacity is >= 0 and <= 1 &&
                        float.IsFinite(clip.ShakePixels) && clip.ShakePixels is >= 0 and <= 32, "presentation-range");
                    Add(new CinematicCameraCommand { MapId = scene.MapId, PresentationOnly = true,
                        DurationMs = clip.DurationMs, Ease = clip.Ease, HideHud = clip.HideHud,
                        Letterbox = clip.Letterbox, FadeOpacity = clip.FadeOpacity, ShakePixels = clip.ShakePixels });
                    break;
                case CinematicTrack.Movement:
                    Require(actorEnds.TryGetValue(clip.ActorId, out var anchor), "actor-unavailable");
                    Require(clip.StartMs >= anchor.Time, "actor-overlap");
                    Require(clip.Speed is >= 0 and <= 4 && clip.Points.Count is >= 2 and <= 1024, "path-points");
                    Require(clip.Points[0].X == anchor.X && clip.Points[0].Y == anchor.Y, "path-anchor");
                    Require(clip.Points.All(p => InMap(p.X, p.Y)), "path-outside-map");
                    Require(clip.ActorId != Guid.Empty || clip.Points.All(p => p.SpeedOverride == -1 && p.Facing != CinematicFacing.Player), "point-action-unavailable");
                    var actions = BuildPath(clip.Points, blocked);
                    Require(actions.Count > 0, "path-empty");
                    if (clip.ActorId != Guid.Empty)
                    {
                        actions.Insert(0, new() { Type = MoveRouteEnum.DirectionFixOff });
                        actions.Insert(0, new() { Type = (MoveRouteEnum)((int)MoveRouteEnum.SetSpeedSlowest + clip.Speed) });
                    }
                    else Require(clip.Speed == 2, "player-native-speed");
                    Add(new SetMoveRouteCommand { Route = new EventMoveRoute { Target = clip.ActorId, Actions = actions,
                        IgnoreIfBlocked = false, RepeatRoute = false } });
                    Add(new WaitForRouteCommand { TargetId = clip.ActorId,
                        TimeoutMs = (int)Math.Min(MaximumDurationMs, Math.Max(30000L, actions.Count * 5000L)) }, end, 0);
                    actorEnds[clip.ActorId] = (end, clip.Points[^1].X, clip.Points[^1].Y);
                    break;
                case CinematicTrack.Animation:
                    Require(clip.ResourceId != Guid.Empty && validAnimation?.Invoke(clip.ResourceId) != false, "animation-unavailable");
                    Require(clip.AttachToActor ? scene.Actors.Any(a => a.TargetId == clip.ActorId) &&
                        Math.Abs((long)clip.AnimationOffsetX) <= 1024 && Math.Abs((long)clip.AnimationOffsetY) <= 1024 :
                        InMap(clip.X, clip.Y) && clip.X <= sbyte.MaxValue && clip.Y <= sbyte.MaxValue, "animation-position");
                    Add(new PlayAnimationCommand { AnimationId = clip.ResourceId, MapId = clip.AttachToActor ? Guid.Empty : scene.MapId,
                        TargetMapId = clip.AttachToActor ? scene.MapId : Guid.Empty, EntityId = clip.AttachToActor ? clip.ActorId : Guid.Empty,
                        X = clip.AttachToActor ? (sbyte)0 : (sbyte)clip.X, Y = clip.AttachToActor ? (sbyte)0 : (sbyte)clip.Y,
                        OffsetX = clip.AttachToActor ? clip.AnimationOffsetX : 0, OffsetY = clip.AttachToActor ? clip.AnimationOffsetY : 0,
                        InstanceToPlayer = true });
                    break;
                case CinematicTrack.Dialogue:
                    Require(!string.IsNullOrWhiteSpace(clip.Text), "dialogue-empty");
                    Add(new ShowTextCommand { Text = clip.Text, Face = clip.Face });
                    break;
                case CinematicTrack.Choice:
                    var options = new[] { clip.Option1, clip.Option2, clip.Option3, clip.Option4 };
                    var branches = new[] { clip.ChoiceEvent1, clip.ChoiceEvent2, clip.ChoiceEvent3, clip.ChoiceEvent4 };
                    Require(!string.IsNullOrWhiteSpace(clip.Text) && !string.IsNullOrWhiteSpace(options[0]) && !string.IsNullOrWhiteSpace(options[1]), "choice-empty");
                    var choice = new ShowOptionsCommand(page.CommandLists) { Text = clip.Text, Face = clip.Face, Options = options };
                    for (var i = 0; i < 4; i++)
                    {
                        if (branches[i] == Guid.Empty) continue;
                        Require(!string.IsNullOrWhiteSpace(options[i]) && validEvent?.Invoke(branches[i]) != false, "event-unavailable");
                        page.CommandLists[choice.BranchIds[i]].Add(new StartCommmonEventCommand { EventId = branches[i] });
                    }
                    Add(choice);
                    break;
                case CinematicTrack.Sound:
                case CinematicTrack.Music:
                    Require(!string.IsNullOrWhiteSpace(clip.AudioFile) && Path.GetFileName(clip.AudioFile) == clip.AudioFile && !clip.AudioFile.Contains('\\') && !clip.AudioFile.Contains('/'), "audio-file");
                    Require(validAudio?.Invoke(clip.AudioFile, clip.Track == CinematicTrack.Music) != false, "audio-unavailable");
                    if (clip.Track == CinematicTrack.Music) Add(new PlayBgmCommand { File = clip.AudioFile });
                    else Add(new PlaySoundCommand { File = clip.AudioFile, VoiceSound = clip.VoiceSound });
                    break;
                case CinematicTrack.Event:
                    Require(clip.ResourceId != Guid.Empty && validEvent?.Invoke(clip.ResourceId) != false, "event-unavailable");
                    if (clip.AllowPlayerControl) Add(new ReleasePlayerCommand());
                    Add(new StartCommmonEventCommand { EventId = clip.ResourceId });
                    if (clip.AllowPlayerControl) Add(new HoldPlayerCommand());
                    break;
            }
        }
        var cursor = 0;
        foreach (var item in scheduled.OrderBy(s => s.Time).ThenBy(s => s.Priority).ThenBy(s => s.Order))
        {
            if (item.Time > cursor) commands.Add(new WaitCommand { Time = item.Time - cursor });
            commands.Add(item.Command); cursor = item.Time;
        }
        if (scene.DurationMs > cursor) commands.Add(new WaitCommand { Time = scene.DurationMs - cursor });
        commands.Add(new CinematicCameraCommand { Reset = true });
        return page;
    }
}
