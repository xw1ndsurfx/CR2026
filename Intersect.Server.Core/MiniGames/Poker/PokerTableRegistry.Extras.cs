#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Intersect.Server.MiniGames.Poker;

public sealed record PokerWinNotice(PokerSession Recipient, Guid TableInstanceId, long HandId,
    string PlayerName, long NetChips, bool AnnounceGlobally);

public sealed partial class PokerTableRegistry
{
    private sealed class Extras
    {
        public readonly PokerHandLedger Ledger = new();
        public readonly Dictionary<Guid, int> SelectedBacks = new();
        public readonly Dictionary<Guid, int> HandBacks = new();
        public readonly HashSet<Guid> EligibleHumans = new();
    }
    private readonly Queue<PokerWinNotice> _wins = new();

    public PokerRegistryResult SelectCardBack(PokerPresence caller, Guid tableId, int backId)
    {
        lock (_gate)
        {
            var error = Find(caller, tableId, out var member);
            if (error != PokerRegistryError.None) return new(error);
            if (!PokerBackCatalog.IsValid(backId)) return Rejected(member!, PokerError.InvalidAmount);
            var entry = member!.Entry;
            entry.Extras.SelectedBacks[caller.Session.PlayerId] = backId;
            // Cosmetic changes do not invalidate betting actions or change balances.
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

    private static int SelectedBack(Entry entry, Guid player) => entry.Npcs.ContainsKey(player)
        ? entry.Options.NpcCardBackId : entry.Extras.SelectedBacks.GetValueOrDefault(player);

    private static PokerError StartTrackedHand(Entry entry, Guid requester, DateTimeOffset now)
    {
        var before = Current(entry);
        var result = entry.Table.StartHand(requester, now);
        if (result != PokerError.None) return result;
        entry.Extras.Ledger.Begin(before, Current(entry).HandId);
        entry.Extras.HandBacks.Clear();
        entry.Extras.EligibleHumans.Clear();
        foreach (var seat in before.Seats)
        {
            entry.Extras.HandBacks[seat.PlayerId] = SelectedBack(entry, seat.PlayerId);
            if (!seat.Leaving && seat.Chips > 0 && entry.Players.Contains(seat.PlayerId))
                entry.Extras.EligibleHumans.Add(seat.PlayerId);
        }
        return result;
    }

    private void CaptureCompleted(Entry entry)
    {
        if (entry.Players.Count == 0) return;
        foreach (var win in entry.Extras.Ledger.Complete(Current(entry)))
        {
            if (!entry.Extras.EligibleHumans.Contains(win.PlayerId) ||
                !entry.Players.Contains(win.PlayerId) || !_members.TryGetValue(win.PlayerId, out var member)) continue;
            _wins.Enqueue(new(member.Presence.Session, entry.Id, entry.Extras.Ledger.HandId,
                win.Name, win.Chips, entry.Options.AnnounceWins));
        }
    }

    private static PokerPresentation Present(Entry entry, Guid player)
    {
        var state = entry.Table.Snapshot(player);
        var net = state.Phase == PokerPhase.Finished && entry.Extras.EligibleHumans.Contains(player)
            ? entry.Extras.Ledger.NetWin(player, state.HandId) : 0;
        return new(entry.Npcs.Keys.ToArray(), entry.DealerNpcId, entry.Options.AutoStart, entry.Options.DealAnimationId)
        {
            CardBacks = state.Seats.Select(s => new PokerSeatBack(s.PlayerId,
                Playing(state.Phase) && s.InHand
                    ? entry.Extras.HandBacks.GetValueOrDefault(s.PlayerId, SelectedBack(entry, s.PlayerId))
                    : SelectedBack(entry, s.PlayerId), SelectedBack(entry, s.PlayerId))).ToArray(),
            NetWin = net,
            VictoryAnimationId = net > 0 ? entry.Options.VictoryAnimationId : Guid.Empty,
        };
    }
}
