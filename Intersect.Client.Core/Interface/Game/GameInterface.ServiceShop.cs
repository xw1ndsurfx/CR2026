using Intersect.GameObjects;
using Newtonsoft.Json;
namespace Intersect.Client.Interface.Game;
public partial class GameInterface
{
    private ServiceShopWindow? _serviceShop;
    public void ApplyServiceShop(string json)
    {
        var state=JsonConvert.DeserializeObject<ShopServiceState>(json);
        if(state==null)return;
        _serviceShop??=new ServiceShopWindow(GameCanvas);_serviceShop.Apply(state);
    }
}
