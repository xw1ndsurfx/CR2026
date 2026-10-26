using Intersect.Client.General;
using Intersect.Client.Maps;
using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Cinematics;
using Intersect.Network.Packets.Server;
using Graphics = Intersect.Client.Core.Graphics;
namespace Intersect.Client.Cinematics;

internal static partial class CinematicCamera
{
    private static readonly object Sync = new();
    private sealed record Shot(Guid Owner, Guid Player, Guid Map, CinematicCameraPose From,
        CinematicCameraPose To, long Started, int Duration, CinematicEase Ease, Guid FollowEntity, bool FollowVessel, float OffsetX, float OffsetY,
        CinematicProjection Projection, CinematicLensPose FromLens, CinematicLensPose ToLens);
    private static Shot? _shot;
    private static CinematicCameraPose? _pose;
    private static CinematicLensPose _lens;
    internal static float? Zoom { get { lock (Sync) return _pose?.Zoom; } }

    internal static void Receive(CinematicCameraPacket packet)
    {
        lock (Sync)
        {
            if (packet.Reset) { Release(packet.EventId); return; }
            if (packet.EnvironmentOnly) { CinematicEnvironment.Receive(packet); return; }
            if (packet.PresentationOnly) { ReceivePresentation(packet); return; }
            if (Globals.Me is not { } me || packet.MapId != me.MapId ||
                !MapInstance.TryGet(packet.MapId, out var map) || packet.EventId == Guid.Empty ||
                !float.IsFinite(packet.X) || !float.IsFinite(packet.Y) || !float.IsFinite(packet.Zoom) ||
                packet.FollowVessel && (!packet.FollowActor || packet.FollowEntityId == Guid.Empty) ||
                packet.Zoom is < .25f or > 4f || packet.DurationMs is < 0 or > CinematicCompiler.MaximumDurationMs ||
                !Enum.IsDefined(packet.Ease) || !float.IsFinite(packet.OffsetX) || !float.IsFinite(packet.OffsetY) ||
                Math.Abs(packet.OffsetX) > Options.Instance.Map.MapWidth || Math.Abs(packet.OffsetY) > Options.Instance.Map.MapHeight ||
                !CinematicInterpolation.ValidLens(packet.Projection, packet.Heading, packet.EyeHeight, packet.PitchDegrees, packet.FieldOfView)) return;
            var tw = Options.Instance.Map.TileWidth; var th = Options.Instance.Map.TileHeight;
            if (packet.X < 0 || packet.Y < 0 || packet.X >= Options.Instance.Map.MapWidth || packet.Y >= Options.Instance.Map.MapHeight) return;
            // A non-default player zoom changes the visible world dimensions,
            // not the display dimensions stored in CurrentView.
            var view = Graphics.WorldViewport;
            var from = _pose ?? new CinematicCameraPose(view.X + view.Width / 2, view.Y + view.Height / 2,
                Graphics.WorldScale);
            var to = new CinematicCameraPose(map.X + (packet.X + .5f + packet.OffsetX) * tw, map.Y + (packet.Y + .5f + packet.OffsetY) * th, packet.Zoom);
            var toLens = new CinematicLensPose(packet.Heading, packet.EyeHeight, packet.PitchDegrees, packet.FieldOfView);
            var fromLens = _shot?.Projection == packet.Projection ? _lens : toLens;
            _shot = new(packet.EventId, me.Id, packet.MapId, from, to, Environment.TickCount64, packet.DurationMs, packet.Ease,
                packet.FollowActor ? packet.FollowEntityId : Guid.Empty, packet.FollowVessel, packet.OffsetX, packet.OffsetY, packet.Projection, fromLens, toLens);
            _pose = packet.DurationMs == 0 ? to : from;
            _lens = packet.DurationMs == 0 ? toLens : fromLens;
            _perspectiveFailed = false; _nextPerspectiveFrame = 0;
        }
    }

    internal static void Update()
    {
        lock (Sync)
        {
            UpdatePresentation();
            if (_shot is not { } shot) return;
            var elapsed = Environment.TickCount64 - shot.Started;
            if (Globals.GameState != GameStates.InGame || Globals.Me?.Id != shot.Player || Globals.Me.MapId != shot.Map ||
                !MapInstance.TryGet(shot.Map, out _) || elapsed > CinematicCompiler.MaximumDurationMs)
            { _shot = null; _pose = null; return; }
            var to = shot.To;
            if (shot.FollowEntity != Guid.Empty)
            {
                if (shot.FollowVessel)
                {
                    if (Intersect.Client.Naval.NavalWorld.TryCinematicVesselCenter(shot.Owner,shot.FollowEntity,out var shipX,out var shipY))
                        to=new(shipX+shot.OffsetX*Options.Instance.Map.TileWidth,shipY+shot.OffsetY*Options.Instance.Map.TileHeight,shot.To.Zoom);
                }
                else
                {
                Intersect.Client.Entities.Entity? target = shot.FollowEntity == Globals.Me.Id ? Globals.Me : null;
                if (target == null && MapInstance.TryGet(shot.Map, out var targetMap)) targetMap.LocalEntities.TryGetValue(shot.FollowEntity, out target);
                if (target != null && target.MapId == shot.Map)
                    to = new(target.Center.X + shot.OffsetX * Options.Instance.Map.TileWidth,
                        target.Center.Y + shot.OffsetY * Options.Instance.Map.TileHeight, shot.To.Zoom);
                }
            }
            _pose = CinematicInterpolation.Lerp(shot.From, to,
                shot.Duration == 0 ? 1 : elapsed / (float)shot.Duration, shot.Ease);
            _lens = CinematicInterpolation.LerpLens(shot.FromLens, shot.ToLens,
                shot.Duration == 0 ? 1 : elapsed / (float)shot.Duration, shot.Ease);
        }
    }
    internal static bool TryPosition(out float x, out float y)
    { lock (Sync) { var shake = PresentationShake(); x = (_pose?.X ?? 0) + shake.X; y = (_pose?.Y ?? 0) + shake.Y;
        if (_pose != null) return true;
        if (shake.X == 0 && shake.Y == 0) return false;
        var center = Globals.Me?.Center ?? default; x += center.X; y += center.Y; return true; } }
    internal static void Release(Guid owner)
    { lock (Sync) { if (_shot?.Owner == owner) { _shot = null; _pose = null; } ReleasePresentation(owner); CinematicEnvironment.Release(owner); Intersect.Client.Naval.NavalWorld.ClearCinematicFleet(owner); } }
    internal static void Clear()
    { lock (Sync) { _shot = null; _pose = null; _presentation = null; CinematicEnvironment.Clear(); Intersect.Client.Naval.NavalWorld.ClearCinematicFleet(); } }
}
