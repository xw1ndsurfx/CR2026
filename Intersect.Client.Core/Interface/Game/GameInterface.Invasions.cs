using Intersect.Network.Packets.WorldEvents;

namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private InvasionStatusWindow? _invasionStatusWindow;
    private InvasionResultWindow? _invasionResultWindow;

    public void UpdateInvasionStatus(InvasionStatusPacket packet)
    {
        _invasionStatusWindow ??= new InvasionStatusWindow(GameCanvas);
        _invasionStatusWindow.Apply(packet);
    }

    public void ShowInvasionResult(InvasionResultPacket packet)
    {
        _invasionResultWindow ??= new InvasionResultWindow(GameCanvas);
        GameMenu?.HideWindows();
        _invasionResultWindow.Apply(packet);
    }
}
