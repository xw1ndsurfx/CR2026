using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Client.Interface.Shared;
using Intersect.Client.Framework.Gwen.Control.EventArguments.InputSubmissionEvent;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

internal sealed partial class HarbourWindow : Window
{
    private readonly Label _message, _pageLabel;
    private readonly ScrollControl _cards;
    private readonly Button _previous, _next,_refresh;
    private readonly Button? _cargoResults;
    private HarbourStatePacket _state = new();
    private int _page;
    private bool _waiting;
    private long _sent;
    private readonly HarbourView _view;
    public HarbourWindow(Canvas parent, HarbourView view) : base(parent,
        ClientLanguages.Text(view == HarbourView.Shop ? "Ship shop and repairs" : "My fleet"), false,
        view == HarbourView.Shop ? "ShipShopWindow" : nameof(HarbourWindow))
    {
        _view = view;
        SetSize(660, view==HarbourView.Fleet?540:500); Alignment = [Alignments.Center]; DeleteOnClose = false; DisableResizing();
        if(view==HarbourView.Fleet)
            Button(this,"FleetCrew","Ship crew",16,480,625,()=>Networking.Network.SendPacket(new HarbourRequestPacket {View=HarbourView.Crew}));

        _message = Text(this, "Status", "", 16, 36, 625, 58);
        _cards = new ScrollControl(this, "ShipCards") {OverflowX=OverflowBehavior.Hidden,OverflowY=OverflowBehavior.Scroll,AutoHideBars=false}; _cards.SetBounds(16, 100, 625, 320);
        _previous = Button(this, "Previous", "Previous", 16, 435, 110, () => { _page--; RenderEntries(); });
        _pageLabel = Text(this, "Page", "", 170, 440, 200, 28);
        _next = Button(this, "Next", "Next", 420, 435, 100, () => { _page++; RenderEntries(); });
        _refresh=Button(this, "Refresh", "Refresh", 530, 435, 110, () => Request(HarbourAction.Open, ""));
        if(view==HarbourView.Fleet)_cargoResults=NavalManagementWindow.Button(this,"VoyageResults","Voyage results",24,435,260,30,()=>
        {
            if(Naval.NavalWorld.ShoreCargo!=null){Naval.NavalWorld.CargoOpen=true;Hide();}
        });
        if(view==HarbourView.Shop)InitializeMerchant();
        else {_previous.Hide();_next.Hide();_pageLabel.Hide();Shop.ShopWindow.ConfigureNativeScrollbar(_cards); }
    }
    protected override void EnsureInitialized() { }
    public override void Think()
    {
        base.Think();
        if(_cargoResults!=null)_cargoResults.IsDisabled=Naval.NavalWorld.ShoreCargo==null;
    }
    private static Label Text(Base parent, string id, string text, int x, int y, int w, int h)
    {
        var label = new Label(parent, id) { Text = text, AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? parent.Skin.DefaultFont, FontSize = 11 };
        label.SetBounds(x,y,w,h); return label;
    }
    private static Button Button(Base parent, string id, string caption, int x, int y, int w, Action action)
    {
        var button = new Button(parent, id) { Text = caption is "Unload cargo" or "Ship crew" or "Sailing skills"?NavalSetupText.Get(caption,ClientLanguages.Current):ClientLanguages.Text(caption),
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? parent.Skin.DefaultFont, FontSize = 11 };
        NavalManagementWindow.StyleButton(button);button.SetBounds(x,y,w,30); button.Clicked += (_,_) => action(); return button;
    }
    internal void Apply(HarbourStatePacket packet)
    {
        _state = packet; _state.Entries ??= []; _waiting = false;
        var message=packet.Message.Length==0 ? (_view==HarbourView.Shop ? "Buy a ship or fully repair a damaged or sunken ship." : "Choose one of your ships to set sail.") : packet.Message;
        var translated=NavalSetupText.Get(message,ClientLanguages.Current);
        _message.Text=translated==message?ClientLanguages.Expanded(message):translated;
        if(_cargoResults!=null)_cargoResults.IsDisabled=Naval.NavalWorld.ShoreCargo==null;
        RenderEntries();
    }
    private void Request(HarbourAction action, string model,string name="")
    {
        // Retry is possible after a lost reply. The server revalidates every operation at the dock.
        if (_waiting && Environment.TickCount64 - _sent < 3000) return;
        _waiting = true; _sent = Environment.TickCount64;
        Networking.Network.SendPacket(new HarbourRequestPacket { Action = action, ModelId = model, View = _view,Name=name });
    }
    private void RenderEntries()
    {
        if(_view==HarbourView.Shop){RenderMerchant();return;}
        _cards.DeleteAllChildren();
        var pages = Math.Max(1, (_state.Entries.Length + 1) / 2); _page = Math.Clamp(_page, 0, pages - 1);
        _pageLabel.Text = $"{_page+1} / {pages}"; _previous.IsDisabled = _page == 0; _next.IsDisabled = _page >= pages-1;
        var i = 0;
        foreach (var entry in _state.Entries)
        {
            var y = i++ * 155;
            var preview=new NavalShipPreview(_cards,"FleetShipPreview"+i,entry.Graphics,entry.Frames);
            preview.SetBounds(4,y+4,112,136);
            Text(_cards, "Name" + i, entry.ShipName.Length>0?entry.ShipName:entry.Name is "Royal ship" or "Royal ship (Premium)" ? ClientLanguages.Text(entry.Name) : ClientLanguages.ContentText(entry.Name), 128, y, 480, 24);
            var status = entry.Owned ? (entry.Health == 0 ? ClientLanguages.Text("Sunken") :
                string.Format(ClientLanguages.Text("Hull: {0}/100"), entry.Health)) : ClientLanguages.Text("Not owned");
            if (entry.PremiumRequired) status += " · " + ClientLanguages.Text("Premium required");
            Text(_cards, "Condition" + i, status, 128, y+25, 480, 23);
            if (_view == HarbourView.Shop)
            {
                Text(_cards, "Cost" + i, string.Format(ClientLanguages.Text("Purchase: {0} {1} · Balance: {2}"),
                    entry.Price, entry.Currency, entry.Balance), 4, y+49, 610, 24);
                Text(_cards, "RepairCost" + i, string.Format(ClientLanguages.Text("Full repair: {0} {1}"),
                    entry.RepairPrice, entry.Currency), 4, y+75, 610, 24);
                var buy = Button(_cards, "Buy" + i, "Buy ship", 4, y+110, 280, () => Request(HarbourAction.Buy, entry.ModelId));
                buy.IsDisabled = !HarbourUiRules.CanBuy(entry, _state.Premium);
                var repair = Button(_cards, "Repair" + i, "Fully repair", 310, y+110, 280, () => Request(HarbourAction.Repair, entry.ModelId));
                repair.IsDisabled = !HarbourUiRules.CanRepair(entry);
            }
            else
            {
                var launch = Button(_cards, "Launch" + i, "Set sail", 128, y+75, 232, () => Request(HarbourAction.Launch, entry.ModelId));
                launch.IsDisabled = !HarbourUiRules.CanLaunch(entry, _state.Premium);
                var rename=Button(_cards,"Rename"+i,"Rename ship",370,y+75,232,()=>new InputBox(
                    title:ClientLanguages.Text("Rename ship"),prompt:ClientLanguages.Text("Enter your ship name (1 to 32 characters)."),inputType:InputType.TextInput,
                    onSubmit:(_,args)=>{if(args.Value is StringSubmissionValue name) Request(HarbourAction.Rename,entry.ModelId,name.Value??"");}));
                rename.IsDisabled=!entry.Owned;
                var unload=Button(_cards,"Unload"+i,"Unload cargo",128,y+110,232,()=>Request(HarbourAction.ClaimCargo,entry.ModelId));
                unload.IsDisabled=!entry.Owned || entry.CargoCount==0;
                Text(_cards,"Cargo"+i,string.Format(NavalSetupText.Get("Cargo: {0}",ClientLanguages.Current),entry.CargoCount),370,y+113,232,24);
            }

        }
    }
}

internal static class HarbourUiRules
{
    internal static bool CanBuy(HarbourEntry e, bool premium) => e.ForSale && !e.Owned && e.CurrencyAvailable && e.Balance >= e.Price && (!e.PremiumRequired || premium);
    internal static bool CanLaunch(HarbourEntry e, bool premium) => e.Owned && e.Health > 0 && (!e.PremiumRequired || premium);
    internal static bool CanRepair(HarbourEntry e) => e.Owned && e.Health < 100 && e.CurrencyAvailable && e.Balance >= e.RepairPrice;
}
