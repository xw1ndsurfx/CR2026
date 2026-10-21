using Intersect.Network.Packets.Server;
namespace Intersect.Client.Interface.Game;
public partial class GameInterface
{
    private PremiumConvenienceWindow? _premiumConvenience;
    public void ApplyPremiumConvenience(PremiumConvenienceStatePacket packet)
    {
        _premiumConvenience??=new PremiumConvenienceWindow(GameCanvas);
        _premiumConvenience.Apply(packet);
    }
}
