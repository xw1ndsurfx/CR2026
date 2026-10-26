using System.Text.Json;
using Intersect.Framework.Core.Jousting;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Jousting;
using Intersect.Server.Mounts;
using Intersect.Server.Networking;
using Intersect.Server.Maps;
using Intersect.Framework.Core;
using Intersect.Enums;
namespace Intersect.Server.Entities;
public partial class Player
{
    private JoustMatch? _joust;
    private JoustArena? _joustArena;
    private Guid _joustMap;
    private Guid _mountBeforeJoust;
    private long _joustSent;
    private long _joustOpenAt;
    private long _joustStartAt;
    private int _joustRouteThread;
    private int _joustMoveMs;
    private JoustOpponent? _joustOpponent;
    private MapInstance? _joustInstance;
    private int _joustChargePresented;
    private int _joustImpactPresented;
    private long _joustBegan;
    private readonly HashSet<Guid> _joustNativeEvents = [];
    internal bool IsJousting => _joust is { Done: false };
    [System.ComponentModel.DataAnnotations.Schema.NotMapped, Newtonsoft.Json.JsonIgnore]
    public override int ScriptedMovementMilliseconds => _joust is { Done: false } ? _joustMoveMs : 0;
    private bool JoustBlocksMovement => _joust is { Done: false } && _joustRouteThread != Environment.CurrentManagedThreadId &&
        !(_joust.Phase == JoustPhase.Preparation && _cinematicRouteScope?.ThreadId == Environment.CurrentManagedThreadId);
    internal void HandleJoustRequest(JoustRequestPacket packet)
    {
        lock (EntityLock)
        {
            var now = Environment.TickCount64;
            if (packet.Action == JoustAction.Open)
            {
                if (now - _joustOpenAt < 500) return;
                _joustOpenAt = now;
                SendPacket(new JoustStatePacket { Open = true, ConfigurationJson = JoustRuntime.Current.ToJson() });
                SendJoustView(now); SendJoustCareer(); return;
            }
            if (packet.Action == JoustAction.Redeem) { RedeemJoustOffer(packet.OfferId, packet.RequestId); return; }
            if (packet.Action == JoustAction.StartPve)
            {
                if (now - _joustStartAt < 1500 || _joust is { Done: false } || _joustPendingReward != null || !Enum.IsDefined((JoustDifficulty)packet.Difficulty)) return;
                _joustStartAt = now;
                var arena = JoustRuntime.Current.Arenas.FirstOrDefault(a => a.Id == packet.ArenaId && a.Enabled && a.MapId == MapId);
                if (arena == null || X != arena.StartX || Y != arena.StartY || IsDead || HasPendingDeath ||
                    Client == null || CombatTimer > Intersect.Framework.Core.Timing.Global.Milliseconds || HasBlockingEventHold ||
                    MoveRoute != null || OpenCraftingTableId != Guid.Empty || !CanUseLobbyTravel || EventLookup.Values.Any(e => e.CinematicActive) ||
                    !JoustRuntime.LanesClear(arena, MapInstanceId, this) ||
                    !MapController.TryGetInstanceFromMap(MapId, MapInstanceId, out var instance) ||
                    MountRuntime.Find(arena.MountId) is not { IntegratedRider: true } mount || !CanRide(mount))
                { SendPacket(new JoustStatePacket { Message = "Stand at the arena start to train." }); return; }
                if (!JoustRuntime.Reserve(arena.Id, Id))
                { SendPacket(new JoustStatePacket { Message = "This jousting lane is occupied." }); return; }
                _mountBeforeJoust = ActiveMountId;
                _joustArena = JoustConfiguration.FromJson(new JoustConfiguration { Arenas = [arena] }.ToJson()).Arenas[0];
                _joustMap = MapId; _joust = new(arena.Rules, now, (JoustDifficulty)packet.Difficulty);
                _joustBegan = now; _joustChargePresented = _joustImpactPresented = 0; _joustMoveMs = 0;
                _joustInstance = instance;
                _joustOpponent = new(mount, MapId, MapInstanceId, arena.EndX, arena.OpponentY);
                _joustOpponent.Dir = arena.StartX < arena.EndX ? Direction.Left : Direction.Right;
                instance.AddEntity(_joustOpponent);
                PacketSender.SendEntityDataToProximity(_joustOpponent);
                ActiveMountId = mount.Id; PacketSender.SendEntityDataToProximity(this); SendMountState();
                SendPacket(new HoldPlayerPacket(_joust.Id, Guid.Empty, false));
                RunJoustEvent(arena.IntroductionEventId); SendJoustView(now); return;
            }
            if (_joust == null || packet.MatchId != _joust.Id) return;
            if (packet.Action == JoustAction.Leave) { EndJoust(true); return; }
            if (packet.Action == JoustAction.Aim)
            {
                UpdateJoust(now);
                _joust.Submit(0, packet.Pass, packet.Sequence, new(packet.X, packet.Y, packet.Defending), now);
            }
        }
    }
    private void RunJoustEvent(Guid id)
    {
        if (id == Guid.Empty) return;
        var evt = EventDescriptor.Get(id);
        if (evt?.CommonEvent != true)
        {
            if (!Intersect.Server.Entities.Events.CommandProcessing.TryResolveCinematicScene(MapController.Get(MapId), id, out _)) return;
            // Adapt a native Director scene to the existing common Event interpreter.
            // This wrapper is session-local and never persisted or registered as authored content.
            var page = new EventPage();
            page.CommandLists.Values.First().Add(new PlayCinematicCommand { MapId = MapId, SceneEventId = id });
            evt = new EventDescriptor(Guid.NewGuid(), Guid.Empty, 0, 0) { CommonEvent = true, Global = false, Pages = [page] };
        }
        if (UnsafeStartCommonEvent(evt) && EventBaseIdLookup.TryGetValue(evt.Id, out var started)) _joustNativeEvents.Add(started.Id);
    }
    private void StopJoustScenes()
    {
        foreach (var id in _joustNativeEvents)
            if (EventLookup.TryGetValue(id, out var scene))
            { scene.CallStack.Clear(); scene.FinishCinematic(interrupted: true); }
        _joustNativeEvents.Clear();
    }
    private void UpdateJoust(long? suppliedNow = null)
    {
        CompleteJoustCareer();
        if (_joust == null || _joust.Done || _joustArena == null) return;
        if (!InGame || Client == null || MapId != _joustMap || _joustInstance?.MapInstanceId != MapInstanceId ||
            _joustInstance.IsDisposed || _joustOpponent is not { IsDisposed: false } || IsDead || HasPendingDeath ||
            ActiveMountId != _joustArena.MountId || CombatTimer > Intersect.Framework.Core.Timing.Global.Milliseconds)
        { EndJoust(true); return; }
        var now = suppliedNow ?? Environment.TickCount64;
        if (now - _joustBegan > 600000) { EndJoust(true); return; }
        if (_joust.Phase == JoustPhase.Preparation && HasBlockingEventHold)
        {
            _joust.HoldPreparation(now);
            if (now - _joustSent >= 100) SendJoustView(now);
            return;
        }
        // After a long server suspension, abort rather than score unseen passes or
        // replay an entire charge instantaneously through other players.
        if (now > _joust.Deadline + 250) { EndJoust(true); return; }
        // Move up to the old phase boundary before resolving it. Both horses reach the
        // midpoint at the same server deadline, regardless of their normal movement stats.
        if (!MoveJoustTrack(now)) { EndJoust(true); return; }
        var before = _joust.Phase;
        _joust.Advance(now);
        if (_joust.Phase is JoustPhase.Charge or JoustPhase.Aim && _joust.Pass != _joustChargePresented)
        { _joustChargePresented = _joust.Pass; RunJoustEvent(_joustArena.ChargeEventId); }
        if (_joust.ResolvedPass > _joustImpactPresented)
        {
            _joustImpactPresented = _joust.ResolvedPass;
            RunJoustEvent(_joust.Strikes.Any(s => s.Hit == JoustHit.Unhorsed) && _joustArena.UnhorseEventId != Guid.Empty
                ? _joustArena.UnhorseEventId : _joustArena.ImpactEventId);
        }
        if (!_joust.Done && !MoveJoustTrack(now)) { EndJoust(true); return; }
        if (_joust.Done)
        {
            _joustPendingReward = _joust;
            try { SendJoustView(now); }
            finally { RestoreJoustMount(); }
            CompleteJoustCareer();
            if (_joust.Winner == 0) RunJoustEvent(_joustArena.VictoryEventId);
        }
        else if (before != _joust.Phase || now - _joustSent >= 100) SendJoustView(now);
    }
    private bool MoveJoustTrack(long now)
    {
        if (_joust == null || _joustArena == null || _joustOpponent == null || _joustInstance == null) return false;
        var pose = JoustTrack.Sample(_joustArena, _joust, now);
        if (Y != _joustArena.StartY || _joustOpponent.Y != _joustArena.OpponentY) return false;
        if (_joust.Phase == JoustPhase.Preparation)
        {
            if (_joustMoveMs != 0)
            {
                _joustMoveMs = _joustOpponent.StepMilliseconds = 0;
                // Reliable snapshots end the scripted interpolation/galop between passes.
                PacketSender.SendEntityDataToProximity(this);
                PacketSender.SendEntityDataToProximity(_joustOpponent);
            }
            return X == pose.PlayerX && _joustOpponent.X == pose.OpponentX;
        }
        _joustMoveMs = _joustOpponent.StepMilliseconds = pose.StepMilliseconds;
        _joustRouteThread = Environment.CurrentManagedThreadId;
        try { return MoveJoustActor(this, pose.PlayerX) && MoveJoustActor(_joustOpponent, pose.OpponentX); }
        finally { _joustRouteThread = 0; }
    }
    private bool MoveJoustActor(Entity entity, int targetX)
    {
        // Catch up every crossed tile through the native collision checks, never a direct warp.
        if (Math.Abs(entity.X - targetX) > 64) return false;
        while (entity.X != targetX)
        {
            var direction = entity.X < targetX ? Direction.Right : Direction.Left;
            if (!JoustRuntime.TileClear(_joustInstance!, entity.X + (direction == Direction.Right ? 1 : -1), entity.Y, entity) ||
                !entity.CanMoveInDirection(direction, out _, out _)) return false;
            var oldX = entity.X;
            entity.MoveTimer = 0;
            entity.Move(direction, this, correction: true);
            if (entity.MapId != _joustMap || entity.X == oldX) return false;
        }
        return true;
    }
    private void SendJoustView(long now)
    {
        if (_joust == null || _joustArena == null) return;
        _joustSent = now;
        SendPacket(new JoustStatePacket { ViewJson = JsonSerializer.Serialize(new JoustView {
            MatchId = _joust.Id, ArenaId = _joustArena.Id, Phase = _joust.Phase, Pass = _joust.Pass,
            Passes = _joust.Passes, RemainingMs = (int)Math.Max(0, _joust.Deadline - now),
            PhaseDurationMs = (int)(_joust.Deadline - _joust.PhaseStarted),
            PlayerScore = _joust.Scores[0], OpponentScore = _joust.Scores[1], Aim = _joust.Aim(0),
            PlayerStrike = _joust.Strikes[0], OpponentStrike = _joust.Strikes[1], Winner = _joust.Winner }) });
    }
    private void RestoreJoustMount()
    {
        StopJoustScenes();
        if (_joustArena != null) JoustRuntime.Release(_joustArena.Id, Id);
        if (_joustOpponent is { } opponent)
        {
            _joustOpponent = null;
            _joustInstance?.RemoveEntity(opponent);
            opponent.Dispose(); PacketSender.SendEntityLeave(opponent);
        }
        _joustInstance = null; _joustMoveMs = 0;
        if (_joust != null) SendPacket(new HoldPlayerPacket(_joust.Id, Guid.Empty, true));
        var previous = MountRuntime.Find(_mountBeforeJoust);
        ActiveMountId = previous != null && MountRuntime.Owns(this, previous) && CanRide(previous) ? previous.Id : Guid.Empty;
        _mountBeforeJoust = Guid.Empty;
        if (MapController.Get(MapId) != null) PacketSender.SendEntityDataToProximity(this);
        SendMountState();
    }
    private void EndJoust(bool abandon)
    {
        lock (EntityLock)
        {
            if (_joust is not { Done: false }) return;
            if (abandon) _joust.Abandon(0);
            try { SendJoustView(Environment.TickCount64); }
            finally { RestoreJoustMount(); }
        }
    }
}
