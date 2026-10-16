using Intersect.Client.General;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private ItemChangeNotificationWindow? _itemChangeNotificationWindow;
    private DayPhaseHud? _dayPhaseHud;
    private readonly DayPhaseNotificationTracker _dayPhaseTracker = new();

    public void ShowItemChangeNotification(ItemChangeNotificationPacket packet)
    {
        _itemChangeNotificationWindow ??= new ItemChangeNotificationWindow(GameCanvas);
        _itemChangeNotificationWindow.Enqueue(packet);
    }

    public void UpdateItemChangeNotifications()
    {
        _dayPhaseHud ??= new DayPhaseHud(GameCanvas);
        _dayPhaseHud.UpdatePhase(Intersect.Client.General.Time.CurrentPhase,
            Globals.Me != null && Intersect.Client.General.Time.HasServerTime);
        if (Intersect.Client.General.Time.HasServerTime && _dayPhaseTracker.Observe(Intersect.Client.General.Time.CurrentPhase))
        {
            _itemChangeNotificationWindow ??= new ItemChangeNotificationWindow(GameCanvas);
            _itemChangeNotificationWindow.EnqueuePhase(Intersect.Client.General.Time.CurrentPhase);
        }
        _itemChangeNotificationWindow?.Update();
    }
}
