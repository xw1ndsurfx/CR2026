using Intersect.Network.Packets.Metalworking;
namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private MetalworkWindow? _metalworkWindow;
    public void ApplyMetalwork(MetalworkStatePacket packet)
    {
        _metalworkWindow ??= new MetalworkWindow(GameCanvas);
        _metalworkWindow.Apply(packet);
    }
}
