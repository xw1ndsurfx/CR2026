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
    private Dictionary<string, long> _masteryExperience;
    private readonly Dictionary<Guid, (Guid Weapon, long Time)> _masteryHits = new();
    private long _masteryAwardAt;
    private long _masteryWindow;
    private int _masteryAwards;
    internal bool IsArcheryActive => _archery is { Done: false };

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
        SendPacket(new ArcheryStatePacket { Open = open, Message = message,
            ConfigurationJson = RoyalCombatRuntime.Current.ToJson(), MasteriesJson = JsonSerializer.Serialize(MasteryExperience),
            MasteryRanksJson = JsonSerializer.Serialize(MasteryRanks) });
    }

    internal void HandleArcheryRequest(ArcheryRequestPacket packet)
    {
        if (packet.Action is ArcheryAction.Ammunition or ArcheryAction.SelectAmmunition) { HandleAmmunitionRequest(packet); return; }
        if (packet.Action == ArcheryAction.BuyTalent) { HandleTalentPurchase(packet); return; }
        lock (EntityLock)
        {
            var now = Environment.TickCount64;
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
            if (packet.Action == ArcheryAction.Start)
            {
                if (IsArcheryActive || now - _archeryStartAt < 500) return;
                _archeryStartAt = now;
                var range = RoyalCombatRuntime.Current.Ranges.FirstOrDefault(r => r.Enabled && r.Id == packet.RangeId && r.MapId == MapId);
                if (range == null || X != range.StartX || Y != range.StartY || IsDead || HasPendingDeath || IsCasting ||
                    CombatTimer > Timing.Global.Milliseconds || HasBlockingEventHold || MoveRoute != null ||
                    !CanUseLobbyTravel || _joust is { Done: false } || EventLookup.Values.Any(e => e.CinematicActive))
                { SendPacket(new ArcheryStatePacket { Message = "Stand at the royal shooting position outside combat." }); return; }
                _archeryRange = new() { Id = range.Id, MapId = range.MapId, StartX = range.StartX, StartY = range.StartY };
                _archeryInstance = MapInstanceId;
                Dismount(); _archery = new(now, range.Wind, range.Rules);
                SendPacket(new HoldPlayerPacket(_archery.Id, Guid.Empty, false)); SendArcheryView(now); return;
            }
            if (_archery == null || packet.MatchId != _archery.Id) return;
            if (packet.Action == ArcheryAction.Leave) { EndArchery(); return; }
            UpdateArchery(now);
            if (packet.Action is ArcheryAction.Draw or ArcheryAction.Release && packet.Round == _archery.Round &&
                _archery.Input(packet.Action == ArcheryAction.Draw, packet.Sequence, packet.Yaw, packet.Pitch, now, packet.Shot)) SendArcheryView(now);
        }
    }
    private void UpdateArchery(long? suppliedNow = null)
    {
        if (!IsArcheryActive) return;
        if (!InGame || Client == null || IsDead || HasPendingDeath || MapId != _archeryRange.MapId ||
            MapInstanceId != _archeryInstance || X != _archeryRange.StartX || Y != _archeryRange.StartY ||
            CombatTimer > Timing.Global.Milliseconds)
        { EndArchery(); return; }
        var now = suppliedNow ?? Environment.TickCount64;
        var changed = _archery.Advance(now);
        if (changed || now - _archerySent >= 250) SendArcheryView(now);
        if (_archery.Done) SendPacket(new HoldPlayerPacket(_archery.Id, Guid.Empty, true));
    }
    private void EndArchery()
    {
        lock (EntityLock)
        {
            if (!IsArcheryActive) return;
            _archery.Abandon(); SendArcheryView(Environment.TickCount64);
            SendPacket(new HoldPlayerPacket(_archery.Id, Guid.Empty, true));
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
