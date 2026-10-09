using Intersect.Network.Packets.WorldEvents;

namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private readonly Dictionary<Guid, WorldBossStatusPacket> _worldBossStatuses = [];
    private WorldBossStatusWindow? _worldBossStatusWindow;
    private WorldBossResultWindow? _worldBossResultWindow;

    public void UpdateWorldBossStatus(WorldBossStatusPacket packet)
    {
        if (packet.Active)
            _worldBossStatuses[packet.BossId] = packet;
        else
            _worldBossStatuses.Remove(packet.BossId);

        _worldBossStatusWindow ??= new WorldBossStatusWindow(GameCanvas);
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
        _worldBossStatusWindow?.Update();
    }

    public void ShowWorldBossResult(WorldBossResultPacket packet)
    {
        _worldBossResultWindow ??= new WorldBossResultWindow(GameCanvas);
        _worldBossResultWindow.Apply(packet);
    }
}
