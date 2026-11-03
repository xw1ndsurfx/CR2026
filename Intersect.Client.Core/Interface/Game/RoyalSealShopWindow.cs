using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Client.Interface.Game.Shop;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.RoyalContracts;
using Intersect.Network.Packets.RoyalContracts;
namespace Intersect.Client.Interface.Game;
internal sealed class RoyalSealShopWindow : Window
{
    private readonly ComboBox _shops, _quantity;
    private readonly Label _balance, _message;
    private readonly ScrollControl _offers;
    private RoyalSealShopState _state=new();
    private bool _applying;
    private static string T(string s)=>Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get(s,ClientLanguages.Current);
    internal RoyalSealShopWindow(Canvas parent):base(parent,T("Royal Seals shop"),false,nameof(RoyalSealShopWindow))
    {
        SetSize(780,620);Alignment=[Alignments.Center];DeleteOnClose=false;DisableResizing();
        _shops=Combo(this,"SealShopChoice",18,22,490,30);
        _quantity=Combo(this,"SealShopQuantity",530,22,220,30);
        foreach(var count in new[]{1,5,10,25})_quantity.AddItem(ClientLanguages.Format("Quantity: {0}",count),userData:count);
        _quantity.SelectByUserData(1);
        _balance=NavalManagementWindow.Text(this,"SealBalance","",18,62,730,28,13);
        _offers=new ScrollControl(this,"SealOffers"){OverflowX=OverflowBehavior.Hidden,OverflowY=OverflowBehavior.Scroll,AutoHideBars=false};
        _offers.SetBounds(18,100,738,400);ShopWindow.ConfigureNativeScrollbar(_offers);
        _message=NavalManagementWindow.Text(this,"SealMessage","",18,516,730,40,11);
        NavalManagementWindow.Button(this,"CloseSealShop",T("Close"),602,562,150,30,CloseShop);
        Closed+=(_,_)=>CloseShop();
        _shops.ItemSelected+=(_,_)=>{if(!_applying && _shops.SelectedItem?.UserData is Guid id)Networking.Network.SendPacket(new RoyalSealShopRequestPacket {Action=RoyalSealShopAction.Select,ShopId=id});};
        _quantity.ItemSelected+=(_,_)=>{if(!_applying)Rebuild();};Hide();
    }
    private static ComboBox Combo(Base parent,string name,int x,int y,int width,int height)
    {
        var box=new ComboBox(parent,name){Font=Intersect.Client.Framework.File_Management.GameContentManager.Current.GetFont("sourcesanspro")??parent.Skin.DefaultFont,FontSize=12};
        box.SetBounds(x,y,width,height);return box;
    }
    protected override void EnsureInitialized() { }
    private void CloseShop(){Hide();Networking.Network.SendPacket(new RoyalSealShopRequestPacket {Action=RoyalSealShopAction.Close});}
    internal void Apply(RoyalSealShopState state)
    {
        _state=state;_applying=true;
        try {_shops.ClearItems();foreach(var shop in state.Shops)_shops.AddItem(ClientLanguages.ContentText(shop.Name),userData:shop.Id);_shops.SelectByUserData(state.ShopId);}
        finally {_applying=false;}
        Rebuild();Show();BringToFront();
    }
    private void Rebuild()
    {
        _offers.DeleteAllChildren();
        _balance.Text=string.Format(T("Royal Seals: {0}"),_state.Seals);
        _message.Text=T(_state.Message);
        var quantity=_quantity.SelectedItem?.UserData as int? ?? 1;var index=0;
        foreach(var offer in _state.Offers)
        {
            if(ItemDescriptor.Get(offer.ItemId) is not {} item)continue;
            var total=(long)offer.Price*quantity;
            var row=ShopProductRow.Buy(_offers,offer.Slot,item,offer.Price,T("Royal Seals"),_state.Seals>=total,
                ()=>Networking.Network.SendPacket(new RoyalSealShopRequestPacket {Action=RoyalSealShopAction.Buy,ShopId=_state.ShopId,Slot=offer.Slot,Quantity=quantity,ExpectedPrice=offer.Price,ExpectedItemId=offer.ItemId}),true);
            row.SetActionCaption(T("Buy"));
            row.SetTotalPrice(total,T("Royal Seals"));
            row.SetToolTipText(ClientLanguages.ContentText(item.Name)+"\n"+string.Format(T("Total: {0} Royal Seals"),total));
            row.SetBounds(index%2*360,index/2*84,332,75);index++;
        }
        _offers.SetInnerSize(716,(index+1)/2*84);_offers.UpdateScrollBars();
    }
}
public partial class GameInterface
{
    private RoyalSealShopWindow? _royalSealShop;
    public void ApplyRoyalSealShop(RoyalSealShopStatePacket packet)
    {
        if(packet.Close){_royalSealShop?.Hide();return;}
        _royalSealShop??=new RoyalSealShopWindow(GameCanvas);
        _royalSealShop.Apply(RoyalContractRules.Read<RoyalSealShopState>(packet.Json));
    }
}
