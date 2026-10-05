using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private RoyalStylistWindow? _royalStylistWindow;

    public void ApplyRoyalStylistState(
        RoyalStylistStatePacket packet
    )
    {
        GameMenu?.HideWindows();
        _royalStylistWindow ??=
            new RoyalStylistWindow(GameCanvas);

        _royalStylistWindow.Apply(packet);
        _royalStylistWindow.Show();
        _royalStylistWindow.BringToFront();
    }

    public void HideRoyalStylist()
    {
        _royalStylistWindow?.Hide();
    }
}
