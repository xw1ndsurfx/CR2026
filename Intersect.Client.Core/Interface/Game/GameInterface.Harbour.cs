using Intersect.Network.Packets.Server;
namespace Intersect.Client.Interface.Game;
public partial class GameInterface
{
    private HarbourWindow? _harbourWindow;
    public bool IsHarbourVisible => _harbourWindow is { IsHidden: false };
    public void HideHarbour() => _harbourWindow?.Hide();
    public void ApplyHarbourState(HarbourStatePacket packet)
    {
        if (packet.Close) { _harbourWindow?.Hide(); return; }
        GameMenu?.HideWindows();
        _harbourWindow ??= new HarbourWindow(GameCanvas);
        _harbourWindow.Apply(packet); _harbourWindow.Show(); _harbourWindow.BringToFront();
    }
}
