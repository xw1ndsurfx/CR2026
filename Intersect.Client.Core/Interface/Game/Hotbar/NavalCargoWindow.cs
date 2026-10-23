using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Client.Interface.Shared;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;
using Chrome = Intersect.Client.Interface.Game.MarketplaceWindow.MarketChrome;
using Style = Intersect.Client.Interface.Game.MarketplaceWindow.FrameStyle;
using UiRectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
namespace Intersect.Client.Interface.Game.Hotbar;

internal sealed class NavalCargoWindow : Window
{
    private static readonly Color Gold=new(255,224,190,113),Cream=new(255,246,229,202),Muted=new(255,193,180,161);
    private readonly Chrome _frame,_ribbon,_section;
    private readonly Label _heading,_subtitle,_summary,_hint,_warning,_empty;
    private readonly ScrollControl _list;
    private readonly ScrollControl _plunder;
    private readonly Button _takeAll;
    private bool _results;
    private long _revealStarted;
    private int _revealed=-1;
    private readonly List<(Base Row,int Y,long Started)> _revealRows=[];
    private readonly NavalSailingBar _capacity;
    private readonly Button _close;
    private readonly Label _closeText;
    private CargoStamp[] _contents=[];
    private Guid _shipId;
    private int _contentRevision=-1;
    private string _language="";
    private bool _layoutChanged=true;
    private readonly record struct CargoStamp(Guid Id,Guid ItemId,int Quantity,int Quality,NavalCargoKind Kind)
    {
        internal CargoStamp(NavalCargoParcel parcel):this(parcel.Id,parcel.ItemId,parcel.Quantity,parcel.Quality,parcel.Kind) { }
    }

    internal NavalCargoWindow(Base parent):base(parent,T("Cargo hold"),false,nameof(NavalCargoWindow))
    {
        Closed+=(_,_)=>Naval.NavalWorld.CargoOpen=false;
        Alignment=[Alignments.Center];DeleteOnClose=false;DisableResizing();
        _frame=new Chrome(this,"CargoFrame",Style.Window);
        _ribbon=new Chrome(this,"CargoRibbon",Style.Ribbon);
        _section=new Chrome(this,"CargoSection",Style.Section);
        _heading=Text(this,"CargoHeading",20,Gold,true,true);
        _subtitle=Text(this,"CargoSubtitle",10,Muted,center:true);
        _summary=Text(this,"CargoSummary",12,Cream,true);
        _capacity=new NavalSailingBar(this,"CargoCapacity") {MouseInputEnabled=false,KeyboardInputEnabled=false};
        _list=new ScrollControl(this,"CargoManifest") {
            OverflowX=OverflowBehavior.Hidden,OverflowY=OverflowBehavior.Scroll,AutoHideBars=true};
        _plunder=new ScrollControl(this,"PlunderSummary") {OverflowX=OverflowBehavior.Hidden,OverflowY=OverflowBehavior.Scroll,AutoHideBars=true};
        Shop.ShopWindow.ConfigureNativeScrollbar(_plunder);
        _takeAll=NavalManagementWindow.Button(this,"TakeAll","Take all",34,530,150,35,()=>
        {
            if(Naval.NavalWorld.ShoreCargo is {} hold)
                Act(hold,new NavalCargoParcel(),HarbourAction.ClaimAllCargo);
        });
        _empty=Text(this,"CargoEmpty",13,Muted,center:true);
        _hint=Text(this,"CargoDockHint",10,Cream);
        _warning=Text(this,"CargoLossWarning",10,new Color(255,225,151,119));
        _close=new Button(this,"CargoClose") {Text=""};
        NavalManagementWindow.StyleButton(_close);
        _closeText=Text(_close,"CargoCloseCaption",11,Cream,true,true);
        _close.Clicked+=(_,_)=>Naval.NavalWorld.CargoOpen=false;

        SetSize(720,610);Hide();
    }
    protected override void EnsureInitialized() { }
    private static string T(string source)=>NavalSetupText.Get(source,ClientLanguages.Current);
    private static Label Text(Base parent,string id,int size,Color color,bool bold=false,bool center=false) => new(parent,id) {
        AutoSizeToContents=false,Font=GameContentManager.Current.GetFont(bold?"sourcesansproblack":"sourcesanspro")??parent.Skin.DefaultFont,
        FontSize=size,TextColorOverride=color,TextAlign=center?Pos.Center:Pos.Left|Pos.CenterV,MouseInputEnabled=false};

    private void LayoutManifest()
    {
        var width=Math.Min(_results?900:720,Math.Max(300,(Parent?.Width??744)-24));
        var height=Math.Min(610,Math.Max(360,(Parent?.Height??642)-32));
        if(Width==width && Height==height && !_layoutChanged) return;
        SetSize(width,height);_layoutChanged=true;
        _frame.SetBounds(14,8,width-28,height-36);
        _heading.SetBounds(28,40,width-56,32);_subtitle.SetBounds(28,76,width-56,23);
        _ribbon.SetBounds(28,110,width-56,58);_summary.SetBounds(42,115,width-84,25);
        _capacity.SetBounds(42,146,width-84,10);
        _section.SetBounds(28,180,width-56,height-284);
        var contentWidth=width-76;
        var leftWidth=_results?(int)(contentWidth*.56):contentWidth;
        _list.SetBounds(38,190,leftWidth,height-304);
        _plunder.SetBounds(50+leftWidth,190,Math.Max(1,contentWidth-leftWidth-12),height-304);
        _plunder.IsHidden=!_results;
        _takeAll.SetBounds(34,height-66,150,35);_takeAll.IsHidden=!_results;
        _warning.IsHidden=_results;
        _empty.SetBounds(50,207,width-100,32);
        _hint.SetBounds(34,height-95,width-68,22);
        _warning.SetBounds(34,height-72,width-218,22);
        _close.SetBounds(width-164,height-66,128,35);

        _closeText.SetBounds(4,3,120,29);
    }

    internal void Update(ShipSnapshot? ship,bool open)
    {
        if(!open || ship==null || ship.Sinking) {Hide();return;}
        var results=Naval.NavalWorld.OwnShip==null;
        if(results!=_results){_results=results;_layoutChanged=true;_revealStarted=Environment.TickCount64;_revealed=-1;}
        LayoutManifest();
        if(IsHidden) {Show();BringToFront();_layoutChanged=true;_revealStarted=Environment.TickCount64;_revealed=-1;}
        var languageChanged=_language!=ClientLanguages.Current;
        if(languageChanged || _layoutChanged)
        {
            _language=ClientLanguages.Current;Title=T("Cargo hold");
            EventPanelText.Fit(_heading,T("SHIP'S HOLD"),20,14);
            EventPanelText.Fit(_subtitle,T("YOUR SPOILS, SAFELY STOWED UNTIL PORT"),10);
            EventPanelText.Fit(_hint,T("Choose lots to unload at a Q dock, or discard unwanted cargo."),10);
            EventPanelText.Fit(_warning,T("Cargo is lost if your ship sinks."),10);
            _closeText.Text=T("Close");_empty.Text=T("The cargo hold is empty.");
        }
        _heading.Text=T(_results?"Voyage results":"CARGO HOLD");
        if(_results){_subtitle.Text=ClientLanguages.ContentText(ship.Name);_hint.Text=T("Your cargo has been revealed. Collect your rewards.");}
        var cargo=ship.Cargo??[];
        _takeAll.IsDisabled=cargo.Length==0;
        var shipChanged=_shipId!=ship.Id;
        if(shipChanged)_revealStarted=Environment.TickCount64;
        var interval=Math.Clamp(6000/Math.Max(1,cargo.Length),120,650);
        var revealCount=!_results || Intersect.Client.WorldEvents.PremiumState.Remaining>0?cargo.Length:
            Math.Min(cargo.Length,(int)((Environment.TickCount64-_revealStarted)/interval)+1);
        var revealChanged=_revealed!=revealCount;
        _revealed=revealCount;
        _takeAll.IsDisabled=cargo.Length==0 || revealCount<cargo.Length;
        var displayed=cargo.OrderByDescending(c=>c.Quality).Take(revealCount).ToArray();
        if(_results && revealCount<cargo.Length)_hint.Text=string.Format(T("Revealing cargo: {0}/{1}"),revealCount,cargo.Length);
        foreach(var (row,baseY,started) in _revealRows)
            row.Y=baseY+(int)(12*(1-Math.Clamp((Environment.TickCount64-started)/240d,0,1)));
        var changed=shipChanged || _contents.Length!=cargo.Length ||
            cargo.Where((parcel,index)=>index>=_contents.Length || _contents[index]!=new CargoStamp(parcel)).Any();
        _summary.Text=string.Format(T("Cargo: {0}/{1} lots"),cargo.Length,ship.CargoCapacity)+(ship.CargoCapacity>0 && cargo.Length>=ship.CargoCapacity?" - MAX":"");
        _summary.TextColorOverride=ship.CargoCapacity>0 && cargo.Length>=ship.CargoCapacity?new Color(255,240,180,108):Cream;
        _capacity.Value=ship.CargoCapacity<=0?0:Math.Clamp(cargo.Length/(float)ship.CargoCapacity,0,1);
        _empty.IsHidden=cargo.Length>0;
        if(changed || revealChanged || languageChanged || _layoutChanged || _contentRevision!=ClientLanguages.ContentRevision)
        {
            var scroll=_list.VerticalScrollBar.ScrollAmount;
            _list.DeleteAll();_revealRows.Clear();
            var width=Math.Max(180,_list.Width-26);var y=4;
            foreach(var parcel in displayed)
            {
                var row=new CargoRow(_list,parcel,width,()=>Act(ship,parcel,HarbourAction.ClaimCargo),()=>new InputBox(T("Discard cargo"),T("Discard this cargo lot permanently?"),InputType.YesNo,(_,_)=>Act(ship,parcel,HarbourAction.DiscardCargo)));row.SetBounds(4,y,width,120);if(_results && Intersect.Client.WorldEvents.PremiumState.Remaining<=0 && parcel.Id==displayed.LastOrDefault()?.Id)_revealRows.Add((row,y,Environment.TickCount64));y+=128;
            }
            _plunder.DeleteAll();
            if(_results)
            {
                var totalY=4;
                foreach(var group in displayed.GroupBy(parcel=>parcel.ItemId).OrderBy(group=>ItemDescriptor.Get(group.Key)?.Name))
                {
                    var item=ItemDescriptor.Get(group.Key);
                    var name=Text(_plunder,"RewardName"+group.Key,12,Cream,true);
                    name.SetBounds(6,totalY,_plunder.Width-26,26);
                    EventPanelText.Fit(name,ClientLanguages.ContentText(item?.Name??T("Unknown item")),12,9);
                    var quantity=Text(_plunder,"RewardQuantity"+group.Key,12,Gold);
                    quantity.SetBounds(6,totalY+27,_plunder.Width-26,26);
                    quantity.Text=NavalSetupText.Display(string.Format(T("Qty: {0:N0}"),group.Sum(parcel=>(long)parcel.Quantity)));
                    totalY+=64;
                }
                _plunder.SetInnerSize(_plunder.Width-20,totalY+8);_plunder.UpdateScrollBars();
            }
            _list.SetInnerSize(_list.Width-20,y+8);
            _list.UpdateScrollBars();
            if(shipChanged) _list.ScrollToTop();
            else _list.VerticalScrollBar.ScrollAmount=scroll;
            _contents=cargo.Select(p=>new CargoStamp(p)).ToArray();
            _shipId=ship.Id;_contentRevision=ClientLanguages.ContentRevision;_layoutChanged=false;
        }
    }

    private static void Act(ShipSnapshot ship,NavalCargoParcel parcel,HarbourAction action) =>
        Intersect.Client.Networking.Network.SendPacket(new Intersect.Network.Packets.Client.HarbourRequestPacket {
            Action=action,ShipId=ship.Id,ParcelId=parcel.Id,
            View=ship.CaptainId==Intersect.Client.General.Globals.Me?.Id?HarbourView.Fleet:HarbourView.Crew});

    private sealed class CargoRow : Base
    {
        internal CargoRow(Base parent,NavalCargoParcel parcel,int width,Action unload,Action discard):base(parent,"CargoLot"+parcel.Id.ToString("N"))
        {
            SetSize(width,120);
            var frame=new Chrome(this,"CargoCardFrame",Style.Card);frame.SetBounds(0,0,width,120);
            var portrait=new Chrome(this,"CargoIconFrame",Style.Preview);portrait.SetBounds(12,12,60,60);
            var descriptor=ItemDescriptor.Get(parcel.ItemId);
            var icon=new ImagePanel(this,"CargoItemIcon") {MaintainAspectRatio=true,ShouldDrawBackground=false,MouseInputEnabled=false};
            icon.SetBounds(20,20,44,44);
            if(!string.IsNullOrWhiteSpace(descriptor?.Icon))
            {
                icon.Texture=GameContentManager.Current.GetTexture(descriptor.UsesSpellIcon?TextureType.Spell:TextureType.Item, descriptor.DisplayIcon);
                icon.RenderColor=descriptor.Color;
            }
            // Missing resources keep a visible empty icon frame without substituting a different item.
            var sealedCargo=parcel.ItemId==Guid.Empty;
            var name=sealedCargo?T("Sealed cargo"):ClientLanguages.ContentText(descriptor?.Name??T("Unknown item"));
            var title=Text(this,"CargoItemName",13,Cream,true);title.SetBounds(86,9,width-240,25);
            EventPanelText.Fit(title,name,13,10);
            var kind=Text(this,"CargoKind",10,Muted);kind.SetBounds(86,36,width-105,20);
            EventPanelText.Fit(kind,T(NavalCargo.Caption(parcel.Kind)),10);
            var quantity=Text(this,"CargoQuantity",12,Gold,true,true);quantity.SetBounds(width-150,12,138,28);
            EventPanelText.Fit(quantity,NavalSetupText.Display(string.Format(T("Qty: {0:N0}"),parcel.Quantity)),12,9);quantity.IsHidden=sealedCargo;
            var quality=Text(this,"CargoQuality",10,QualityColor(parcel.Quality),true);
            quality.SetBounds(156,59,width-170,19);quality.Text=string.Format(T("Quality {0}/5"),parcel.Quality);
            var marks=new QualityMarks(this,parcel.Quality);marks.SetBounds(86,64,60,9);
            var bx=width<330?12:86;var bw=Math.Max(60,Math.Min(110,(width-bx-20)/2));
            var unloadButton=new Button(this,"CargoUnload") {Text=""};unloadButton.SetBounds(bx,86,bw,26);
            var discardButton=new Button(this,"CargoDiscard") {Text=""};discardButton.SetBounds(bx+bw+8,86,bw,26);
            foreach(var (button,caption) in new[]{(unloadButton,"Unload"),(discardButton,"Discard")})
            {
                NavalManagementWindow.StyleButton(button);
                var label=Text(button,"CargoActionCaption",11,Cream,true,true);label.SetBounds(4,2,bw-8,22);
                EventPanelText.Fit(label,T(caption),11,9);
            }
            unloadButton.IsDisabled=sealedCargo;
            unloadButton.Clicked+=(_,_)=>unload();discardButton.Clicked+=(_,_)=>discard();
            TooltipText=name;
        }
    }
    private static Color QualityColor(int quality)=>quality switch {
        5=>new(255,242,192,78),4=>new(255,197,149,236),3=>new(255,117,180,237),2=>new(255,133,210,153),_=>Muted};
    private sealed class QualityMarks(Base parent,int quality):Base(parent,"CargoQualityMarks")
    {
        protected override void Render(Intersect.Client.Framework.Gwen.Skin.Base skin)
        {
            var renderer=skin.Renderer;var old=renderer.DrawColor;
            for(var i=0;i<5;i++) {renderer.DrawColor=i<quality?QualityColor(quality):new Color(255,78,59,44);renderer.DrawFilledRect(new UiRectangle(i*12,0,8,8));}
            renderer.DrawColor=old;
        }
    }
}
