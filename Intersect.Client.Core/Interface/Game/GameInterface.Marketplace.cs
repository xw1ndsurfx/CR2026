using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private MarketplaceWindow? _marketplaceWindow;

    public bool IsMarketplaceVisible => _marketplaceWindow is { IsHidden: false };

    public void ToggleMarketplace()
    {
        if (_marketplaceWindow is { IsHidden: false })
        {
            _marketplaceWindow.Hide();
            return;
        }

        OpenMarketplace();
    }

    public void OpenMarketplace()
    {
        GameMenu?.HideWindows();
        _marketplaceWindow ??= new MarketplaceWindow(GameCanvas);
        _marketplaceWindow.ShowAndRequest();
    }

    public void HideMarketplace()
    {
        _marketplaceWindow?.Hide();
    }

    public void ApplyMarketplaceState(MarketplaceStatePacket packet)
    {
        _marketplaceWindow ??= new MarketplaceWindow(GameCanvas);
        _marketplaceWindow.Apply(packet);
        _marketplaceWindow.Show();
        _marketplaceWindow.BringToFront();
    }
}
