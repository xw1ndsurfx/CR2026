using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private AchievementsWindow? _achievementsWindow;
    private AchievementsWindow? _observedAchievementsWindow;

    public void ApplyAchievementState(AchievementStatePacket packet)
    {
        if (packet.InspectedPlayerId != Guid.Empty)
        {
            if (!packet.OpenWindow)
                return;
            _observedAchievementsWindow ??= new AchievementsWindow(GameCanvas, readOnly: true);
            _observedAchievementsWindow.Apply(packet);
            _observedAchievementsWindow.ShowWindow();
            return;
        }

        if (_achievementsWindow != null)
            _achievementsWindow.Apply(packet);

        if (!packet.OpenWindow)
            return;

        GameMenu?.HideWindows();
        _achievementsWindow ??= new AchievementsWindow(GameCanvas);
        _achievementsWindow.Apply(packet);
        _achievementsWindow.ShowWindow();
    }
}
