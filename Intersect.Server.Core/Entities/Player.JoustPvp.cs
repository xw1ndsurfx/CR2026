using System.Text.Json;
using Intersect.Enums;
using Intersect.Framework.Core.Jousting;
using Intersect.Network.Packets.Server;
using Intersect.Server.Jousting;
using Intersect.Server.Mounts;
using Intersect.Server.Maps;
using Intersect.Server.Networking;
namespace Intersect.Server.Entities;
public partial class Player
{
    private JoustPvpSession? _joustPvp;
    private int _pvpJoustMoveMs;
    private int _pvpJoustRouteThread;
    private Guid _pvpJoustMountBefore;
    private long _pvpJoustSent;
    private long _pvpJoustRequestAt;
    private long _pvpJoustBrowseAt;
    private long _pvpJoustAnswerAt;
    private int _pvpJoustCharge, _pvpJoustImpact;
    private JoustView? _lastPvpJoustView;
    internal bool JoustIntroductionHolding => HasBlockingEventHold;
    private bool PvpJoustBlocksMovement => _joustPvp != null && _pvpJoustRouteThread != Environment.CurrentManagedThreadId &&
        !(_joustPvp.Match.Phase == JoustPhase.Preparation && _cinematicRouteScope?.ThreadId == Environment.CurrentManagedThreadId);
    internal bool CanEnterPvpJoust(JoustArena arena) => !IsDisposed && InGame && Client != null && !Client.IsEditor &&
        MapId == arena.MapId && !IsJousting && _joustPendingReward == null && !HideEntity && !CachedStatuses.Any(s => s.Type == SpellEffect.Stealth) &&
        !HasBlockingEventHold && MoveRoute == null && OpenCraftingTableId == Guid.Empty && CanUseLobbyTravel &&
        !EventLookup.Values.Any(e => e.CinematicActive) && MountRuntime.Find(arena.MountId) is { IntegratedRider: true } mount && CanRide(mount);
    internal void BeginPvpJoust(JoustPvpSession session)
    {
        _joustPvp = session; _pvpJoustMountBefore = ActiveMountId; _pvpJoustMoveMs = _pvpJoustCharge = _pvpJoustImpact = 0;
        _lastPvpJoustView = null; _pvpJoustSent = 0;
        Dir = (session.Side(this) == 0) == (session.Arena.StartX < session.Arena.EndX) ? Direction.Right : Direction.Left;
        ActiveMountId = session.Arena.MountId;
        PacketSender.SendEntityDataToProximity(this); SendMountState();
        SendPacket(new HoldPlayerPacket(session.Match.Id, Guid.Empty, false));
        SendPacket(new JoustStatePacket { Open = true, ConfigurationJson = JoustRuntime.Current.ToJson(), DuelJson = JsonSerializer.Serialize(new JoustDuelView { Started = true }) });
    }
    internal void IntroducePvpJoust()
    { if (_joustPvp is { } session) { RunJoustEvent(session.Arena.IntroductionEventId); session.Publish(this, session.Match.PhaseStarted); } }
    internal bool MovePvpJoust(long now)
    {
        if (_joustPvp is not { } session) return false;
        var arena = session.Arena; var side = session.Side(this); var pose = JoustTrack.Sample(arena, session.Match, now);
        var target = side == 0 ? pose.PlayerX : pose.OpponentX;
        if (Y != (side == 0 ? arena.StartY : arena.OpponentY)) return false;
        if (session.Match.Phase == JoustPhase.Preparation)
        {
            var facing = (side == 0) == (pose.PlayerX < pose.OpponentX) ? Direction.Right : Direction.Left;
            if (_pvpJoustMoveMs != 0 || Dir != facing) { _pvpJoustMoveMs = 0; Dir = facing; PacketSender.SendEntityDataToProximity(this); }
            return X == target;
        }
        _pvpJoustMoveMs = pose.StepMilliseconds; _pvpJoustRouteThread = Environment.CurrentManagedThreadId;
        try
        {
            if (Math.Abs(X - target) > 64) return false;
            while (X != target)
            {
                var direction = X < target ? Direction.Right : Direction.Left;
                if (!JoustRuntime.TileClear(session.Instance, X + (direction == Direction.Right ? 1 : -1), Y, this) || !CanMoveInDirection(direction, out _, out _)) return false;
                var previous = X; MoveTimer = 0; Move(direction, this, correction: true);
                if (MapId != arena.MapId || X == previous) return false;
            }
            return true;
        }
        finally { _pvpJoustRouteThread = 0; }
    }
    internal void PresentPvpJoust()
    {
        if (_joustPvp is not { } session) return;
        var match = session.Match;
        if (match.Phase is JoustPhase.Charge or JoustPhase.Aim && match.Pass != _pvpJoustCharge)
        { _pvpJoustCharge = match.Pass; RunJoustEvent(session.Arena.ChargeEventId); }
        if (match.ResolvedPass > _pvpJoustImpact)
        {
            _pvpJoustImpact = match.ResolvedPass;
            RunJoustEvent(match.Strikes.Any(s => s.Hit == JoustHit.Unhorsed) && session.Arena.UnhorseEventId != Guid.Empty ? session.Arena.UnhorseEventId : session.Arena.ImpactEventId);
        }
    }
    internal void PublishPvpJoust(JoustView view, bool force = false)
    {
        var now = Environment.TickCount64;
        if (!force && now - _pvpJoustSent < 100 && _lastPvpJoustView?.Phase == view.Phase && _lastPvpJoustView.Pass == view.Pass) return;
        _pvpJoustSent = now; _lastPvpJoustView = view;
        SendPacket(new JoustStatePacket { ViewJson = JsonSerializer.Serialize(view) });
    }
    internal void FinishPvpJoust(JoustPvpSession session, JoustView view, int side)
    {
        if (!ReferenceEquals(_joustPvp, session)) return;
        _joustPvp = null; _pvpJoustMoveMs = 0;
        if (session.Match.Phase == JoustPhase.Finished) { _joustPendingReward = session.Match; _joustRewardSide = side; }
        try { if (!IsDisposed) PublishPvpJoust(view, true); }
        finally
        {
            StopJoustScenes();
            var previous = MountRuntime.Find(_pvpJoustMountBefore);
            ActiveMountId = previous != null && MountRuntime.Owns(this, previous) && CanRide(previous) ? previous.Id : Guid.Empty;
            _pvpJoustMountBefore = Guid.Empty;
            if (!IsDisposed)
            {
                SendPacket(new HoldPlayerPacket(session.Match.Id, Guid.Empty, true));
                if (MapController.Get(MapId) != null) PacketSender.SendEntityDataToProximity(this);
                SendMountState();
                if (view.Phase == JoustPhase.Finished && view.Winner == 0) RunJoustEvent(session.Arena.VictoryEventId);
            }
        }
    }
}
