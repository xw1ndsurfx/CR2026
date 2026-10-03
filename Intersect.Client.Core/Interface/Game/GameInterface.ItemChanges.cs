using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private ItemChangeNotificationWindow? _itemChangeNotificationWindow;

    public void ShowItemChangeNotification(ItemChangeNotificationPacket packet)
    {
        _itemChangeNotificationWindow ??= new ItemChangeNotificationWindow(GameCanvas);
        _itemChangeNotificationWindow.Enqueue(packet);
    }

    public void UpdateItemChangeNotifications()
    {
        _itemChangeNotificationWindow?.Update();
    }
}
