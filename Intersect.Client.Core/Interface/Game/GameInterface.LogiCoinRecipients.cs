using Intersect.Network.Packets.Server;
namespace Intersect.Client.Interface.Game;
public partial class GameInterface
{
    public void ApplyLogiCoinRecipient(LogiCoinRecipientPacket packet)=>_logiCoinShopWindow?.ApplyRecipient(packet);
}
