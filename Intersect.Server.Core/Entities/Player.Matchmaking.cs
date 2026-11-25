using Intersect.Server.Networking;
using System.Collections.Concurrent;

namespace Intersect.Server.Entities;

public partial class Player
{
    [System.ComponentModel.DataAnnotations.Schema.NotMapped, Newtonsoft.Json.JsonIgnore]
    internal Guid MatchmakingSessionId { get; } = Guid.NewGuid();
    private static readonly ConcurrentDictionary<Guid, byte> MatchmakingDepartures = new();
    internal bool MatchmakingDeparting => MatchmakingDepartures.ContainsKey(Id) || Party.Any(p => MatchmakingDepartures.ContainsKey(p.Id));

    // TryEnter avoids reversing the order of entity/party locks used by other activities.
    internal static bool TryFormMatchedParty(Player[] players, IReadOnlyList<Matchmaking.MatchTicket> tickets,
        out Dictionary<Player, List<Player>> previous)
    {
        previous = [];
        if (!Monitor.TryEnter(PartyMembershipLock)) return false;
        try
        {
            foreach (var ticket in tickets)
            {
                var ids = ticket.Members.Select(m => m.Id).ToArray();
                foreach (var player in players.Where(p => ids.Contains(p.Id)))
                    if (!(player.Party.Count == 0 ? new[] { player.Id } : player.Party.Select(p => p.Id)).SequenceEqual(ids) || player.MatchmakingDeparting) return false;
            }
            foreach (var player in players) { previous[player] = player.Party.ToList(); MatchmakingDepartures[player.Id] = 0; }
            foreach (var player in players) player.Party = players.ToList();
            return true;
        }
        finally { Monitor.Exit(PartyMembershipLock); }
    }

    internal static void FinishMatchedParty(Dictionary<Player, List<Player>> previous, bool success)
    {
        lock (PartyMembershipLock)
        {
            foreach (var pair in previous)
            {
                if (!success) pair.Key.Party = pair.Value;
                MatchmakingDepartures.TryRemove(pair.Key.Id, out _);
                PacketSender.SendParty(pair.Key);
            }
        }
    }
}
