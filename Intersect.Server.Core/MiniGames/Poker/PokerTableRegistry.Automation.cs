#nullable enable
using System;
using System.Linq;

namespace Intersect.Server.MiniGames.Poker;

public sealed partial class PokerTableRegistry
{
    // All helpers execute under _gate. They never call a player, network or asset callback.
    private static void MakeRoomForHuman(Entry entry, DateTimeOffset now)
    {
        var state = Current(entry);
        if (Playing(state.Phase) || state.Seats.Length < entry.Rules.MaxPlayers) return;
        var replaceable = entry.Npcs.Keys.FirstOrDefault(id => id != entry.DealerNpcId);
        if (replaceable != Guid.Empty) RemoveNpc(entry, replaceable, now);
    }
    private static void RemoveNpc(Entry entry, Guid id, DateTimeOffset now)
    {
        if (entry.Table.Leave(id, now) != PokerError.None)
            throw new InvalidOperationException("Unable to remove an idle poker NPC.");
        entry.Npcs.Remove(id);
        if (entry.DealerNpcId == id) entry.DealerNpcId = Guid.Empty;
    }
    private static void AddNpc(Entry entry, string name, bool dealer, Guid id = default)
    {
        if (id == Guid.Empty) id = Guid.NewGuid();
        if (entry.Table.Join(id, name) != PokerError.None)
            throw new InvalidOperationException("Unable to seat a configured poker NPC.");
        entry.Npcs.Add(id, name);
        if (dealer) entry.DealerNpcId = id;
    }
    private static void PrepareOpponents(Entry entry, DateTimeOffset now, bool refill)
    {
        if (Playing(Current(entry).Phase)) return;
        if (entry.Options.DealerPlays && entry.DealerNpcId == Guid.Empty)
            AddNpc(entry, "Dealer", dealer: true);
        var desiredOthers = Math.Min(entry.Options.NpcPlayers,
            entry.Rules.MaxPlayers - entry.Players.Count - (entry.Options.DealerPlays ? 1 : 0));
        while (entry.Npcs.Keys.Count(id => id != entry.DealerNpcId) > desiredOthers)
            RemoveNpc(entry, entry.Npcs.Keys.First(id => id != entry.DealerNpcId), now);
        while (entry.Npcs.Keys.Count(id => id != entry.DealerNpcId) < desiredOthers)
        {
            var name = Enumerable.Range(1, 5).Select(i => "Guest " + i).First(n => !entry.Npcs.ContainsValue(n));
            AddNpc(entry, name, dealer: false);
        }
        if (!refill) return;
        // TEST MODE ONLY. Refill broke NPCs between hands. Never refill a human or persistent wallet.
        foreach (var seat in Current(entry).Seats.Where(s => s.Chips == 0 && entry.Npcs.ContainsKey(s.PlayerId)).ToArray())
        {
            var dealer = seat.PlayerId == entry.DealerNpcId;
            var name = entry.Npcs[seat.PlayerId];
            RemoveNpc(entry, seat.PlayerId, now);
            AddNpc(entry, name, dealer, seat.PlayerId);
        }
    }
    private void AdvanceAutomation(Entry entry, DateTimeOffset now)
    {
        var state = Current(entry);
        if (!Playing(state.Phase))
        {
            entry.NpcSeat = -1;
            var human = state.Seats.FirstOrDefault(s => entry.Players.Contains(s.PlayerId) &&
                !_members[s.PlayerId].Leaving && !s.Leaving && s.Chips > 0);
            if (!entry.Options.AutoStart || human == null)
            {
                entry.NextHandAt = null;
                return;
            }
            // Configured guests can have all yielded their seats to humans. Recreate them at
            // the next hand if a human subsequently leaves; do not strand the remaining human.
            if (!entry.Options.DealerPlays && entry.Options.NpcPlayers == 0 &&
                state.Seats.Count(s => !s.Leaving && s.Chips > 0) < 2)
            {
                entry.NextHandAt = null;
                return;
            }
            entry.NextHandAt ??= now.AddSeconds(5);
            if (now < entry.NextHandAt.Value) return;
            PrepareOpponents(entry, now, refill: true);
            entry.Table.StartHand(human.PlayerId, now);
            entry.NextHandAt = null;
            return;
        }
        entry.NextHandAt = null;
        var actor = state.Seats.FirstOrDefault(s => s.Seat == state.ActingSeat);
        if (actor == null || !entry.Npcs.ContainsKey(actor.PlayerId))
        {
            entry.NpcSeat = -1;
            return;
        }
        if (entry.NpcHand != state.HandId || entry.NpcSeat != state.ActingSeat)
        {
            entry.NpcHand = state.HandId;
            entry.NpcSeat = state.ActingSeat;
            entry.NpcDue = now.AddMilliseconds(1200);
            return;
        }
        if (now < entry.NpcDue) return;
        // Only this recipient's private cards reach the NPC policy. Never pass a human view.
        var npcView = entry.Table.Snapshot(actor.PlayerId);
        var decision = PokerNpcPolicy.Choose(npcView, actor.PlayerId, entry.Rules.BigBlind);
        var result = entry.Table.Act(actor.PlayerId, npcView.HandId, npcView.Revision,
            decision.Action, decision.Amount, now);
        if (result == PokerError.IllegalAction || result == PokerError.InvalidAmount)
        {
            npcView = entry.Table.Snapshot(actor.PlayerId);
            entry.Table.Act(actor.PlayerId, npcView.HandId, npcView.Revision,
                npcView.ToCall == 0 ? PokerAction.Check : PokerAction.Fold, 0, now);
        }
        entry.NpcSeat = -1;
    }
}
