using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private PlayerProfileWindow? _playerProfileWindow;

    public void ShowPlayerProfile(PlayerProfilePacket packet)
    {
        if (!packet.Found)
            return;

        GameMenu?.HideWindows();
        _playerProfileWindow ??= new PlayerProfileWindow(GameCanvas);
        _playerProfileWindow.Apply(packet);
    }

    public void ApplyOwnPlayerProfile(PlayerProfilePacket packet)
    {
        GameMenu?.ApplyOwnPlayerProfile(packet);
    }
}
