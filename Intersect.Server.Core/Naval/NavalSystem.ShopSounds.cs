using Intersect.GameObjects;
using Intersect.Server.Entities;
using Intersect.Server.Networking;
namespace Intersect.Server.Naval;
internal static partial class NavalSystem
{
    private static void PlayShopTradeSound(Player player,bool selling)
    {
        var shop=ShopDescriptor.Get(player.NavalShopId);
        var sound=selling?shop?.SellSound:shop?.BuySound;
        if(!string.IsNullOrWhiteSpace(sound) && !string.Equals(sound,"None",StringComparison.OrdinalIgnoreCase))
            PacketSender.SendPlaySound(player,sound);
    }
}
