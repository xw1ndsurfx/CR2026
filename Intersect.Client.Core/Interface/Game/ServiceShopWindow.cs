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
    private ShopServiceState? _state;
    private readonly ShopDescriptor _pricing = new();
    private DateTimeOffset _nextPromotionChange = DateTimeOffset.MaxValue;
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
        _state=state;_pricing.ServiceOffers=state.Rows.Select(row=>row.Offer).ToArray();
        _pricing.SynchronizePricingTime(state.ServerTimeUnixMilliseconds);
        Title=ClientLanguages.ContentText(state.Name);
        _status.Text=T(state.Message);
        RefreshOffers();Show();BringToFront();
    }
    internal void Update()
    {
        if(!IsHidden && _state!=null && _pricing.PricingTime>=_nextPromotionChange)RefreshOffers();
    }
    private void RefreshOffers()
    {
        if(_state is not {} state)return;
        var now=_pricing.PricingTime;_nextPromotionChange=_pricing.NextPromotionChange(now);
        _list.DeleteAll();var y=0;
        foreach(var entry in state.Rows)
        {
            var offer=entry.Offer;
            var detail=state.Music?T("Music for everyone in this zone")+" · "+T("Premium required"):
                string.Format(T("Required level: {0}"),offer.RequiredLevel);
            var price=_pricing.ServicePrice(offer,now);
            var discount=ShopPromotion.EffectiveDiscount(now,offer.Promotion);
            var enabled=entry.Available && !entry.Owned && entry.Balance>=price;
            if(state.Music)enabled=entry.Available && entry.Balance>=price;
            var row=ShopProductRow.Service(_list,"Service"+offer.Id,ClientLanguages.ContentText(offer.Name),detail,
                $"{price} {entry.Currency}"+(discount>0?$" (-{discount}%)":""),T(state.Music?"Play music":entry.Owned?"Owned":"Learn spell"),enabled,
                ()=>Networking.Network.SendPacket(new ServiceShopPurchasePacket{OfferId=offer.Id,ExpectedUnitPrice=price}),offer.Icon);
            row.SetBounds(0,y,700,75);y+=76;
        }
        _list.SetInnerSize(700,y);_list.UpdateScrollBars();
    }
}
