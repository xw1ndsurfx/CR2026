using Intersect.Network.Packets.WorldEvents;

namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private readonly Dictionary<Guid, WorldBossStatusPacket> _worldBossStatuses = [];
    private readonly Dictionary<Guid, long> _finishedWorldBossNpcs = [];
    private WorldBossStatusWindow? _worldBossStatusWindow;
    private WorldBossResultWindow? _worldBossResultWindow;

    public void UpdateWorldBossStatus(WorldBossStatusPacket packet)
    {
        if (packet.Active && packet.NpcId != Guid.Empty && _finishedWorldBossNpcs.ContainsKey(packet.NpcId))
            return;
        if (!packet.Active && packet.NpcId != Guid.Empty)
            _finishedWorldBossNpcs[packet.NpcId] = Environment.TickCount64 + 600_000;
        if (packet.Active)
            _worldBossStatuses[packet.BossId] = packet;
        else
            _worldBossStatuses.Remove(packet.BossId);

        _worldBossStatusWindow ??= new WorldBossStatusWindow(GameCanvas);
        RefreshWorldBossUi();
    }

    public void OnWorldBossNpcDied(Guid npcId)
    {
        var finished = _worldBossStatuses.Where(pair => pair.Value.NpcId == npcId && npcId != Guid.Empty).Select(pair => pair.Key).ToArray();
        if (finished.Length == 0) return;
        _finishedWorldBossNpcs[npcId] = Environment.TickCount64 + 600_000;
        foreach (var bossId in finished) _worldBossStatuses.Remove(bossId);
        RefreshWorldBossUi();
    }

    private void RefreshWorldBossUi()
    {
        if (_worldBossStatusWindow == null)
            return;

        if (_worldBossStatuses.Count == 0)
        {
            _worldBossStatusWindow.Hide();
            return;
        }

        // Prefer the boss the local player is helping to defeat.
        var next = _worldBossStatuses.Values
            .OrderByDescending(state => state.YourDamage > 0)
            .ThenBy(state => state.ExpiresAtUnixMilliseconds)
            .First();
        _worldBossStatusWindow.Apply(next, _worldBossStatuses.Count);
    }

    public void UpdateWorldBossUi()
    {
        foreach (var id in _finishedWorldBossNpcs.Where(pair => pair.Value <= Environment.TickCount64).Select(pair => pair.Key).ToArray())
            _finishedWorldBossNpcs.Remove(id);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var expired = _worldBossStatuses.Where(pair => pair.Value.Health <= 0 ||
            pair.Value.ExpiresAtUnixMilliseconds > 0 && pair.Value.ExpiresAtUnixMilliseconds <= now)
            .Select(pair => pair.Key).ToArray();
        foreach (var id in expired) _worldBossStatuses.Remove(id);
        if (expired.Length > 0) RefreshWorldBossUi();
        _worldBossStatusWindow?.Update();
    }

    public void ShowWorldBossResult(WorldBossResultPacket packet)
    {
        if (_worldBossStatuses.TryGetValue(packet.BossId, out var status) && status.NpcId != Guid.Empty)
            _finishedWorldBossNpcs[status.NpcId] = Environment.TickCount64 + 600_000;
        _worldBossStatuses.Remove(packet.BossId);
        RefreshWorldBossUi();
        _worldBossResultWindow ??= new WorldBossResultWindow(GameCanvas);
        _worldBossResultWindow.Apply(packet);
    }
}
