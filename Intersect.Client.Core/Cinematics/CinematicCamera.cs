using Intersect.Client.General;
using Intersect.Client.Maps;
using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Cinematics;
using Intersect.Network.Packets.Server;
using Graphics = Intersect.Client.Core.Graphics;
namespace Intersect.Client.Cinematics;

internal static class CinematicCamera
{
    private static readonly object Sync = new();
    private sealed record Shot(Guid Owner, Guid Player, Guid Map, CinematicCameraPose From,
        CinematicCameraPose To, long Started, int Duration, CinematicEase Ease);
    private static Shot? _shot;
    private static CinematicCameraPose? _pose;
    internal static float? Zoom { get { lock (Sync) return _pose?.Zoom; } }

    internal static void Receive(CinematicCameraPacket packet)
    {
        lock (Sync)
        {
            if (packet.Reset) { Release(packet.EventId); return; }
            if (Globals.Me is not { } me || packet.MapId != me.MapId ||
                !MapInstance.TryGet(packet.MapId, out var map) || packet.EventId == Guid.Empty ||
                !float.IsFinite(packet.X) || !float.IsFinite(packet.Y) || !float.IsFinite(packet.Zoom) ||
                packet.Zoom is < .25f or > 4f || packet.DurationMs is < 0 or > CinematicCompiler.MaximumDurationMs ||
                !Enum.IsDefined(packet.Ease)) return;
            var tw = Options.Instance.Map.TileWidth; var th = Options.Instance.Map.TileHeight;
            if (packet.X < 0 || packet.Y < 0 || packet.X >= Options.Instance.Map.MapWidth || packet.Y >= Options.Instance.Map.MapHeight) return;
            var view = Graphics.CurrentView;
            var from = _pose ?? new CinematicCameraPose(view.X + view.Width / 2, view.Y + view.Height / 2,
                Graphics.Renderer?.Scale ?? Globals.Database.WorldZoom);
            var to = new CinematicCameraPose(map.X + (packet.X + .5f) * tw, map.Y + (packet.Y + .5f) * th, packet.Zoom);
            _shot = new(packet.EventId, me.Id, packet.MapId, from, to, Environment.TickCount64, packet.DurationMs, packet.Ease);
            _pose = packet.DurationMs == 0 ? to : from;
        }
    }

    internal static void Update()
    {
        lock (Sync)
        {
            if (_shot is not { } shot) return;
            var elapsed = Environment.TickCount64 - shot.Started;
            if (Globals.GameState != GameStates.InGame || Globals.Me?.Id != shot.Player || Globals.Me.MapId != shot.Map ||
                !MapInstance.TryGet(shot.Map, out _) || elapsed > CinematicCompiler.MaximumDurationMs)
            { _shot = null; _pose = null; return; }
            _pose = CinematicInterpolation.Lerp(shot.From, shot.To,
                shot.Duration == 0 ? 1 : elapsed / (float)shot.Duration, shot.Ease);
        }
    }
    internal static bool TryPosition(out float x, out float y)
    { lock (Sync) { x = _pose?.X ?? 0; y = _pose?.Y ?? 0; return _pose != null; } }
    internal static void Release(Guid owner)
    { lock (Sync) if (_shot?.Owner == owner) { _shot = null; _pose = null; } }
}
