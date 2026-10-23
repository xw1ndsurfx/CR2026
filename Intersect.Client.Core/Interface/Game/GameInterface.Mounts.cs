using Intersect.Network.Packets.Server;
namespace Intersect.Client.Interface.Game;
public partial class GameInterface
{
    private MountsWindow? _mountsWindow;
    internal void ApplyMountState(MountStatePacket packet)
    {
        if (_mountsWindow == null && !packet.OpenWindow) return;
        _mountsWindow ??= new MountsWindow(GameCanvas);
        _mountsWindow.Apply(packet);
        if (packet.OpenWindow) { GameMenu?.HideWindows(); _mountsWindow.Show(); _mountsWindow.BringToFront(); }
    }
}
