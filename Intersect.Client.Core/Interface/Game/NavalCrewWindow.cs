using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Newtonsoft.Json;
using Chrome=Intersect.Client.Interface.Game.MarketplaceWindow.MarketChrome;
using Style=Intersect.Client.Interface.Game.MarketplaceWindow.FrameStyle;
namespace Intersect.Client.Interface.Game;

internal sealed class NavalCrewWindow:Window
{
    private readonly Label _heading,_hint,_status,_cargo,_footer;
    private readonly ScrollControl _members;
    private readonly Button _action;
    private readonly Label _actionCaption;
    private ShipCrewState _state=new();
    private string _signature="";
    internal NavalCrewWindow(Canvas parent):base(parent,T("Ship crew"),false,nameof(NavalCrewWindow))
    {
        var width=Math.Min(680,parent.Width-24);var height=Math.Min(570,parent.Height-32);
        SetSize(width,height);Alignment=[Alignments.Center];DeleteOnClose=false;DisableResizing();
        var frame=new Chrome(this,"CrewFrame",Style.Window);frame.SetBounds(14,8,width-28,height-36);
        _heading=Text(this,"CrewHeading",18,true);_heading.SetBounds(24,28,width-48,32);
        _hint=Text(this,"CrewHint",10);_hint.SetBounds(24,66,width-48,24);
        _status=Text(this,"CrewStatus",10);_status.SetBounds(24,102,width-48,24);
        _members=new ScrollControl(this,"CrewRoster") {OverflowX=OverflowBehavior.Hidden,OverflowY=OverflowBehavior.Scroll,AutoHideBars=true};
        _members.SetBounds(24,142,width-48,height-342);
        _cargo=Text(this,"CrewCargo",11);_cargo.SetBounds(24,height-137,width-48,24);
        _action=new Button(this,"CrewAction") {Text=""};_action.SetBounds(24,height-95,width-200,34);
        var actionFrame=new Chrome(_action,"CrewActionFrame",Style.PrimaryAction);actionFrame.SetBounds(0,0,width-200,34);
        _actionCaption=Text(_action,"CrewActionCaption",11,true);_actionCaption.SetBounds(4,3,width-208,28);
        _action.Clicked+=(_,_)=>Request(_state.CaptainId==Globals.Me?.Id?HarbourAction.ToggleCrewBoarding:
            _state.ShipId!=Guid.Empty?HarbourAction.LeaveCrew:HarbourAction.ClaimCargo);
        var close=new Button(this,"CrewClose") {Text=T("Close"),Font=GameContentManager.Current.GetFont("sourcesanspro")??Skin.DefaultFont,FontSize=11};
        close.SetBounds(width-164,height-95,140,34);close.Clicked+=(_,_)=>Hide();
        NavalManagementWindow.Button(this,"NavalActivities","Naval activities",24,height-190,Width-48,32,()=>NavalManagementWindow.Request(NavalManageAction.Ammo));
        _footer=Text(this,"CrewFooter",10);_footer.SetBounds(24,height-49,width-48,22);Hide();
    }
    protected override void EnsureInitialized() { }
    private static string T(string source)=>NavalSetupText.Get(source,ClientLanguages.Current);
    private static Label Text(Base parent,string name,int size,bool bold=false)=>new(parent,name) {
        AutoSizeToContents=false,MouseInputEnabled=false,Font=GameContentManager.Current.GetFont(bold?"sourcesansproblack":"sourcesanspro")??parent.Skin.DefaultFont,
        FontSize=size,TextColorOverride=bold?new Color(255,236,210,153):new Color(255,230,218,198)};
    private void Request(HarbourAction action,Guid ship=default)=>Networking.Network.SendPacket(new HarbourRequestPacket {View=HarbourView.Crew,Action=action,ShipId=ship});
    internal void Apply(HarbourStatePacket packet)
    {
        EventPanelText.Fit(_status,T(packet.Message),10);Update(packet.Crew);
    }
    internal void Update(ShipCrewState state)
    {
        var signature=ClientLanguages.Current+JsonConvert.SerializeObject(state);
        if(signature==_signature) return;_signature=signature;_state=state;
        var aboard=state.ShipId!=Guid.Empty;var captain=aboard && state.CaptainId==Globals.Me?.Id;
        EventPanelText.Fit(_heading,aboard?string.Format(T("Ship crew · {0}/{1}"),state.Members.Length,state.Capacity):T("Ship crew"),18,12);
        EventPanelText.Fit(_hint,T(aboard?"The captain steers and fires. Every member keeps their own rewards.":"Join the captain's party, then board together at a Q dock."),10);
        EventPanelText.Fit(_cargo,string.Format(T("Your crew cargo: {0} lots"),state.PersonalCargoCount),11);
        EventPanelText.Fit(_footer,T("Crew cargo is lost if the ship sinks. Unload your share at Q."),10);
        _actionCaption.Text=T(captain?(state.BoardingOpen?"Close crew boarding":"Open crew boarding"):
            aboard?"Disembark":"Unload my crew cargo");
        _action.IsDisabled=!state.AtDock || (!aboard && state.PersonalCargoCount==0);
        var scroll=_members.VerticalScrollBar.ScrollAmount;_members.DeleteAll();var y=4;
        if(aboard)
            foreach(var member in state.Members)
            {
                var row=new Chrome(_members,"CrewMember"+member.PlayerId,Style.Card);row.SetBounds(2,y,_members.Width-26,52);
                var label=Text(row,"CrewName",12,true);label.SetBounds(12,10,row.Width-130,30);EventPanelText.Fit(label,member.Name,12);
                var role=Text(row,"CrewRole",10);role.SetBounds(row.Width-112,12,104,26);role.Text=T(member.Captain?"Captain":"Crew member");
                if(captain && !member.Captain){var id=member.PlayerId;role.Hide();NavalManagementWindow.Button(row,"OfferHelm","Offer helm",row.Width-126,9,112,32,()=>NavalManagementWindow.Request(NavalManageAction.OfferHelm,target:id));}
                y+=60;
            }
        else foreach(var offer in state.Offers)
        {
            var row=new Chrome(_members,"CrewOffer"+offer.ShipId,Style.Card);row.SetBounds(2,y,_members.Width-26,76);
            var label=Text(row,"CrewOfferName",12,true);label.SetBounds(12,8,row.Width-132,26);
            EventPanelText.Fit(label,string.IsNullOrWhiteSpace(offer.Name)?T("Unnamed ship"):offer.Name,12);
            var detail=Text(row,"CrewOfferCaptain",10);detail.SetBounds(12,39,row.Width-132,24);
            EventPanelText.Fit(detail,string.Format(T("Captain: {0} · {1}/{2}"),offer.CaptainName,offer.Occupied,offer.Capacity),10);
            var join=new Button(row,"CrewJoin") {Text=T("Board"),Font=GameContentManager.Current.GetFont("sourcesanspro")??Skin.DefaultFont,FontSize=11};
            join.SetBounds(row.Width-110,20,98,32);join.Clicked+=(_,_)=>Request(HarbourAction.JoinCrew,offer.ShipId);y+=84;
        }
        if(y==4)
        {var empty=Text(_members,"CrewEmpty",11);empty.SetBounds(10,12,_members.Width-32,30);EventPanelText.Fit(empty,T("No party ship is currently open for boarding at this dock."),11);}
        _members.UpdateScrollBars();_members.VerticalScrollBar.ScrollAmount=scroll;
    }
}
