using System.Collections.Concurrent;
using System.Text.Json;
using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.MountRacing;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.Maps;
using Microsoft.Extensions.Logging;

namespace Intersect.Server.MountRacing;

internal static class MountRaceRuntime
{
    private sealed record Waiting(Player Player, Guid CourseId, string Fingerprint, long Since);
    private sealed record Request(Player Player, MountRaceRequestPacket Packet);
    private sealed class Session(MountRaceRound round, Player[] players)
    {
        internal MountRaceRound Round = round;
        internal Player[] Players = players;
        internal HashSet<Guid> Entered = [], Returned = [], Recorded = [], Dismissed = [];
        internal long LastSent, FinishedAt, RetryAt;
        internal string Fingerprint = JsonSerializer.Serialize(round.Course);
    }
    private static readonly Dictionary<Guid, Waiting> WaitingPlayers = [];
    private static readonly Dictionary<Guid, Session> Sessions = [];
    private static readonly ConcurrentDictionary<Guid, Request> Pending = new();
    private static readonly Dictionary<Guid, long> LastRequest = [];
    private static long _nextTick, _nextRecovery;
    private static MountRaceConfiguration Configuration => MountRaceStore.Configuration;
    private static bool Online(Player p) => p.InGame && ReferenceEquals(Player.FindOnline(p.Id), p);
    private static Session? SessionFor(Guid id) => Sessions.Values.FirstOrDefault(s => !s.Dismissed.Contains(id) && s.Players.Any(p => p.Id == id));
    internal static void Enqueue(Player player, MountRaceRequestPacket packet)
    {
        if (!Enum.IsDefined(packet.Action) || packet.Action is MountRaceAction.GetConfiguration or MountRaceAction.SaveConfiguration || Pending.Count >= 512) return;
        Pending.AddOrUpdate(player.Id, new Request(player, packet), (_, prior) =>
            packet.Action == MountRaceAction.Leave || prior.Packet.Action is MountRaceAction.Poll or MountRaceAction.Open ? new(player, packet) : prior);
    }
    internal static string CanJoin(Player p, MountRaceCourse course)
    {
        if (!Configuration.Enabled || !course.Enabled) return "This race circuit is unavailable.";
        if (!Online(p) || !p.CanUseLobbyTravel || p.IsMountRacing || p.IsCasting || p.IsBlocking || p.MoveRoute != null ||
            p.HideEntity || p.NavalShipId != Guid.Empty || p.MapInstanceId != Guid.Empty || p.Z != 0 ||
            p.EventLookup.Values.Any(e => e.CinematicActive)) return "Finish your current activity before entering a race.";
        if (!MountRaceStore.AvailableWorld(course)) return "This race circuit is unavailable.";
        if (Intersect.Server.Mounts.MountRuntime.Find(course.MountId) is not {} mount || !p.CanRide(mount) ||
            p.CachedStatuses.Any(s => s.Type is SpellEffect.Stun or SpellEffect.Snare or SpellEffect.Sleep)) return "Finish your current activity before entering a race.";
        if (p.MapId != course.RegistrationMapId || Math.Abs(p.X - course.RegistrationX) > 3 || Math.Abs(p.Y - course.RegistrationY) > 3)
            return "Meet at this circuit's registration point to enter.";
        if (!Intersect.Server.WorldEvents.WorldActivitiesRuntime.Current.Teleports.Allows(p.MapId, course.MapId, false)) return "Travel to this race circuit is unavailable.";
        return "";
    }
    internal static void Update(long now)
    {
        foreach (var id in Pending.Keys.Take(32))
        {
            if (!Pending.TryRemove(id, out var request) || !Online(request.Player)) continue;
            if (LastRequest.TryGetValue(id, out var last) && now - last < 200) { Pending.TryAdd(id, request); continue; }
            if (!Monitor.TryEnter(request.Player.EntityLock)) { Pending.TryAdd(id, request); continue; }
            try { LastRequest[id] = now; Process(request.Player, request.Packet, now); }
            catch (Exception e) { Log(e); Send(request.Player, message: "Race service unavailable. Please try again.", error: true); }
            finally { Monitor.Exit(request.Player.EntityLock); }
        }
        if (now < _nextTick) return;
        _nextTick = now + 100;
        foreach (var id in LastRequest.Keys.Where(id => Player.FindOnline(id) == null).ToArray()) LastRequest.Remove(id);
        foreach (var wait in WaitingPlayers.Values.ToArray())
        {
            var c = Configuration.Courses.FirstOrDefault(c => c.Id == wait.CourseId);
            if (c == null || JsonSerializer.Serialize(c) != wait.Fingerprint || CanJoin(wait.Player, c).Length > 0 || now - wait.Since > 600000)
            { WaitingPlayers.Remove(wait.Player.Id); Send(wait.Player, message: "Your race registration expired or its conditions changed."); }
        }
        foreach (var c in Configuration.Courses.Where(c => c.Enabled))
        {
            var candidates = WaitingPlayers.Values.Where(w => w.CourseId == c.Id).OrderBy(w => w.Since).Take(c.MaximumPlayers).ToArray();
            if (Sessions.Count < 32 && candidates.Length >= c.MinimumPlayers &&
                (candidates.Length == c.MaximumPlayers || now - candidates[0].Since >= c.QueueSeconds * 1000L))
            {
                try { Create(c, candidates.Select(w => w.Player).ToArray(), false, now); }
                catch (Exception e)
                {
                    Log(e);
                    foreach (var candidate in candidates) { WaitingPlayers.Remove(candidate.Player.Id); Send(candidate.Player, message: "Race service unavailable. Please try again.", error: true); }
                }
            }
        }
        foreach (var s in Sessions.Values.ToArray())
        {
            try { Tick(s, now); }
            catch (Exception e) { Log(e); s.RetryAt = now + 5000; s.Round.Cancel(); }
        }
        // Recovery can outlive an unsuccessful departure/return save, without trapping the character in an instance.
        if (now >= _nextRecovery)
        {
            _nextRecovery = now + 5000;
            foreach (var p in Player.OnlinePlayersSnapshot().Where(p => p.PetCollection.Racing.Return != null && SessionFor(p.Id) == null))
            if (Monitor.TryEnter(p.EntityLock))
                try { p.ReturnFromMountRace(); } catch (Exception e) { Log(e); } finally { Monitor.Exit(p.EntityLock); }
        }
    }
    private static void Process(Player p, MountRaceRequestPacket packet, long now)
    {
        var session = SessionFor(p.Id);
        switch (packet.Action)
        {
            case MountRaceAction.Join:
            case MountRaceAction.Practice:
                var c = Configuration.Courses.FirstOrDefault(c => c.Id == packet.CourseId);
                var error = c == null ? "This race circuit is unavailable." : CanJoin(p, c);
                if (error.Length == 0 && (session != null || WaitingPlayers.ContainsKey(p.Id) || WaitingPlayers.Count >= 128 || Sessions.Count >= 32)) error = "You are already registered, or the race queue is full.";
                if (error.Length == 0 && packet.Action == MountRaceAction.Practice && !c!.AllowPractice) error = "Practice is disabled for this circuit.";
                if (error.Length > 0) { Send(p, message: error, error: true); return; }
                if (packet.Action == MountRaceAction.Practice) Create(c!, [p], true, now);
                else { WaitingPlayers[p.Id] = new(p, c!.Id, JsonSerializer.Serialize(c), now); Send(p, true, selected: c.Id); }
                return;
            case MountRaceAction.Ready:
                if (session?.Round.Confirm(p.Id, packet.RaceId, now) != true) Send(p, message: "This race invitation is no longer available.", error: true);
                else foreach (var rider in session.Players) Send(rider);
                return;
            case MountRaceAction.Leave:
                WaitingPlayers.Remove(p.Id);
                if (session != null && packet.RaceId == session.Round.Id)
                {
                    var rider = session.Round.Riders.First(r => r.Id == p.Id);
                    if (!p.IsMountRacing && (rider.Place > 0 || rider.Withdrawn || session.Round.Phase is MountRacePhase.Finished or MountRacePhase.Cancelled)) session.Dismissed.Add(p.Id);
                    else session.Round.Withdraw(p.Id);
                }
                else if (session != null) { Send(p, message: "This race invitation is no longer available.", error: true); return; }
                Send(p, message: "You left the race."); return;
            case MountRaceAction.Claim:
                var message = p.ClaimRacePrize(packet.RecordId, MountRaceStore.Journal);
                Send(p, true, message, message is not ("Race prize claimed!" or "Race prize already claimed."), packet.CourseId); return;
            default: Send(p, packet.Action == MountRaceAction.Open, selected: packet.CourseId); return;
        }
    }
    private static void Create(MountRaceCourse c, Player[] players, bool practice, long now)
    {
        MountRaceStore.Journal.EnsureAvailable();
        var round = new MountRaceRound(c, players.Select(p => (p.Id, p.Name)), practice, now);
        var session = new Session(round, players); Sessions.Add(round.Id, session);
        foreach (var p in players) { WaitingPlayers.Remove(p.Id); Send(p, true); }
    }
    private static void Tick(Session s, long now)
    {
        if (now < s.RetryAt) return;
        var round = s.Round;
        var current = Configuration.Courses.FirstOrDefault(c => c.Id == round.Course.Id);
        if (!Configuration.Enabled || current == null || s.Fingerprint != JsonSerializer.Serialize(current)) round.Cancel();
        foreach (var p in s.Players)
        {
            if (!Online(p)) { round.Withdraw(p.Id); continue; }
            if (!s.Entered.Contains(p.Id) && round.Phase == MountRacePhase.Ready && CanJoin(p, round.Course).Length > 0) round.Cancel();
            if (s.Entered.Contains(p.Id) && !s.Returned.Contains(p.Id) &&
                (p.IsDead || p.HasPendingDeath || p.MapId != round.Course.MapId || p.MapInstanceId != round.Id || p.ActiveMountId != round.Course.MountId)) round.Withdraw(p.Id);
        }
        round.Tick(now);
        if (round.Phase == MountRacePhase.Ready && round.Riders.All(r => r.Ready)) Start(s, now);
        RaceRider[] standings;
        lock (round.Sync) standings = JsonSerializer.Deserialize<RaceRider[]>(JsonSerializer.Serialize(round.Riders))!;
        foreach (var rider in standings.Where(r => r.Place > 0 && !s.Recorded.Contains(r.Id)))
        {
            var prize = round.Practice ? null : round.Course.Prizes.FirstOrDefault(p => p.Place == rider.Place);
            MountRaceStore.Journal.Add(new() { RaceId = round.Id, CourseId = round.Course.Id, Revision = MountRaceStore.Revision(round.Course),
                PlayerId = rider.Id, Name = rider.Name, Practice = round.Practice, Place = rider.Place,
                ElapsedMilliseconds = rider.ElapsedMilliseconds, FinishedUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ItemId = prize?.ItemId ?? Guid.Empty, Quantity = prize?.Quantity ?? 0 }, round.Course.RewardCooldownSeconds);
            s.Recorded.Add(rider.Id);
        }
        var ended = round.Phase is MountRacePhase.Finished or MountRacePhase.Cancelled;
        foreach (var p in s.Players.Where(p => s.Entered.Contains(p.Id) && !s.Returned.Contains(p.Id) &&
            (ended || round.Riders.First(r => r.Id == p.Id) is { Withdrawn: true } or { Place: > 0 })))
        {
            if (!Online(p)) { s.Returned.Add(p.Id); continue; } // durable return location is recovered on login
            if (!Monitor.TryEnter(p.EntityLock)) continue;
            try { if (p.ReturnFromMountRace()) s.Returned.Add(p.Id); else s.RetryAt = now + 5000; }
            finally { Monitor.Exit(p.EntityLock); }
        }
        if (now - s.LastSent >= 250 || ended && s.FinishedAt == 0)
        { s.LastSent = now; foreach (var p in s.Players.Where(p => Online(p) && !s.Dismissed.Contains(p.Id))) Send(p, ended && s.FinishedAt == 0); }
        if (ended && s.FinishedAt == 0) s.FinishedAt = now;
        if (ended && s.Entered.All(s.Returned.Contains) && now - s.FinishedAt >= 15000)
        { Sessions.Remove(round.Id); foreach (var p in s.Players.Where(p => Online(p) && !s.Dismissed.Contains(p.Id))) Send(p); }
    }
    private static void Start(Session s, long now)
    {
        List<object> held = [];
        try
        {
            foreach (var p in s.Players.OrderBy(p => p.Id))
            { if (!Monitor.TryEnter(p.EntityLock)) return; held.Add(p.EntityLock); }
            if (!MountRaceStore.ValidWorld(s.Round.Course) || s.Players.Any(p => CanJoin(p, s.Round.Course).Length > 0)) { s.Round.Cancel(); return; }
            foreach (var p in s.Players)
            {
                s.Entered.Add(p.Id);
                var entered = p.EnterMountRace(s.Round);
                if (!entered) { s.Round.Cancel(); return; }
            }
            // Countdown starts after every arrival, not while slow clients are still being warped.
            if (!s.Round.Begin(Math.Max(now, Timing.Global.Milliseconds))) s.Round.Cancel();
        }
        finally { for (var i = held.Count - 1; i >= 0; i--) Monitor.Exit(held[i]); }
    }
    private static void Send(Player p, bool open = false, string message = "", bool error = false, Guid selected = default)
    {
        if (!Online(p)) return;
        var s = SessionFor(p.Id); var waiting = WaitingPlayers.GetValueOrDefault(p.Id);
        var round = s?.Round; var courseId = round?.Course.Id ?? waiting?.CourseId ?? selected;
        var view = new MountRaceView { Presentation = Configuration.Presentation, Phase = waiting != null ? MountRacePhase.Queued : MountRacePhase.Idle,
            CourseId = courseId, Message = message, Error = error };
        view.Courses = Configuration.Courses.Where(c => c.Enabled).Select(c => new RaceCourseChoice {
            Id = c.Id, Name = c.Name, Description = c.Description, Laps = c.Laps, MinimumPlayers = c.MinimumPlayers, MaximumPlayers = c.MaximumPlayers,
            AllowPractice = c.AllowPractice, Queued = WaitingPlayers.Values.Count(w => w.CourseId == c.Id), Reason = CanJoin(p, c) }).ToArray();
        foreach (var c in view.Courses) c.CanJoin = c.Reason.Length == 0 && s == null && waiting == null;
        if (round != null) lock (round.Sync)
        {
            view.Phase = round.Phase; view.RaceId = round.Id; view.Practice = round.Practice; view.CourseName = round.Course.Name;
            view.MapId = round.Course.MapId; view.OnTrack = p.RaceRound == round && p.MapInstanceId == round.Id;
            view.Laps = round.Course.Laps; view.Checkpoints = round.Course.Checkpoints;
            view.MovementMilliseconds = round.Course.MovementMilliseconds;
            view.Riders = JsonSerializer.Deserialize<RaceRider[]>(JsonSerializer.Serialize(round.Riders))!;
            view.RemainingMilliseconds = (int)Math.Clamp(round.Deadline - Timing.Global.Milliseconds, 0, int.MaxValue);
            view.ElapsedMilliseconds = round.StartedAt == 0 ? 0 : Math.Max(0, Timing.Global.Milliseconds - round.StartedAt);
        }
        if (round == null || round.Phase is MountRacePhase.Finished or MountRacePhase.Cancelled || round.Riders.Any(r => r.Id == p.Id && r.Place > 0))
        {
            try
            {
                var records = MountRaceStore.Journal.Snapshot();
                view.Rewards = records.Where(r => r.PlayerId == p.Id && !r.Claimed && r.Quantity > 0).Take(64).ToArray();
                var course = Configuration.Courses.FirstOrDefault(c => c.Id == courseId);
                if (course != null) view.Records = records.Where(r => r.CourseId == courseId && r.Revision == MountRaceStore.Revision(course))
                    .GroupBy(r => (r.PlayerId, r.Practice)).Select(g => g.MinBy(r => r.ElapsedMilliseconds)!).OrderBy(r => r.ElapsedMilliseconds).Take(50).ToArray();
            }
            catch (Exception e) { Log(e); view.Message = "Race results are temporarily unavailable."; view.Error = true; }
        }
        p.SendPacket(new MountRaceStatePacket { Open = open, Json = JsonSerializer.Serialize(view) });
    }
    private static void Log(Exception e) => Intersect.Core.ApplicationContext.Context.Value?.Logger.LogError(e, "Mount racing operation failed");
}
