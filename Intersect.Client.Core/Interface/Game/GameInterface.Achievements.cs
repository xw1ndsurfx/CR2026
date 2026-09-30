using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private AchievementsWindow? _achievementsWindow;

    public void ApplyAchievementState(AchievementStatePacket packet)
    {
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
