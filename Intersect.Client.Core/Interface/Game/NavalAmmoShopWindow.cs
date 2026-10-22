using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game.Shop;
using Intersect.Client.Localization;
using Intersect.Client.Interface.Shared;
using Intersect.Client.Framework.Gwen.Control.EventArguments.InputSubmissionEvent;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.Naval;
namespace Intersect.Client.Interface.Game;
internal sealed class NavalAmmoShopWindow:Window
{
    private readonly ScrollControl _buy,_sell;
    private readonly Label _message;
    private readonly Button _upgrade;
    internal NavalAmmoShopWindow(Canvas parent):base(parent,NavalManagementWindow.T("Naval ammunition shop"),false,nameof(NavalAmmoShopWindow))
    {
        SetSize(780,620);Alignment=[Alignments.Center];DeleteOnClose=false;DisableResizing();
        foreach(var (x,name) in new[]{(16,"Buy"),(398,"Sell")}) {var pane=new ShopWindow.MerchantPanelChrome(this,name+"Pane");pane.SetBounds(x,48,363,512);pane.SendToBack();}
        NavalManagementWindow.Text(this,"BuyHeader",Strings.Shop.BuyItem,24,52,340,28,14);
        NavalManagementWindow.Text(this,"SellHeader",Strings.Shop.SellItem,406,52,340,28,14);
        _buy=List("BuyList",24);_sell=List("SellList",406);
        _message=NavalManagementWindow.Text(this,"Status","",24,570,590,26);
        _upgrade=NavalManagementWindow.Button(this,"ExpandAmmo","Expand ammunition inventory",24,601,590,32,()=>NavalManagementWindow.Request(NavalManageAction.UpgradeAmmoInventory));
        SetSize(780,675);
        NavalManagementWindow.Button(this,"CloseAmmoShop","Close",640,565,110,32,Hide);Hide();
    }
    protected override void EnsureInitialized() { }
    private ScrollControl List(string name,int x)
    {var list=new ScrollControl(this,name) {OverflowX=OverflowBehavior.Hidden,OverflowY=OverflowBehavior.Scroll,AutoHideBars=false};list.SetBounds(x,90,350,468);ShopWindow.ConfigureNativeScrollbar(list);return list;}
    internal void Apply(NavalManagementState state)
    {
        _message.Text=string.Format(NavalManagementWindow.T("Ammunition: {0}/{1}"),state.AmmoUsed,state.AmmoCapacity)+"  "+NavalManagementWindow.T(state.Message);
        _upgrade.IsHidden=state.AmmoUpgrade==null;
        if(state.AmmoUpgrade is {} upgrade)
        {
            _upgrade.IsDisabled=!upgrade.CanBuy;
            _upgrade.Text=string.Format(NavalManagementWindow.T("Expand to {0} - {1} {2}"),upgrade.NextCapacity,upgrade.Price,upgrade.Currency);
            var requirement=string.Format(NavalManagementWindow.T("Gunnery level {0}"),upgrade.RequiredLevel);
            if(upgrade.QuestName.Length>0)
            {
                var quest=string.Format(NavalManagementWindow.T("Complete quest: {0}"),upgrade.QuestName);
                requirement=upgrade.UnlockRule==NavalAmmoUnlockRule.CompletedQuest?quest:upgrade.UnlockRule==NavalAmmoUnlockRule.GunneryLevelOrQuest
                    ?string.Format(NavalManagementWindow.T("{0} or {1}"),requirement,quest):requirement;
            }
            _upgrade.TooltipText=requirement;
        }
        _buy.DeleteAllChildren();_sell.DeleteAllChildren();var by=0;var sy=0;
        foreach(var ammo in state.Ammo)
        {
            if(ammo.Quantity<0 || ItemDescriptor.Get(ammo.ItemId) is not {} item)continue;
            if(ammo.ForSale) {var row=ShopProductRow.Buy(_buy,by,item,ammo.Price,ammo.Currency,ammo.CanBuy && ammo.MaximumPurchase>0,()=>Quantity(ammo, false),nativeButton:true);row.SetBounds(0,by,332,75);by+=76;}
            if(ammo.SellPrice>0 && ammo.Quantity+ (long)ammo.MainQuantity>0) {var row=ShopProductRow.Sell(_sell,sy,item,(int)Math.Min(int.MaxValue,ammo.Quantity+(long)ammo.MainQuantity),ammo.SellPrice,ammo.SellCurrency,()=>Quantity(ammo, true),nativeButton:true);row.SetBounds(0,sy,332,75);sy+=76;}
        }
        _buy.UpdateScrollBars();_sell.UpdateScrollBars();Show();BringToFront();
    }
    private static void Quantity(NavalAmmoOffer ammo, bool sell)
    {
        var item = ItemDescriptor.Get(ammo.ItemId);
        if (item == null) return;
        var maximum = sell ? (int)Math.Min(10000, (long)ammo.Quantity + ammo.MainQuantity) : ammo.MaximumPurchase;
        if (maximum <= 0) return;
        _ = new InputBox(
            title: sell ? Strings.Shop.SellItem : Strings.Shop.BuyItem,
            prompt: sell ? Strings.Shop.SellItemPrompt.ToString(item.Name) : Strings.Shop.BuyItemPrompt.ToString(item.Name),
            inputType: InputType.NumericSliderInput,
            quantity: 1, maximumQuantity: maximum,
            onSubmit: (_, args) =>
            {
                if (args.Value is NumericalSubmissionValue value && value.Value > 0 && value.Value <= maximum)
                    NavalManagementWindow.Request(sell ? NavalManageAction.SellAmmo : NavalManageAction.BuyAmmo, ammo.Kind, (int)value.Value);
            });
    }

}
