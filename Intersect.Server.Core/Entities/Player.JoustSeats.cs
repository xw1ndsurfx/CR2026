using System.Text.Json;
using Intersect.Enums;
using Intersect.Framework.Core.Jousting;
using Intersect.Framework.Core.Cinematics;
using Intersect.Network.Packets.Server;
using Intersect.Server.Jousting;
using Intersect.Server.Maps;
using Intersect.Server.Networking;
namespace Intersect.Server.Entities;
public partial class Player
{
    private JoustAudienceRuntime.Lease? _joustSeat;
    private long _joustSeatRequestAt;
    private long _joustAudienceAt;
    internal bool IsJoustSeated => _joustSeat != null;
    internal bool IsJoustProtected => IsJousting || IsJoustSeated;
    protected override bool CanLookInDirection(Direction direction) => !IsJoustSeated && base.CanLookInDirection(direction);
    private void BrowseJoustSeats(bool open = true)
    {
        var arena = JoustRuntime.Current.Arenas.FirstOrDefault(a => a.Enabled && a.MapId == MapId);
        var context = arena == null ? null : JoustAudienceRuntime.Get(arena, MapInstanceId);
        if (context != null && open) JoustAudienceRuntime.EnsureCrowd(context);
        SendPacket(new JoustStatePacket { OpenSpectatorWindow = open, SpectatorJson = JsonSerializer.Serialize(context == null ? new JoustSpectatorView() : JoustAudienceRuntime.View(context, this)) });
    }
    private string ReserveJoustSeat(Guid seatId)
    {
        var arena = JoustRuntime.Current.Arenas.FirstOrDefault(a => a.Enabled && a.MapId == MapId);
        var seat = arena?.Seats.FirstOrDefault(s => s.Id == seatId);
        if (arena == null || seat == null) return "This grandstand seat is not configured here.";
        if (IsJoustProtected || !CanUseLobbyTravel || !InGame || Client == null || HideEntity || HasBlockingEventHold || MoveRoute != null ||
            EventLookup.Values.Any(e => e.CinematicActive) || CaptainedShipId != Guid.Empty || BoardedShipId != Guid.Empty ||
            CachedStatuses.Any(s => s.Type is SpellEffect.Transform or SpellEffect.Stealth)) return "You cannot take a grandstand seat right now.";
        if (X != seat.ExitX || Y != seat.ExitY) return "Stand on the selected seat's entrance tile before sitting down.";
        var lease = JoustAudienceRuntime.Reserve(this, arena, seat);
        if (lease == null) return "This seat is occupied or blocked.";
        try
        {
            Dismount(); _joustSeat = lease;
            // A trusted one-tile transfer onto a configured bench, not a client coordinate or map warp.
            SetJoustSeatPosition(seat.X, seat.Y, (Direction)seat.Facing);
            SendPacket(new HoldPlayerPacket(lease.Id, Guid.Empty, false));
            SendJoustSeatCamera(lease, true); BrowseJoustSeats();
            return "";
        }
        catch { LeaveJoustSeat(true); JoustAudienceRuntime.Release(lease); throw; }
    }
    private void SetJoustSeatPosition(int x, int y, Direction facing)
    {
        X = x; Y = y; Dir = facing; MoveTimer = 0;
        PacketSender.SendEntityPositionToAll(this); PacketSender.SendEntityDataToProximity(this);
    }
    internal void UpdateJoustSeat()
    {
        if (_joustSeat is not { } lease)
        {
            var time = Environment.TickCount64;
            if (InGame && Client != null && time >= _joustAudienceAt)
            {
                _joustAudienceAt = time + 2000;
                var configured = JoustRuntime.Current.Arenas.FirstOrDefault(a => a.Enabled && a.MapId == MapId && a.Seats.Length > 0);
                if (configured != null) JoustAudienceRuntime.Get(configured, MapInstanceId);
            }
            return;
        }
        var arena = lease.Context.Arena;
        var current = arena?.Seats.FirstOrDefault(s => s.Id == lease.Seat.Id);
        if (!InGame || Client == null || IsDisposed || IsDead || HasPendingDeath || HideEntity || lease.Context.Instance.IsDisposed ||
            arena == null || MapController.Get(arena.MapId) == null || MapId != arena.MapId || MapInstanceId != lease.Context.Key.Instance ||
            X != lease.Seat.X || Y != lease.Seat.Y || current == null || current.X != lease.Seat.X || current.Y != lease.Seat.Y ||
            current.ExitX != lease.Seat.ExitX || current.ExitY != lease.Seat.ExitY || current.Facing != lease.Seat.Facing ||
            CombatTimer > Intersect.Framework.Core.Timing.Global.Milliseconds || EventLookup.Values.Any(e => e.CinematicActive))
        { LeaveJoustSeat(true); return; }
        SendJoustSeatCamera(lease, false);
        var now = Environment.TickCount64;
        var interval = JoustAudienceRuntime.CurrentMatch(lease.Context.Key) is { Phase: JoustPhase.Charge or JoustPhase.Aim or JoustPhase.Impact or JoustPhase.Results } ? 100 : 250;
        if (now - lease.ViewSent >= interval) { lease.ViewSent = now; BrowseJoustSeats(false); }
    }
    private void SendJoustSeatCamera(JoustAudienceRuntime.Lease lease, bool force)
    {
        var arena = lease.Context.Arena; if (arena == null) return;
        var match = JoustAudienceRuntime.CurrentMatch(lease.Context.Key);
        var mode = match is { Live: true } ? lease.Camera : JoustSpectatorCamera.Arena;
        var id = mode == JoustSpectatorCamera.FirstKnight ? match?.FirstId ?? Guid.Empty : mode == JoustSpectatorCamera.SecondKnight ? match?.SecondId ?? Guid.Empty : Guid.Empty;
        var now = Environment.TickCount64;
        if (!force && lease.AppliedCamera == mode && lease.CameraMatch == (match?.MatchId ?? Guid.Empty) && now - lease.CameraSent < 60000) return;
        lease.AppliedCamera = mode; lease.CameraMatch = match?.MatchId ?? Guid.Empty; lease.CameraSent = now;
        SendPacket(new CinematicCameraPacket { EventId = lease.Id, MapId = arena.MapId, X = (arena.StartX + arena.EndX) / 2f,
            Y = (arena.StartY + arena.OpponentY) / 2f, Zoom = arena.SpectatorZoom, DurationMs = 300, Ease = CinematicEase.EaseInOut,
            FollowActor = id != Guid.Empty, FollowEntityId = id });
    }
    private void ChangeJoustSeatCamera(int camera)
    {
        if (_joustSeat is not { } lease || !Enum.IsDefined((JoustSpectatorCamera)camera)) return;
        lease.Camera = (JoustSpectatorCamera)camera; SendJoustSeatCamera(lease, true); BrowseJoustSeats(false);
    }
    internal string LeaveJoustSeat(bool force)
    {
        if (_joustSeat is not { } lease) return "";
        var arena = lease.Context.Arena;
        var samePlace = arena != null && MapId == arena.MapId && MapInstanceId == lease.Context.Key.Instance && X == lease.Seat.X && Y == lease.Seat.Y;
        (int X, int Y)? exit = null;
        if (samePlace && !IsDead && !HasPendingDeath && !IsDisposed && !EventLookup.Values.Any(e => e.CinematicActive))
        {
            foreach (var (dx, dy) in new[] { (0, 0), (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, 1), (1, -1), (-1, -1) })
            {
                var x = lease.Seat.ExitX + dx; var y = lease.Seat.ExitY + dy;
                if (!arena!.OnTrack(x, y) && !arena.Seats.Any(s => s.X == x && s.Y == y) && JoustRuntime.TileClear(lease.Context.Instance, x, y, this)) { exit = (x, y); break; }
            }
            if (exit == null && !force) return "The grandstand exit is blocked. Please wait or clear the entrance.";
        }
        _joustSeat = null;
        JoustAudienceRuntime.Release(lease);
        if (!IsDisposed)
        {
            SendPacket(new CinematicCameraPacket { EventId = lease.Id, Reset = true });
            SendPacket(new HoldPlayerPacket(lease.Id, Guid.Empty, true));
            if (exit is { } p) SetJoustSeatPosition(p.X, p.Y, lease.BeforeDirection);
            else if (MapController.Get(MapId) != null) PacketSender.SendEntityDataToProximity(this);
            if (Client != null) BrowseJoustSeats(false);
        }
        return "";
    }
}
