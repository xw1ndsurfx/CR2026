using Intersect.Network.Packets.Server;
namespace Intersect.Client.Interface.Game;
public partial class GameInterface
{
    private JoustWindow? _joustWindow;
    private JoustDuelWindow? _joustDuelWindow;
    internal void ApplyJoustState(JoustStatePacket packet)
    {
        if (packet.DuelJson.Length > 0 || packet.OpenDuelWindow)
            (_joustDuelWindow ??= new JoustDuelWindow(GameCanvas)).Apply(packet);
        else if (packet.Message.Length > 0) _joustDuelWindow?.Apply(packet);
        if (_joustWindow == null && !packet.Open) return;
        _joustWindow ??= new JoustWindow(GameCanvas);
        _joustWindow.Apply(packet);
        if (packet.Open) { _joustWindow.Show(); _joustWindow.BringToFront(); }
    }
}
