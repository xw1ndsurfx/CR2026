using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private LogiCoinShopWindow? _logiCoinShopWindow;

    public bool IsLogiCoinShopVisible => _logiCoinShopWindow is { IsHidden: false };

    public void ToggleLogiCoinShop()
    {
        if (_logiCoinShopWindow is { IsHidden: false })
        {
            _logiCoinShopWindow.Hide();
            return;
        }

        OpenLogiCoinShop();
    }

    public void OpenLogiCoinShop()
    {
        GameMenu?.HideWindows();
        _logiCoinShopWindow ??= new LogiCoinShopWindow(GameCanvas);
        _logiCoinShopWindow.ShowAndRequest();
    }

    public void HideLogiCoinShop()
    {
        _logiCoinShopWindow?.Hide();
    }

    public void ApplyLogiCoinShopState(LogiCoinShopStatePacket packet)
    {
        _logiCoinShopWindow ??= new LogiCoinShopWindow(GameCanvas);
        _logiCoinShopWindow.Apply(packet);
        _logiCoinShopWindow.Show();
        _logiCoinShopWindow.BringToFront();
    }
}
