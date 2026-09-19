#nullable enable
using System;
using System.Linq;

namespace Intersect.Server.MiniGames.Poker;

/// <summary>Public actions only. Never contains cards, evaluator scores or NPC policy input.</summary>
public sealed record PokerPublicDecision(long Sequence, Guid PlayerId, string Name, string Action, long Amount, bool Automatic);

public sealed partial class PokerTableRegistry
{
    private static void Decision(Entry entry, PokerSeatView seat, string action, long amount = 0, bool automatic = false)
    {
        entry.Extras.Decisions.Add(new(++entry.Extras.DecisionSequence, seat.PlayerId, seat.Name, action, amount, automatic));
        if (entry.Extras.Decisions.Count > 12) entry.Extras.Decisions.RemoveAt(0);
        entry.PublishedRevision = -1;
    }
    private static void AdditionalFolds(Entry entry, PokerSnapshot before, PokerSnapshot after, Guid primary)
    {
        foreach (var seat in after.Seats.Where(s => s.PlayerId != primary && s.Folded &&
            before.Seats.Any(b => b.PlayerId == s.PlayerId && !b.Folded && b.InHand)))
            Decision(entry, seat, "fold", automatic: true);
    }
    private static PokerError ActTracked(Entry entry, Guid player, long hand, long revision,
        PokerAction action, long raiseTo, DateTimeOffset now)
    {
        var before = entry.Table.Snapshot(player);
        var result = entry.Table.Act(player, hand, revision, action, raiseTo, now);
        var after = entry.Table.Snapshot(player);
        if (after.Revision == before.Revision) return result;
        var actor = before.Seats.FirstOrDefault(s => s.Seat == before.ActingSeat);
        if (actor != null)
        {
            if (result == PokerError.None)
            {
                var kind = action switch { PokerAction.Fold => "fold", PokerAction.Check => "check", PokerAction.Call => "call", _ => "raise" };
                Decision(entry, actor, kind, action == PokerAction.RaiseTo ? raiseTo : action == PokerAction.Call ? before.ToCall : 0);
            }
            else if (result == PokerError.StaleState && now >= before.Deadline)
                Decision(entry, actor, actor.StreetBet >= before.CurrentBet ? "check" : "fold", automatic: true);
            AdditionalFolds(entry, before, after, actor.PlayerId);
        }
        return result;
    }
    private static void TickTracked(Entry entry, DateTimeOffset now)
    {
        var before = Current(entry);
        if (!entry.Table.Tick(now)) return;
        var actor = before.Seats.FirstOrDefault(s => s.Seat == before.ActingSeat);
        if (actor == null) return;
        Decision(entry, actor, actor.StreetBet >= before.CurrentBet ? "check" : "fold", automatic: true);
        AdditionalFolds(entry, before, Current(entry), actor.PlayerId);
    }
}
