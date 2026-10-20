using Intersect.Network.Packets.Server;
using Intersect.Framework.Core.Naval;
namespace Intersect.Client.Interface.Game;
public partial class GameInterface
{
    private HarbourWindow? _harbourWindow, _shipShopWindow;
    public bool IsHarbourVisible => _harbourWindow is { IsHidden: false } || _shipShopWindow is { IsHidden: false };
    public void HideHarbour() { _harbourWindow?.Hide(); _shipShopWindow?.Hide(); }
    public void ApplyHarbourState(HarbourStatePacket packet)
    {
        if (packet.View is not (HarbourView.Fleet or HarbourView.Shop)) return;
        if (packet.Close) { if (packet.View == HarbourView.Shop) _shipShopWindow?.Hide(); else _harbourWindow?.Hide(); return; }
        GameMenu?.HideWindows();
        if (packet.View == HarbourView.Shop)
        {
            _harbourWindow?.Hide();
            _shipShopWindow ??= new HarbourWindow(GameCanvas, HarbourView.Shop);
            _shipShopWindow.Apply(packet); _shipShopWindow.Show(); _shipShopWindow.BringToFront();
        }
        else
        {
            _shipShopWindow?.Hide();
            _harbourWindow ??= new HarbourWindow(GameCanvas, HarbourView.Fleet);
            _harbourWindow.Apply(packet); _harbourWindow.Show(); _harbourWindow.BringToFront();
        }
    }
}
