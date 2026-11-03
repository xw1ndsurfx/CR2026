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
    private readonly Label _empty, _heading;
    private readonly ShopCatalogControls _catalog;
    private ShopServiceState? _state;
    private readonly ShopDescriptor _pricing = new();
    private DateTimeOffset _nextPromotionChange = DateTimeOffset.MaxValue;
    internal ServiceShopWindow(Canvas canvas):base(canvas,"",false,"ServiceShopWindow")
    {
        SetSize(780,620);Alignment=[Alignments.Center];DeleteOnClose=false;DisableResizing();
        var pane=new ShopWindow.MerchantPanelChrome(this,"ServicesPane");pane.SetBounds(16,48,745,510);
        _heading=NavalManagementWindow.Text(this,"CatalogHeading","",32,52,700,26,14);
        _list=new ScrollControl(this,"ServicesList"){OverflowX=OverflowBehavior.Hidden,OverflowY=OverflowBehavior.Scroll,AutoHideBars=false};
        _list.SetBounds(24,84,722,466);ShopWindow.ConfigureNativeScrollbar(_list);
        _status=NavalManagementWindow.Text(this,"ServiceStatus","",24,565,600,28);
        _empty=NavalManagementWindow.Text(this,"CatalogEmpty",T("No products match your filters."),32,100,680,30);
        _catalog=new ShopCatalogControls(this,()=>{Rebuild();_list.ScrollToTop();});
        NavalManagementWindow.Button(this,"CloseServiceShop","Close",640,565,110,30,CloseShop);
        Closed+=(_,_)=>CloseShop();
    }
    protected override void EnsureInitialized(){}
    private static string T(string source)=>Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get(source,ClientLanguages.Current);
    private void CloseShop(){Hide();Networking.Network.SendPacket(new ServiceShopPurchasePacket{Close=true});}
    internal void Apply(ShopServiceState state)
    {
        _state=state;
        _pricing.ServiceOffers=state.Rows.Select(row=>row.Offer).ToArray();
        _pricing.SynchronizePricingTime(state.ServerTimeUnixMilliseconds);
        Title=ClientLanguages.ContentText(state.Name);
        _status.Text=T(state.Message);
        Rebuild();Show();BringToFront();
    }
    internal void Update()
    {
        if (!IsHidden && _state != null && _pricing.PricingTime >= _nextPromotionChange) Rebuild();
    }
    private void Rebuild()
    {
        if (_state is not {} state) return;
        var now = _pricing.PricingTime;
        _nextPromotionChange = _pricing.NextPromotionChange(now);
        _list.DeleteAll();
        var entries=_catalog.Browse(state.Rows,e=>e.Offer.Name,e=>e.Offer.RequiredLevel,e=>_pricing.ServicePrice(e.Offer,now),
            e=>e.Available && (state.Music || !e.Owned) && e.Balance>=_pricing.ServicePrice(e.Offer,now),e=>e.Owned).ToArray();
        _heading.Text=string.Format(T("Products: {0} / {1}"),entries.Length,state.Rows.Length);
        _empty.IsHidden=entries.Length>0;
        for(var index=0;index<entries.Length;index++)
        {
            var entry=entries[index];
            var offer=entry.Offer;
            var detail=state.Music?T("Music for everyone in this zone")+" · "+T("Premium required"):
                string.Format(T("Required level: {0}"),offer.RequiredLevel);
            var price = _pricing.ServicePrice(offer,now);
            var discount = ShopPromotion.EffectiveDiscount(now,offer.Promotion);
            var enabled=entry.Available && !entry.Owned && entry.Balance>=price;
            if(state.Music)enabled=entry.Available && entry.Balance>=price;
            var row=ShopProductRow.Service(_list,"Service"+offer.Id,ClientLanguages.ContentText(offer.Name),detail,
                $"{price} {ClientLanguages.ContentText(entry.Currency)}"+(discount>0?$" (-{discount}%)":""),T(state.Music?"Play music":entry.Owned?"Owned":"Learn spell"),enabled,
                ()=>Networking.Network.SendPacket(new ServiceShopPurchasePacket{OfferId=offer.Id,ExpectedUnitPrice=price}),string.IsNullOrWhiteSpace(offer.Icon) && !state.Music?SpellDescriptor.Get(offer.SpellId)?.Icon??"":offer.Icon,state.Music?Guid.Empty:offer.SpellId);
            row.SetBounds(index%2*360,index/2*84,332,75);
        }
        _list.SetInnerSize(700,(entries.Length+1)/2*84);_list.UpdateScrollBars();
    }
}
