#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Intersect.Framework.Core.MiniGames;
using Intersect.Server.MiniGames.Progression;

namespace Intersect.Server.MiniGames.Poker;

public sealed record PokerWinNotice(PokerSession Recipient, Guid TableInstanceId, long HandId,
    string PlayerName, long NetChips, bool AnnounceGlobally);
public sealed record PokerLevelRewardNotice(PokerSession Recipient, Guid TableInstanceId, int Level, PokerLevelReward[] Rewards);
public sealed record PokerQuestNotice(PokerSession Recipient, Guid TableInstanceId, PokerQuestUpdate Update);

public sealed partial class PokerTableRegistry
{
    private sealed class Extras
    {
        public readonly PokerHandLedger Ledger = new();
        public readonly Dictionary<Guid, int> SelectedBacks = new();
        public readonly Dictionary<Guid, int> HandBacks = new();
        public readonly HashSet<Guid> EligibleHumans = new();
        public readonly Dictionary<Guid, MiniGameProgress> Profiles = new();
        public readonly List<PokerPublicDecision> Decisions = new();
        public long DecisionSequence;
    }
    private readonly Queue<PokerWinNotice> _wins = new();
    private readonly Queue<PokerLevelRewardNotice> _levelRewards = new();
    private readonly Queue<PokerQuestNotice> _questUpdates = new();

    public PokerRegistryResult SelectCardBack(PokerPresence caller, Guid tableId, int backId)
    {
        lock (_gate)
        {
            var error = Find(caller, tableId, out var member);
            if (error != PokerRegistryError.None) return new(error);
            if (!PokerBackCatalog.IsValid(backId)) return Rejected(member!, PokerError.InvalidAmount);
            var entry = member!.Entry;
            var player = caller.Session.PlayerId;
            var progress = entry.Extras.Profiles[player];
            if (!MiniGameProgression.IsUnlocked(backId, progress.Experience))
                return View(member) with { Error = PokerRegistryError.CardBackLocked };
            try
            {
                progress = _progression.SelectBack(player, MiniGameProgression.Poker, backId);
                if (!progress.IsValid) throw new InvalidOperationException("Invalid saved card back.");
            }
            catch (ArgumentOutOfRangeException)
            { return View(member) with { Error = PokerRegistryError.CardBackLocked }; }
            catch (Exception exception)
            {
                ProgressionFailure = exception;
                return View(member) with { Error = PokerRegistryError.ProgressionUnavailable };
            }
            entry.Extras.Profiles[player] = progress;
            entry.Extras.SelectedBacks[player] = progress.SelectedBack;
            entry.PublishedRevision = -1;
            return View(member);
        }
    }

    public PokerWinNotice[] CollectWins()
    {
        lock (_gate)
        {
            var notices = _wins.ToArray();
            _wins.Clear();
            return notices;
        }
    }

    public PokerLevelRewardNotice[] CollectLevelRewards()
    {
        lock (_gate)
        {
            var notices = _levelRewards.ToArray();
            _levelRewards.Clear();
            return notices;
        }
    }

    public PokerQuestNotice[] CollectQuestUpdates()
    {
        lock (_gate)
        {
            var notices = _questUpdates.ToArray();
            _questUpdates.Clear();
            return notices;
        }
    }

    private static int SelectedBack(Entry entry, Guid player) => entry.Npcs.ContainsKey(player)
        ? entry.Options.NpcCardBackId : entry.Extras.SelectedBacks.GetValueOrDefault(player);

    private PokerError StartTrackedHand(Entry entry, Guid requester, DateTimeOffset now)
    {
        if (HasPendingExperience(entry)) return PokerError.IllegalAction;
        var before = Current(entry);
        var result = entry.Table.StartHand(requester, now);
        if (result != PokerError.None) return result;
        var after = Current(entry);
        entry.Extras.Ledger.Begin(before, after.HandId);
        entry.Extras.HandBacks.Clear();
        entry.Extras.EligibleHumans.Clear();
        entry.Extras.Decisions.Clear();
        foreach (var seat in before.Seats)
        {
            entry.Extras.HandBacks[seat.PlayerId] = SelectedBack(entry, seat.PlayerId);
            if (!seat.Leaving && seat.Chips > 0 && entry.Players.Contains(seat.PlayerId))
                entry.Extras.EligibleHumans.Add(seat.PlayerId);
        }
        var dealer = after.Seats.FirstOrDefault(s => s.PlayerId == entry.DealerNpcId) ??
            after.Seats.FirstOrDefault(s => s.Seat == after.DealerSeat);
        if (dealer != null) Decision(entry, dealer, "deal");
        return result;
    }

    private void CaptureCompleted(Entry entry)
    {
        if (entry.Players.Count == 0) return;
        var state = Current(entry);
        var alreadyFinished = entry.Extras.Ledger.IsFinished;
        var wins = entry.Extras.Ledger.Complete(state);
        if (alreadyFinished || !entry.Extras.Ledger.IsFinished) return;

        foreach (var win in wins)
        {
            var seat = state.Seats.First(s => s.PlayerId == win.PlayerId);
            Decision(entry, seat, "wins", win.Chips);
            if (!entry.Extras.EligibleHumans.Contains(win.PlayerId) ||
                !entry.Players.Contains(win.PlayerId) || !_members.TryGetValue(win.PlayerId, out var member)) continue;
            _wins.Enqueue(new(member.Presence.Session, entry.Id, entry.Extras.Ledger.HandId,
                win.Name, win.Chips, entry.Options.AnnounceWins));
            QueueExperience(entry, win.PlayerId, state.HandId);
        }

        ProcessExperience(DateTimeOffset.UtcNow);
        foreach (var player in entry.Extras.EligibleHumans)
        {
            if (!_members.TryGetValue(player, out var member)) continue;
            var net = wins.FirstOrDefault(w => w.PlayerId == player)?.Chips ?? 0;
            var level = entry.Extras.Profiles.GetValueOrDefault(player)?.Level ?? 1;
            _questUpdates.Enqueue(new(member.Presence.Session, entry.Id, new PokerQuestUpdate(true, net, level)));
        }
    }

    private PokerPresentation Present(Entry entry, Guid player)
    {
        var state = entry.Table.Snapshot(player);
        var net = state.Phase == PokerPhase.Finished && entry.Extras.EligibleHumans.Contains(player)
            ? entry.Extras.Ledger.NetWin(player, state.HandId) : 0;
        var profile = entry.Extras.Profiles.GetValueOrDefault(player) ?? new();
        return new(entry.Npcs.Keys.ToArray(), entry.DealerNpcId, entry.Options.AutoStart, entry.Options.DealAnimationId)
        {
            CardBacks = state.Seats.Select(s =>
            {
                var selected = SelectedBack(entry, s.PlayerId);
                // Card backs are cosmetic only: apply a human's newly selected back immediately,
                // even during the current hand. NPC backs still come from the event configuration.
                return new PokerSeatBack(s.PlayerId, selected, selected);
            }).ToArray(),
            NetWin = net,
            VictoryAnimationId = net > 0 ? entry.Options.VictoryAnimationId : Guid.Empty,
            Experience = profile.Experience,
            Wins = profile.Wins,
            ProgressPending = HasPendingExperience(entry),
            Decisions = entry.Extras.Decisions.ToArray(),
            Sounds = entry.Options.EffectiveSounds,
            Animations = entry.Options.EffectiveAnimations,
        };
    }
}
