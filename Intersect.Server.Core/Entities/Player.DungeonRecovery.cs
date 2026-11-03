using Intersect.Server.Networking;

namespace Intersect.Server.Entities;

public partial class Player
{
    internal void LeavePartyForDungeonDisconnect()
    {
        lock (PartyMembershipLock) LeavePartyCore(preserveDungeon: true);
    }

    // Candidates are authenticated members of the same active run, selected by the
    // server. Preserve any new unrelated party rather than silently moving its members.
    internal bool RestoreDungeonParty(Player[] candidates)
    {
        lock (PartyMembershipLock)
        {
            var allowed = candidates.Select(p => p.Id).Append(Id).ToHashSet();
            if (Party.Any(member => !allowed.Contains(member.Id))) return false;
            var anchor = candidates.FirstOrDefault(member => member.Party.All(p => allowed.Contains(p.Id)));
            if (anchor == null) return false;
            var members = anchor.Party.Append(anchor).Where(member => member.Id != Id).DistinctBy(member => member.Id).ToList();
            var maximum = Math.Min(Options.Instance.Party.MaximumMembers,
                Intersect.Server.PartyLobbies.PartyLobbyRuntime.MaximumMembersForHost(members[0].Id));
            if (members.Count >= maximum) return false;
            members.Add(this);
            foreach (var member in members)
            {
                member.Party = members;
                PacketSender.SendParty(member);
            }
            return true;
        }
    }
}
