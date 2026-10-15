using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private ItemChangeNotificationWindow? _itemChangeNotificationWindow;
    private readonly DayPhaseNotificationTracker _dayPhaseTracker = new();

    public void ShowItemChangeNotification(ItemChangeNotificationPacket packet)
    {
        _itemChangeNotificationWindow ??= new ItemChangeNotificationWindow(GameCanvas);
        _itemChangeNotificationWindow.Enqueue(packet);
    }

    public void UpdateItemChangeNotifications()
    {
        if (Intersect.Client.General.Time.HasServerTime && _dayPhaseTracker.Observe(Intersect.Client.General.Time.CurrentPhase))
        {
            _itemChangeNotificationWindow ??= new ItemChangeNotificationWindow(GameCanvas);
            _itemChangeNotificationWindow.EnqueuePhase(Intersect.Client.General.Time.CurrentPhase);
        }
        _itemChangeNotificationWindow?.Update();
    }
}
