namespace Intersect.Server.Matchmaking;

// This state machine has no entity, database or network side effects. The runtime
// owns it on the logic thread and revalidates the world before every departure.
internal sealed record MatchMember(Guid Id, string Name, int Level, Guid MapId,
    Guid ShipId = default, Guid CaptainId = default, int ShipCapacity = 0, string ShipName = "", Guid SessionId = default)
{
    internal bool Captain => ShipId != Guid.Empty && CaptainId == Id;
}

internal sealed record MatchRule(Guid EntryId, string Fingerprint, int Target, int Maximum,
    int LevelSpread = 0, int MinimumShips = 0, int MaximumShips = 0);

internal sealed record MatchTicket(Guid Id, Guid EntryId, string Fingerprint, MatchMember[] Members,
    long Created, long Expires);

internal sealed class MatchProposal
{
    internal Guid Id { get; } = Guid.NewGuid();
    internal required MatchRule Rule { get; init; }
    internal required MatchTicket[] Tickets { get; init; }
    internal required Dictionary<Guid, Guid> Ships { get; init; }
    internal required long Deadline { get; init; }
    internal HashSet<Guid> Ready { get; } = [];
    internal bool Launching { get; set; }
    internal MatchMember[] Members => Tickets.SelectMany(t => t.Members).ToArray();
}

internal sealed class MatchmakingQueue
{
    internal readonly Dictionary<Guid, MatchTicket> Tickets = [];
    internal readonly Dictionary<Guid, MatchProposal> Proposals = [];
    private readonly Dictionary<Guid, Guid> _players = [];
    private readonly Dictionary<Guid, Guid> _reserved = [];
    internal MatchTicket? TicketFor(Guid player) => _players.TryGetValue(player, out var id) ? Tickets.GetValueOrDefault(id) : null;
    internal MatchProposal? ProposalFor(Guid player) => TicketFor(player) is { } ticket && _reserved.TryGetValue(ticket.Id, out var id) ? Proposals.GetValueOrDefault(id) : null;
    internal int Waiting(Guid entry) => Tickets.Values.Where(t => t.EntryId == entry && !_reserved.ContainsKey(t.Id)).Sum(t => t.Members.Length);

    internal bool Enqueue(MatchTicket ticket, int maximumPlayers)
    {
        if (ticket.Id == Guid.Empty || ticket.Members.Length == 0 || Tickets.ContainsKey(ticket.Id) ||
            ticket.Members.Any(m => m.Id == Guid.Empty || _players.ContainsKey(m.Id)) ||
            ticket.Members.Select(m => m.Id).Distinct().Count() != ticket.Members.Length ||
            _players.Count + ticket.Members.Length > maximumPlayers) return false;
        Tickets.Add(ticket.Id, ticket);
        foreach (var member in ticket.Members) _players.Add(member.Id, ticket.Id);
        return true;
    }

    internal MatchProposal? Find(MatchRule rule, long now, int readySeconds, Func<MatchMember[], bool> compatible)
    {
        var candidates = Tickets.Values.Where(t => t.EntryId == rule.EntryId && t.Fingerprint == rule.Fingerprint &&
            t.Expires > now && !_reserved.ContainsKey(t.Id)).OrderBy(t => t.Created).ThenBy(t => t.Id).Take(128).ToArray();
        if (rule.MinimumShips > 0)
            candidates = candidates.OrderByDescending(t => t.Members.Any(m => m.Captain)).ThenBy(t => t.Created).ToArray();
        var budget = 4096;
        List<MatchTicket> chosen = [];
        MatchTicket[]? result = null;
        Dictionary<Guid, Guid>? assignments = null;
        bool Search(int index)
        {
            if (--budget < 0) return false;
            var members = chosen.SelectMany(t => t.Members).ToArray();
            if (members.Length > rule.Maximum || members.Length > 0 &&
                (rule.LevelSpread > 0 && members.Max(m => m.Level) - members.Min(m => m.Level) > rule.LevelSpread || !compatible(members))) return false;
            if (members.Length >= rule.Target && PlanShips(rule, members) is { } planned)
            { result = chosen.ToArray(); assignments = planned; return true; }
            if (members.Length >= rule.Maximum) return false;
            for (var next = index; next < candidates.Length && budget > 0; next++)
            {
                chosen.Add(candidates[next]);
                if (Search(next + 1)) return true;
                chosen.RemoveAt(chosen.Count - 1);
            }
            return false;
        }
        if (!Search(0) || result == null || assignments == null) return null;
        var proposal = new MatchProposal { Rule = rule, Tickets = result, Ships = assignments, Deadline = now + readySeconds * 1000L };
        Proposals.Add(proposal.Id, proposal);
        foreach (var ticket in result) _reserved.Add(ticket.Id, proposal.Id);
        return proposal;
    }

    internal static Dictionary<Guid, Guid>? PlanShips(MatchRule rule, IReadOnlyList<MatchMember> members)
    {
        if (rule.MinimumShips == 0) return members.All(m => m.ShipId == Guid.Empty) ? [] : null;
        var captains = members.Where(m => m.Captain).ToArray();
        if (captains.Length < rule.MinimumShips || captains.Length > rule.MaximumShips ||
            captains.Select(m => m.MapId).Distinct().Count() != 1 ||
            captains.Select(m => m.ShipId).Distinct().Count() != captains.Length) return null;
        Dictionary<Guid, Guid> assignments = [];
        var capacity = captains.ToDictionary(c => c.ShipId, c => c.ShipCapacity);
        foreach (var member in members.Where(m => m.ShipId != Guid.Empty))
        {
            if (!capacity.ContainsKey(member.ShipId) || --capacity[member.ShipId] < 0 ||
                !captains.Any(c => c.Id == member.CaptainId && c.ShipId == member.ShipId)) return null;
            assignments.Add(member.Id, member.ShipId);
        }
        foreach (var member in members.Where(m => m.ShipId == Guid.Empty))
        {
            var free = captains.FirstOrDefault(c => capacity[c.ShipId] > 0);
            if (free == null) return null;
            capacity[free.ShipId]--;
            assignments.Add(member.Id, free.ShipId);
        }
        return assignments;
    }

    internal bool Confirm(Guid player, Guid proposalId, long now)
    {
        var proposal = ProposalFor(player);
        return proposal != null && proposal.Id == proposalId && !proposal.Launching && now < proposal.Deadline && proposal.Ready.Add(player);
    }

    internal bool TryLaunch(MatchProposal proposal, long now)
    {
        if (!Proposals.ContainsKey(proposal.Id) || proposal.Launching || now >= proposal.Deadline ||
            !proposal.Members.All(m => proposal.Ready.Contains(m.Id))) return false;
        proposal.Launching = true;
        return true;
    }

    internal MatchTicket[] Cancel(Guid player)
    {
        if (TicketFor(player) is not { } ticket) return [];
        if (ProposalFor(player) is { } proposal)
        {
            if (proposal.Launching) return [];
            Release(proposal);
        }
        Remove(ticket);
        return [ticket];
    }

    internal MatchTicket[] Timeout(MatchProposal proposal)
    {
        if (proposal.Launching || !Proposals.ContainsKey(proposal.Id)) return [];
        var absent = proposal.Tickets.Where(t => t.Members.Any(m => !proposal.Ready.Contains(m.Id))).ToArray();
        Release(proposal);
        foreach (var ticket in absent) Remove(ticket);
        return absent;
    }

    internal void Complete(MatchProposal proposal)
    {
        Release(proposal);
        foreach (var ticket in proposal.Tickets) Remove(ticket);
    }

    internal void Clear()
    { Tickets.Clear(); Proposals.Clear(); _players.Clear(); _reserved.Clear(); }

    private void Release(MatchProposal proposal)
    {
        Proposals.Remove(proposal.Id);
        foreach (var ticket in proposal.Tickets) _reserved.Remove(ticket.Id);
    }
    private void Remove(MatchTicket ticket)
    {
        Tickets.Remove(ticket.Id);
        foreach (var member in ticket.Members) _players.Remove(member.Id);
    }
}
