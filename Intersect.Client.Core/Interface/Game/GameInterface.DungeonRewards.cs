using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private DungeonRewardNotificationWindow? _dungeonRewardNotificationWindow;

    public void ShowDungeonRewardNotification(DungeonRewardNotificationPacket packet)
    {
        _dungeonRewardNotificationWindow ??= new DungeonRewardNotificationWindow(GameCanvas);
        _dungeonRewardNotificationWindow.Enqueue(packet);
    }

    public void UpdateDungeonRewardNotifications()
    {
        _dungeonRewardNotificationWindow?.Update();
    }
}
