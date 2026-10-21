using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game.Shop;
using Intersect.Client.Localization;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.Naval;
namespace Intersect.Client.Interface.Game;
internal sealed class NavalAmmoShopWindow:Window
{
    private readonly ScrollControl _buy,_sell;
    private readonly Label _message;
    internal NavalAmmoShopWindow(Canvas parent):base(parent,NavalManagementWindow.T("Naval ammunition shop"),false,nameof(NavalAmmoShopWindow))
    {
        SetSize(780,620);Alignment=[Alignments.Center];DeleteOnClose=false;DisableResizing();
        foreach(var (x,name) in new[]{(16,"Buy"),(398,"Sell")}) {var pane=new ShopWindow.MerchantPanelChrome(this,name+"Pane");pane.SetBounds(x,48,363,512);pane.SendToBack();}
        NavalManagementWindow.Text(this,"BuyHeader",Strings.Shop.BuyItem,24,52,340,28,14);
        NavalManagementWindow.Text(this,"SellHeader",Strings.Shop.SellItem,406,52,340,28,14);
        _buy=List("BuyList",24);_sell=List("SellList",406);
        _message=NavalManagementWindow.Text(this,"Status","",24,570,590,26);
        NavalManagementWindow.Button(this,"CloseAmmoShop","Close",640,565,110,32,Hide);Hide();
    }
    protected override void EnsureInitialized() { }
    private ScrollControl List(string name,int x)
    {var list=new ScrollControl(this,name) {OverflowX=OverflowBehavior.Hidden,OverflowY=OverflowBehavior.Scroll,AutoHideBars=false};list.SetBounds(x,90,350,468);ShopWindow.ConfigureNativeScrollbar(list);return list;}
    internal void Apply(NavalManagementState state)
    {
        _message.Text=NavalManagementWindow.T(state.Message);_buy.DeleteAllChildren();_sell.DeleteAllChildren();var by=0;var sy=0;
        foreach(var ammo in state.Ammo)
        {
            if(ammo.Quantity<0 || ItemDescriptor.Get(ammo.ItemId) is not {} item)continue;
            if(ammo.ForSale) {var row=ShopProductRow.Buy(_buy,by,item,ammo.Price,ammo.Currency,ammo.CanBuy,()=>NavalManagementWindow.Request(NavalManageAction.BuyAmmo,ammo.Kind),nativeButton:true);row.SetBounds(0,by,332,75);by+=76;}
            if(ammo.SellPrice>0 && ammo.Quantity+ (long)ammo.MainQuantity>0) {var row=ShopProductRow.Sell(_sell,sy,item,(int)Math.Min(int.MaxValue,ammo.Quantity+(long)ammo.MainQuantity),ammo.SellPrice,ammo.SellCurrency,()=>NavalManagementWindow.Request(NavalManageAction.SellAmmo,ammo.Kind),nativeButton:true);row.SetBounds(0,sy,332,75);sy+=76;}
        }
        _buy.UpdateScrollBars();_sell.UpdateScrollBars();Show();BringToFront();
    }
}
