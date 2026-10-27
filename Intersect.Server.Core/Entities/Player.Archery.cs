using System.Text.Json;
using Intersect.Framework.Core.Archery;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Archery;
using Intersect.Server.Maps;
using Intersect.Utilities;
using Intersect.Core;
using Microsoft.Extensions.Logging;

namespace Intersect.Server.Entities;
public partial class Player
{
    private ArcheryMatch _archery;
    private ArcheryRange _archeryRange;
    private Guid _archeryInstance;
    private long _archerySent;
    private long _royalRequestAt;
    private long _archeryStartAt;
    private Guid _archeryCompetition;
    private bool _archeryWaiting;
    private long _archeryCompetitionSent;
    private long _archeryCompetitionPolled;
    private Guid _archeryStartToken;
    private long _archeryLeaderboardAt;
    private Dictionary<string, long> _masteryExperience;
    private readonly Dictionary<Guid, (Guid Weapon, long Time)> _masteryHits = new();
    private long _masteryAwardAt;
    private long _masteryWindow;
    private int _masteryAwards;
    internal bool IsArcheryActive => _archeryWaiting || _archery is { Done: false };

    private Dictionary<string, long> MasteryExperience => _masteryExperience ??= RoyalCombatRuntime.Load(Id);
    internal void OpenArcheryFromEvent() => SendPacket(new ArcheryStatePacket { Open = true, ConfigurationJson = RoyalCombatRuntime.Current.ToJson() });
    internal void LeaveArcheryFromEvent() => EndArchery();
    internal double MasteryMultiplier(ItemDescriptor weapon) // Use the projectile's source item, including after equipment changes.
    {
        lock (EntityLock)
        {
            if (weapon == null) return 1;
            try
            {
                var category = RoyalCombatRuntime.Current.Masteries.FirstOrDefault(m => m.WeaponIds.Contains(weapon.Id));
                if (category == null) return 1;
                return 1 + ((category.Level(MasteryExperience.GetValueOrDefault(category.Key)) - 1) * category.DamagePercentPerLevel + MasteryPassives(weapon).Damage) / 100;
            }
            catch (Exception e) when (e is IOException or Microsoft.Data.Sqlite.SqliteException or JsonException)
            { ApplicationContext.Context.Value?.Logger.LogError(e, "Weapon mastery could not be loaded for {CharacterId}", Id); return 1; }
        }
    }
    internal void RecordMasteryHit(Npc npc, ItemDescriptor weapon)
    {
        lock (EntityLock)
        {
            if (weapon == null || npc.PetOwner != null) return;
            if (_masteryHits.Count >= 256) _masteryHits.Clear();
            _masteryHits[npc.Id] = (weapon.Id, Environment.TickCount64);
        }
    }
    private void AwardWeaponMastery(Npc npc)
    {
        lock (EntityLock)
        {
            if (!_masteryHits.Remove(npc.Id, out var hit)) return;
            var now = Environment.TickCount64;
            if (npc.PetOwner != null || npc.Descriptor.Experience <= 0 || _isAfk || now - hit.Time > 30000 ||
                now - _masteryAwardAt < 2000 || npc.Level + 5 < Level) return;
            if (now - _masteryWindow >= 60000) { _masteryWindow = now; _masteryAwards = 0; }
            if (_masteryAwards >= 20) return;
            try
            {
                var category = RoyalCombatRuntime.Current.Masteries.FirstOrDefault(m => m.WeaponIds.Contains(hit.Weapon));
                if (category == null || MasteryExperience.GetValueOrDefault(category.Key) >= category.Maximum) return;
                // Commit before publishing. A failed database write never grants a live damage bonus.
                MasteryExperience[category.Key] = RoyalCombatRuntime.Award(Id, category, 5);
                _masteryAwardAt = now; _masteryAwards++;
                SendMasteries(false);
            }
            catch (Exception e) when (e is IOException or Microsoft.Data.Sqlite.SqliteException or JsonException)
            { ApplicationContext.Context.Value?.Logger.LogError(e, "Weapon mastery could not be saved for {CharacterId}", Id); }
        }
    }
    private void SendMasteries(bool open, string message = "")
    {
        if (open) SynchronizeMasterySpells();
        if (PetCollection.MasteryRespec != null) message = "Your paid mastery reset is awaiting recovery. No further payment was taken.";
        SendPacket(new ArcheryStatePacket { Open = open, Message = message,
            ConfigurationJson = RoyalCombatRuntime.Current.ToJson(), MasteriesJson = JsonSerializer.Serialize(MasteryExperience),
            MasteryRanksJson = JsonSerializer.Serialize(MasteryRanks) });
    }

    internal void HandleArcheryRequest(ArcheryRequestPacket packet)
    {
        if (packet.Action is ArcheryAction.Ammunition or ArcheryAction.SelectAmmunition) { HandleAmmunitionRequest(packet); return; }
        if (packet.Action == ArcheryAction.BuyTalent) { HandleTalentPurchase(packet); return; }
        if (packet.Action == ArcheryAction.Respec) { HandleMasteryRespec(packet); return; }
        lock (EntityLock)
        {
            var now = Environment.TickCount64;
            if (packet.Action == ArcheryAction.InspectMasteries)
            {
                if (now - _royalRequestAt < 1000) return; _royalRequestAt = now;
                var target = FindOnline(packet.TargetId);
                if (target == null) return;
                try
                {
                    var snapshot = RoyalCombatRuntime.MasterySnapshot(target.Id);
                    SendPacket(new ArcheryStatePacket { Open = true, MasteriesReadOnly = true, MasteriesOwner = target.Name,
                        ConfigurationJson = RoyalCombatRuntime.Current.ToJson(), MasteriesJson = JsonSerializer.Serialize(snapshot.Experience),
                        MasteryRanksJson = JsonSerializer.Serialize(snapshot.Ranks) });
                }
                catch (Exception e) when (e is IOException or Microsoft.Data.Sqlite.SqliteException or UnauthorizedAccessException)
                { SendPacket(new ArcheryStatePacket { Open = true, MasteriesReadOnly = true, MasteriesOwner = target.Name, Message = "Weapon mastery information is temporarily unavailable." }); }
                return;
            }
            if (packet.Action == ArcheryAction.Leaderboard)
            {
                if (!Enum.IsDefined((ArcheryLeaderboardPeriod)packet.Period)) return;
                if (now - _archeryLeaderboardAt < 1000)
                { SendPacket(new ArcheryStatePacket { RequestId = packet.RequestId, Message = "Please wait before refreshing rankings." }); return; }
                _archeryLeaderboardAt = now;
                try
                {
                    var board = Intersect.Server.Leaderboards.LeaderboardDataRuntime.Archery(packet.RangeId, (ArcheryLeaderboardPeriod)packet.Period);
                    SendPacket(new ArcheryStatePacket { RequestId = packet.RequestId, LeaderboardJson = board == null ? "" : JsonSerializer.Serialize(board),
                        Message = board == null ? "No ranked archery range is available." : "" });
                }
                catch (Exception e) when (e is IOException or Microsoft.Data.Sqlite.SqliteException or UnauthorizedAccessException)
                {
                    ApplicationContext.Context.Value?.Logger.LogError(e, "Archery leaderboard unavailable");
                    SendPacket(new ArcheryStatePacket { RequestId = packet.RequestId, Message = "Archery rankings are temporarily unavailable." });
                }
                return;
            }
            if (packet.Action is ArcheryAction.Open or ArcheryAction.Masteries)
            {
                if (now - _royalRequestAt < 500) return;
                _royalRequestAt = now;
            }
            if (packet.Action == ArcheryAction.Masteries) { SendMasteries(true); return; }
            if (packet.Action == ArcheryAction.Open)
            {
                SendPacket(new ArcheryStatePacket { Open = true, ConfigurationJson = RoyalCombatRuntime.Current.ToJson() });
                if (_archery != null) SendArcheryView(now); return;
            }
            if (packet.Action is ArcheryAction.Start or ArcheryAction.JoinCompetition)
            {
                if (IsArcheryActive || now - _archeryStartAt < 500) return;
                _archeryStartAt = now;
                var competition = packet.Action == ArcheryAction.JoinCompetition;
                var range = RoyalCombatRuntime.Current.Ranges.FirstOrDefault(r => r.Enabled && r.Id == packet.RangeId && r.MapId == MapId);
                if (range == null || (competition ? !range.CompetitionEnabled || Math.Abs(X - range.StartX) > 2 || Math.Abs(Y - range.StartY) > 2 : X != range.StartX || Y != range.StartY) || IsDead || HasPendingDeath || IsCasting ||
                    CombatTimer > Timing.Global.Milliseconds || HasBlockingEventHold || MoveRoute != null ||
                    !CanUseLobbyTravel || _joust is { Done: false } || EventLookup.Values.Any(e => e.CinematicActive))
                { SendPacket(new ArcheryStatePacket { Message = "Stand at the royal shooting position outside combat." }); return; }
                EndArchery();
                _archeryStartToken = packet.RequestId;
                _archeryRange = new() { Id = range.Id, MapId = range.MapId, StartX = X, StartY = Y };
                _archeryInstance = MapInstanceId;
                if (competition)
                {
                    _archeryCompetition = ArcheryCompetitions.Join(range, MapInstanceId, Id, Name, now);
                    if (_archeryCompetition == Guid.Empty) return;
                    _archeryCompetitionPolled = _archeryCompetitionSent = 0;
                    _archery = null; _archeryWaiting = true; Dismount();
                    SendPacket(new HoldPlayerPacket(_archeryCompetition, Guid.Empty, false));
                    UpdateArchery(now); return;
                }
                Dismount(); _archery = new(now, range.Wind, range.Rules);
                SendPacket(new HoldPlayerPacket(_archery.Id, Guid.Empty, false)); SendArcheryView(now); return;
            }
            if (packet.Action == ArcheryAction.Leave && _archeryStartToken != Guid.Empty && packet.MatchId == _archeryStartToken) { EndArchery(); return; }
            if (packet.Action == ArcheryAction.Leave && _archeryCompetition != Guid.Empty &&
                (packet.MatchId == _archeryCompetition || packet.MatchId == _archery?.Id)) { EndArchery(); return; }
            if (_archery == null || packet.MatchId != _archery.Id) return;
            if (packet.Action == ArcheryAction.Leave) { EndArchery(); return; }
            UpdateArchery(now);
            if (packet.Action is ArcheryAction.Draw or ArcheryAction.Release && packet.Round == _archery.Round &&
                _archery.Input(packet.Action == ArcheryAction.Draw, packet.Sequence, packet.Yaw, packet.Pitch, now, packet.Shot)) SendArcheryView(now);
        }
    }
    private void UpdateArchery(long? suppliedNow = null)
    {
        if (!IsArcheryActive && _archeryCompetition == Guid.Empty) return;
        if (!InGame || Client == null || IsDead || HasPendingDeath || MapId != _archeryRange.MapId ||
            MapInstanceId != _archeryInstance || X != _archeryRange.StartX || Y != _archeryRange.StartY ||
            CombatTimer > Timing.Global.Milliseconds)
        { EndArchery(); return; }
        var now = suppliedNow ?? Environment.TickCount64;
        if (_archeryCompetition != Guid.Empty && now - _archeryCompetitionPolled >= 250)
        {
            _archeryCompetitionPolled = now;
            var update = ArcheryCompetitions.Poll(_archeryCompetition, Id, now, _archery?.Score ?? 0,
                _archery?.Phase == ArcheryPhase.Finished, _archery?.Phase == ArcheryPhase.Abandoned);
            ArcheryCompetitions.FlushPending();
            if (update == null || update.View.Cancelled) { EndArchery(); SendPacket(new ArcheryStatePacket { Message = "Competition cancelled: not enough participants." }); return; }
            if (_archeryWaiting && update.View.Started)
            {
                SendPacket(new HoldPlayerPacket(_archeryCompetition, Guid.Empty, true));
                _archeryWaiting = false; _archery = new(update.Started, update.Wind, update.Rules);
                SendPacket(new HoldPlayerPacket(_archery.Id, Guid.Empty, false)); SendArcheryView(now);
            }
            if (update.View.Players.FirstOrDefault(p => p.Id == Id)?.Forfeited == true && _archery is { Done: false })
            {
                _archery.Abandon(); SendArcheryView(now);
                SendPacket(new HoldPlayerPacket(_archery.Id, Guid.Empty, true));
            }
            if (now - _archeryCompetitionSent >= 250)
            {
                _archeryCompetitionSent = now;
                SendPacket(new ArcheryStatePacket { CompetitionJson = JsonSerializer.Serialize(update.View) });
            }
        }
        if (_archeryWaiting) return;
        if (_archery == null || _archery.Done) return;
        var changed = _archery.Advance(now);
        if (changed || now - _archerySent >= 250) SendArcheryView(now);
        if (_archery.Done)
        {
            SendPacket(new HoldPlayerPacket(_archery.Id, Guid.Empty, true));
            if (_archeryCompetition != Guid.Empty)
            {
                ArcheryCompetitions.Poll(_archeryCompetition, Id, now, _archery.Score,
                    _archery.Phase == ArcheryPhase.Finished, _archery.Phase == ArcheryPhase.Abandoned);
                ArcheryCompetitions.FlushPending();
            }
        }
    }
    private void EndArchery()
    {
        lock (EntityLock)
        {
            var now = Environment.TickCount64;
            _archeryStartToken = Guid.Empty;
            if (_archery is { Done: false })
            {
                _archery.Abandon(); SendArcheryView(now);
                SendPacket(new HoldPlayerPacket(_archery.Id, Guid.Empty, true));
            }
            if (_archeryCompetition != Guid.Empty)
            {
                // Publish the last authoritative score before recording a withdrawal.
                ArcheryCompetitions.Poll(_archeryCompetition, Id, now, _archery?.Score ?? 0,
                    _archery?.Phase == ArcheryPhase.Finished, !_archeryWaiting && _archery?.Phase != ArcheryPhase.Finished);
                ArcheryCompetitions.Leave(_archeryCompetition, Id, now);
                ArcheryCompetitions.FlushPending();
                SendPacket(new HoldPlayerPacket(_archeryCompetition, Guid.Empty, true));
                _archeryCompetition = Guid.Empty; _archeryWaiting = false;
                SendPacket(new ArcheryStatePacket { CompetitionJson = "null" });
            }
        }
    }
    private void SendArcheryView(long now)
    {
        _archerySent = now;
        SendPacket(new ArcheryStatePacket { ViewJson = JsonSerializer.Serialize(new ArcheryView {
            MatchId = _archery.Id, Phase = _archery.Phase, Shot = _archery.Shot, Score = _archery.Score,
            Round = _archery.Round, Rounds = _archery.Rounds, TotalArrows = _archery.TotalArrows, MaximumScore = _archery.MaximumScore,
            ShotInRound = _archery.ShotInRound, TargetIndex = _archery.TargetIndex, Targets = _archery.RoundSettings.Targets,
            RemainingMs = _archery.Done ? 0 : (int)Math.Max(0, _archery.RoundDeadline - now),
            Distance = _archery.Distance, Wind = _archery.Wind, ElapsedMs = (int)(now - _archery.Started),
            DrawMs = _archery.Phase == ArcheryPhase.Drawing ? (int)(now - _archery.DrawStarted) : 0,
            FlightMs = _archery.Phase == ArcheryPhase.Flying ? (int)(now - _archery.Released) : 0,
            Yaw = _archery.Yaw, Pitch = _archery.Pitch, Charge = _archery.Charge, Impacts = _archery.Impacts.ToArray() }) });
    }
}
