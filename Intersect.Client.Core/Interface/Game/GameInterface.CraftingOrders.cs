using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private CraftingOrdersWindow? _craftingOrdersWindow;

    public void ApplyCraftingOrdersState(CraftingOrdersStatePacket packet)
    {
        if (!packet.OpenWindow && (_craftingOrdersWindow == null || _craftingOrdersWindow.IsHidden))
            return;
        if (packet.OpenWindow) GameMenu?.HideWindows();
        _craftingOrdersWindow ??= new CraftingOrdersWindow(GameCanvas);
        _craftingOrdersWindow.Apply(packet);
        if (packet.OpenWindow)
        {
            _craftingOrdersWindow.Show();
            _craftingOrdersWindow.BringToFront();
        }
    }

    public void HideCraftingOrders() => _craftingOrdersWindow?.Hide();
}
