using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Client.Interface.Game.Shop;
using Intersect.GameObjects;
using Intersect.Network.Packets.Client;
namespace Intersect.Client.Interface.Game;
internal sealed class ServiceShopWindow:Window
{
    private readonly ScrollControl _list;
    private readonly Label _status;
    internal ServiceShopWindow(Canvas canvas):base(canvas,"",false,"ServiceShopWindow")
    {
        SetSize(780,620);Alignment=[Alignments.Center];DeleteOnClose=false;DisableResizing();
        var pane=new ShopWindow.MerchantPanelChrome(this,"ServicesPane");pane.SetBounds(16,48,745,510);
        _list=new ScrollControl(this,"ServicesList"){OverflowX=OverflowBehavior.Hidden,OverflowY=OverflowBehavior.Scroll,AutoHideBars=false};
        _list.SetBounds(24,84,722,466);ShopWindow.ConfigureNativeScrollbar(_list);
        _status=NavalManagementWindow.Text(this,"ServiceStatus","",24,565,600,28);
        NavalManagementWindow.Button(this,"CloseServiceShop","Close",640,565,110,30,CloseShop);
        Closed+=(_,_)=>CloseShop();
    }
    protected override void EnsureInitialized(){}
    private static string T(string source)=>Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get(source,ClientLanguages.Current);
    private void CloseShop(){Hide();Networking.Network.SendPacket(new ServiceShopPurchasePacket{Close=true});}
    internal void Apply(ShopServiceState state)
    {
        Title=ClientLanguages.ContentText(state.Name);
        _status.Text=T(state.Message);
        _list.DeleteAll();var y=0;
        foreach(var entry in state.Rows)
        {
            var offer=entry.Offer;
            var detail=state.Music?T("Music for everyone in this zone")+" · "+T("Premium required"):
                string.Format(T("Required level: {0}"),offer.RequiredLevel);
            var enabled=entry.Available && !entry.Owned && entry.Balance>=offer.Price;
            if(state.Music)enabled=entry.Available && entry.Balance>=offer.Price;
            var row=ShopProductRow.Service(_list,"Service"+offer.Id,ClientLanguages.ContentText(offer.Name),detail,
                $"{offer.Price} {entry.Currency}",T(state.Music?"Play music":entry.Owned?"Owned":"Learn spell"),enabled,
                ()=>Networking.Network.SendPacket(new ServiceShopPurchasePacket{OfferId=offer.Id}),offer.Icon);
            row.SetBounds(0,y,700,75);y+=76;
        }
        _list.SetInnerSize(700,y);_list.UpdateScrollBars();Show();BringToFront();
    }
}
