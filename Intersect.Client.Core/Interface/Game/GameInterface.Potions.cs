using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private PotionWindow? _potionWindow;
    private Guid _potionSessionId;

    internal void OpenPotionMiniGame(PotionMiniGamePacket packet)
    {
        if (!packet.IsValid) return;

        GameMenu?.HideWindows();
        ClosePotionWindow();

        _potionSessionId = packet.SessionId;
        _potionWindow = new PotionWindow(GameCanvas, packet.Seed, packet.Title);
        _potionWindow.BringToFront();
    }

    private void UpdatePotions()
    {
        if (_potionWindow == null) return;
        if (_potionWindow.ExitRequested)
        {
            ClosePotionWindow();
            return;
        }

        _potionWindow.Update();
    }

    private bool ClosePotionWindow()
    {
        if (_potionWindow == null) return false;
        _potionWindow.Destroy();
        _potionWindow = null;
        _potionSessionId = Guid.Empty;
        return true;
    }

    private void DisposePotions()
    {
        _potionWindow?.Destroy();
        _potionWindow = null;
        _potionSessionId = Guid.Empty;
    }
}
