using Intersect.Network.Packets.Server;
namespace Intersect.Client.Interface.Game;
public partial class GameInterface
{
    private JoustWindow? _joustWindow;
    internal void ApplyJoustState(JoustStatePacket packet)
    {
        if (_joustWindow == null && !packet.Open) return;
        _joustWindow ??= new JoustWindow(GameCanvas);
        _joustWindow.Apply(packet);
        if (packet.Open) { _joustWindow.Show(); _joustWindow.BringToFront(); }
    }
}
