namespace Intersect.Server.GuildHeadquarters;

internal sealed class HeadquartersInvitations
{
    internal sealed record Invitation(Guid Host, Guid Guest, Guid Guild, long Until);
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Invitation> _pending = [];
    internal Guid Create(Guid host, Guid guest, Guid guild, long now)
    {
        lock (_gate)
        {
            foreach (var key in _pending.Where(p => p.Value.Until <= now || p.Value.Guest == guest).Select(p => p.Key).ToArray()) _pending.Remove(key);
            if (_pending.Count >= 2000 || _pending.Values.Count(p => p.Guild == guild) >= 20) return Guid.Empty;
            var id = Guid.NewGuid(); _pending[id] = new(host, guest, guild, now + 120000); return id;
        }
    }
    internal Invitation? Take(Guid id, Guid guest, long now)
    {
        lock (_gate)
        {
            if (!_pending.TryGetValue(id, out var invitation) || invitation.Guest != guest) return null;
            _pending.Remove(id);
            return invitation.Until > now ? invitation : null;
        }
    }
    internal void RemovePlayer(Guid player)
    {
        lock (_gate)
            foreach (var id in _pending.Where(p => p.Value.Host == player || p.Value.Guest == player).Select(p => p.Key).ToArray()) _pending.Remove(id);
    }
    internal void RemoveGuild(Guid guild)
    {
        lock (_gate)
            foreach (var id in _pending.Where(p => p.Value.Guild == guild).Select(p => p.Key).ToArray()) _pending.Remove(id);
    }
}
