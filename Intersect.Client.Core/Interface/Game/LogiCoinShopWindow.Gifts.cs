using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Framework.Core.LogiCoins;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
namespace Intersect.Client.Interface.Game;
internal sealed partial class LogiCoinShopWindow
{
    private bool _gifting;
    private LogiCoinPurchaseHistoryRow[] _history=[];
    private GiftRecipientWindow? _giftWindow;
    private static string Q(string source)=>Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get(source,ClientLanguages.Current);
    private void RequestPurchase(LogiCoinShopOffer offer)
    {
        if(!_gifting){Networking.PacketSender.SendPurchaseLogiCoinOffer(offer.Id,Guid.NewGuid());return;}
        if(!offer.AllowGifts){_status.Text=Q("This offer cannot be gifted.");return;}
        _giftWindow?.Destroy();_giftWindow=new GiftRecipientWindow(Canvas!,offer);_giftWindow.Show();
    }
    internal void ApplyRecipient(LogiCoinRecipientPacket packet) { if(_giftWindow is { IsVisibleInParent:true }) _giftWindow.Apply(packet); }
    private void ShowPurchaseHistory()
    {
        var window=new HistoryWindow(Canvas!){DeleteOnClose=true,Alignment=[Alignments.Center]};
        window.SetSize(700,470);
        var list=new ScrollControl(window,"PurchaseHistoryList"){OverflowX=OverflowBehavior.Hidden,OverflowY=OverflowBehavior.Scroll,AutoHideBars=true};
        list.SetBounds(16,24,660,385);var y=0;
        foreach(var row in _history)
        {
            var when=DateTimeOffset.FromUnixTimeMilliseconds(row.CreatedUtc).ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            NavalManagementWindow.Text(list,"Purchase"+y,$"{when} · {row.Price} LogiCoins · {ClientLanguages.ContentText(row.Offer)}",8,y,625,26);
            NavalManagementWindow.Text(list,"Recipient"+y,$"{row.Recipient} · {Q(row.Completed?"Delivered":"Pending delivery")}",8,y+27,625,26);y+=68;
        }
        if(_history.Length==0)NavalManagementWindow.Text(list,"EmptyHistory",Q("No previous purchases."),8,8,625,30);
        list.SetInnerSize(640,y+20);list.UpdateScrollBars();window.Show();window.BringToFront();
    }
    private sealed class HistoryWindow(Canvas canvas):Window(canvas,Q("Purchase history"),false,"LogiCoinPurchaseHistory")
    { protected override void EnsureInitialized() {} }
    private sealed class GiftRecipientWindow:Window
    {
        protected override void EnsureInitialized() {}
        private readonly LogiCoinShopOffer _offer;
        private readonly TextBox _account;
        private readonly ComboBox _character;
        private readonly Button _confirm;
        private readonly Label _status;
        private Guid _recipient;
        private bool _finished;
        internal void Destroy()
        {
            if(_finished)return;_finished=true;Hide();Parent?.RemoveChild(this,false);Dispose();
        }
        internal GiftRecipientWindow(Canvas canvas,LogiCoinShopOffer offer):base(canvas,Q("Gift a friend"),false,"LogiCoinGiftRecipient")
        {
            _offer=offer;SetSize(620,350);Alignment=[Alignments.Center];DeleteOnClose=true;DisableResizing();
            NavalManagementWindow.Text(this,"GiftOffer",ClientLanguages.ContentText(offer.Name)+$" · {offer.EffectivePrice(DateTimeOffset.UtcNow)} LogiCoins",20,26,570,35);
            NavalManagementWindow.Text(this,"RecipientAccount",Q("Recipient account"),20,82,180,28);
            _account=new TextBox(this,"GiftAccount"){Text=""};_account.SetBounds(205,82,270,28);
            NavalManagementWindow.Button(this,"FindRecipient",Q("Search"),485,82,100,28,()=>
            {
                _recipient=Guid.Empty;_confirm.IsDisabled=true;
                Networking.Network.SendPacket(new LogiCoinRecipientRequestPacket{Account=(_account.Text??"").Trim()});
            });
            _character=new ComboBox(this,"GiftCharacter");_character.SetBounds(205,132,380,28);
            _character.IsHidden=offer.AccountDelivery;
            NavalManagementWindow.Text(this,"GiftScope",Q(offer.AccountDelivery?"Delivered to the account":"Recipient character"),20,132,180,28);
            _status=NavalManagementWindow.Text(this,"GiftMessage","",20,188,565,42);
            _confirm=NavalManagementWindow.Button(this,"ConfirmGift",Q("Purchase gift"),205,250,260,35,()=>
            {
                if(_recipient==Guid.Empty)return;
                var character=_character.SelectedItem?.UserData is Guid id?id:Guid.Empty;
                if(!_offer.AccountDelivery && character==Guid.Empty)return;
                Networking.Network.SendPacket(new PurchaseLogiCoinOfferPacket(_offer.Id,Guid.NewGuid()){RecipientUserId=_recipient,RecipientCharacterId=character});
                Destroy();
            });
            _confirm.IsDisabled=true;
            _account.TextChanged+=(_,_)=>{_recipient=Guid.Empty;_confirm.IsDisabled=true;};
            Closed+=(_,_)=>{_finished=true;Networking.PacketSender.SendRequestLogiCoinShopState();};
            Disposed+=(_,_)=>_finished=true;
        }
        internal void Apply(LogiCoinRecipientPacket packet)
        {
            if(packet.UserId==Guid.Empty){_status.Text=Q("Recipient account not found.");return;}
            if(!string.Equals(packet.Account,(_account.Text??"").Trim(),StringComparison.OrdinalIgnoreCase))return;
            _recipient=packet.UserId;_character.ClearItems();
            foreach(var character in packet.Characters)_character.AddItem(character.Name,userData:character.Id);
            _character.SelectByUserData(packet.Characters.FirstOrDefault()?.Id??Guid.Empty);
            _confirm.IsDisabled=!_offer.AccountDelivery && packet.Characters.Length==0;
            _status.Text=packet.Account+" · "+Q(_offer.AccountDelivery?"Delivered to the account":"Choose the recipient character.");
        }
    }
}
