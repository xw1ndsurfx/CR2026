using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Client.Networking;
using Intersect.Network.Packets.PartyLobbies;
namespace Intersect.Client.Interface.Game;

internal sealed class PartyLobbyWindow : Window
{
    private readonly ComboBox _filter, _activity, _dungeon;
    private readonly TextBox _search, _title;
    private readonly ScrollControl _rows;
    private readonly Label _message, _summary, _dungeonLabel;
    private readonly Button _create, _closeListing, _leaveParty;
    private PartyLobbyStatePacket _state = new();
    private bool _applying;
    private long _nextRefresh;
    private static readonly Color Gold = new(255,231,199,135);
    private static readonly Color Cream = new(255,246,235,211);
    internal static string ActivityName(LobbyActivity activity) => ClientLanguages.Text(activity switch {
        LobbyActivity.Dungeon => "Dungeon", LobbyActivity.Invasion => "Invasion", LobbyActivity.WorldBoss => "World Boss",
        LobbyActivity.Alchemy => "Alchemy", LobbyActivity.Poker => "Poker", LobbyActivity.Blackjack => "Blackjack",
        LobbyActivity.Roulette => "Roulette", LobbyActivity.Lumberjack => "Lumberjack", LobbyActivity.HouseParty => "House Party",
        LobbyActivity.Looting => "Looting", LobbyActivity.Cooking => "Cooking", _ => "All activities" });
    internal PartyLobbyWindow(Canvas canvas) : base(canvas,ClientLanguages.Text("Adventure lobbies"),false,nameof(PartyLobbyWindow))
    {
        SetSize(Math.Min(890,canvas.Width-20),Math.Min(650,canvas.Height-35));
        Alignment=[Alignments.Center]; DeleteOnClose=false; DisableResizing();
        var width=Width-32;
        _filter=Combo(this,"ActivityFilter",16,14,175);
        _filter.AddItem(ClientLanguages.Text("All activities"),userData:-1);
        foreach(var activity in Enum.GetValues<LobbyActivity>()) _filter.AddItem(ActivityName(activity),userData:(int)activity);
        _search=new TextBox(this,"LobbySearch") {Font=GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,FontSize=11};
        _search.PlaceholderText=ClientLanguages.Text("Search host, lobby or dungeon");
        _search.SetBounds(199,14,width-295,28); _search.SetToolTipText(ClientLanguages.Text("Search host, lobby or dungeon"));
        Button(this,"Refresh",Width-110,14,94,()=>Request(LobbyAction.Browse));
        _summary=Label(this,"LobbySummary",16,49,width,26,Gold);
        _rows=new ScrollControl(this,"LobbyRows") {OverflowX=OverflowBehavior.Hidden,OverflowY=OverflowBehavior.Scroll,AutoHideBars=true};
        _rows.SetBounds(16,81,width,Height-328);
        var baseY=Height-238;
        Label(this,"CreateCaption",16,baseY,width,24,Gold).Text=ClientLanguages.Text("Create a lobby for your next adventure");
        _activity=Combo(this,"CreateActivity",16,baseY+29,175);
        foreach(var activity in Enum.GetValues<LobbyActivity>()) _activity.AddItem(ActivityName(activity),userData:(int)activity);
        _title=new TextBox(this,"LobbyTitle") {Font=_search.Font,FontSize=11};
        _title.PlaceholderText=ClientLanguages.Text("Optional lobby title");
        _title.SetBounds(199,baseY+29,width-183,28); _title.SetToolTipText(ClientLanguages.Text("Optional lobby title"));
        _dungeonLabel=Label(this,"DungeonCaption",16,baseY+65,175,26,Cream); _dungeonLabel.Text=ClientLanguages.Text("Dungeon");
        _dungeon=Combo(this,"DungeonChoice",199,baseY+65,width-183);
        _create=Button(this,"Create lobby",16,baseY+101,175,Create);
        _closeListing=Button(this,"Close my lobby",199,baseY+101,175,()=>Request(LobbyAction.Close,_state.CurrentLobbyId));
        _leaveParty=Button(this,"Leave party",384,baseY+101,Math.Min(180,Width-400),()=>Request(LobbyAction.Leave));
        _message=Label(this,"LobbyMessage",16,baseY+139,width,44,Cream);
        _message.WrappingBehavior=WrappingBehavior.Wrapped;
        _filter.ItemSelected+=(_,_)=>RefreshRows(); _search.TextChanged+=(_,_)=>RefreshRows();
        _activity.ItemSelected+=(_,_)=>UpdateCreation();
        _filter.SelectByUserData(-1); _activity.SelectByUserData((int)LobbyActivity.Dungeon);
        Hide();
    }
    protected override void EnsureInitialized() { }
    internal void Open() { Show(); BringToFront(); _nextRefresh=Intersect.Framework.Core.Timing.Global.Milliseconds+10000; Request(LobbyAction.Browse); }
    private static ComboBox Combo(Base parent,string name,int x,int y,int width)
    {
        var result=new ComboBox(parent,name) {Font=GameContentManager.Current.GetFont("sourcesanspro") ?? parent.Skin.DefaultFont,FontSize=11};
        result.SetBounds(x,y,width,28); return result;
    }
    private static Label Label(Base parent,string name,int x,int y,int width,int height,Color color)
    {
        var result=new Label(parent,name) {Font=GameContentManager.Current.GetFont("sourcesanspro") ?? parent.Skin.DefaultFont,FontSize=11,TextColorOverride=color,AutoSizeToContents=false,MouseInputEnabled=false};
        result.SetBounds(x,y,width,height); return result;
    }
    private static Button Button(Base parent,string text,int x,int y,int width,Action click)
    {
        var result=new Button(parent,text.Replace(" ","")) {Text=ClientLanguages.Text(text),Font=GameContentManager.Current.GetFont("sourcesanspro") ?? parent.Skin.DefaultFont,FontSize=11};
        result.SetBounds(x,y,width,30); result.Clicked+=(_,_)=>click(); return result;
    }
    private static void Request(LobbyAction action,Guid id=default) => PacketSender.SendPartyLobby(action,id);
    private void Create() => PacketSender.SendPartyLobby(LobbyAction.Create,activity:(LobbyActivity)(_activity.SelectedItem?.UserData as int? ?? 0),dungeon:_dungeon.SelectedItem?.UserData as Guid? ?? Guid.Empty,title:_title.Text);
    private void UpdateCreation()
    {
        var dungeon=(_activity.SelectedItem?.UserData as int? ?? 0)==(int)LobbyActivity.Dungeon;
        _dungeon.IsHidden=_dungeonLabel.IsHidden=!dungeon;
        _create.IsDisabled=_state.CurrentLobbyId!=Guid.Empty || Globals.Me?.Party is {Count:>0} party && party[0].Id!=Globals.Me.Id || dungeon && _dungeon.SelectedItem==null;
        _leaveParty.IsDisabled=_state.CurrentLobbyId==Guid.Empty;
        _closeListing.IsDisabled=!_state.Lobbies.Any(l=>l.Id==_state.CurrentLobbyId && l.HostId==Globals.Me?.Id);
    }
    internal void Apply(PartyLobbyStatePacket state)
    {
        _state=state; _applying=true;
        var selected=_dungeon.SelectedItem?.UserData as Guid?;
        _dungeon.ClearItems();
        foreach(var dungeon in state.Dungeons) _dungeon.AddItem(dungeon.MaximumLevel>0 ? ClientLanguages.Format("{0} (Lv. {1}-{2})",ClientLanguages.ContentText(dungeon.Name),dungeon.MinimumLevel,dungeon.MaximumLevel) : ClientLanguages.Format("{0} (Lv. {1}+)",ClientLanguages.ContentText(dungeon.Name),dungeon.MinimumLevel),userData:dungeon.Id);
        if(selected.HasValue) _dungeon.SelectByUserData(selected.Value);
        if(_dungeon.SelectedItem==null && state.Dungeons.Length>0) _dungeon.SelectByUserData(state.Dungeons[0].Id);
        _message.Text=ClientLanguages.Text(state.Message);
        _applying=false; UpdateCreation(); RefreshRows();
    }
    internal static bool Matches(PartyLobbyEntry entry,int activity,string search) =>
        (activity<0 || (int)entry.Activity==activity) &&
        (string.IsNullOrWhiteSpace(search) || new[]{entry.HostName,entry.Title,ClientLanguages.ContentText(entry.DungeonName)}.Any(s=>s.Contains(search.Trim(),StringComparison.OrdinalIgnoreCase)));
    private void RefreshRows()
    {
        if(_applying) return;
        _rows.DeleteAll();
        var entries=_state.Lobbies.Where(l=>Matches(l,_filter.SelectedItem?.UserData as int? ?? -1,_search.Text)).ToArray();
        _summary.Text=ClientLanguages.Format("{0} lobbies - Find companions and use party chat",entries.Length);
        var width=_rows.Width-22; var actionX=width-122; var y=0;
        foreach(var lobby in entries)
        {
            var card=new LobbyCard(_rows,"Lobby"+lobby.Id,lobby.Id==_state.CurrentLobbyId); card.SetBounds(0,y,width,94);
            var textWidth=actionX-20;
            Label(card,"Host",10,5,textWidth,24,Gold).Text=lobby.HostName+"  -  "+lobby.Players+" / "+lobby.Capacity;
            var title=Label(card,"Title",10,29,textWidth,24,Cream); title.Text=string.IsNullOrEmpty(lobby.Title)?ActivityName(lobby.Activity):lobby.Title;
            var activity=Label(card,"Activity",10,54,textWidth,30,Cream);
            activity.Text=ActivityName(lobby.Activity)+(string.IsNullOrEmpty(lobby.DungeonName)?"":" - "+ClientLanguages.ContentText(lobby.DungeonName));
            card.SetToolTipText(string.Join(", ",lobby.Members)+"\n"+lobby.Title+"\n"+activity.Text);
            var mine=lobby.Id==_state.CurrentLobbyId;
            var action=Button(card,mine?"Meet host":"Join lobby",actionX,14,112,()=>Request(mine?LobbyAction.Travel:LobbyAction.Join,lobby.Id));
            action.IsDisabled=lobby.Started || !mine && (_state.CurrentLobbyId!=Guid.Empty || lobby.Players>=lobby.Capacity);
            Label(card,"State",actionX,50,112,28,lobby.Started?new Color(255,240,140,115):Gold).Text=ClientLanguages.Text(lobby.Started?"Started - locked":lobby.Players>=lobby.Capacity?"Full":"Recruiting");
            y+=101;
        }
        if(entries.Length==0) Label(_rows,"Empty",10,24,width-20,48,Cream).Text=ClientLanguages.Text("No matching lobbies. Create one to gather your party.");
        _rows.SetInnerSize(width,Math.Max(y,_rows.Height-4));
    }
    public override void Think()
    {
        base.Think();
        if(!IsHidden && Intersect.Framework.Core.Timing.Global.Milliseconds>=_nextRefresh)
        { _nextRefresh=Intersect.Framework.Core.Timing.Global.Milliseconds+10000; Request(LobbyAction.Browse); }
    }
    private sealed class LobbyCard(Base parent,string name,bool current) : Base(parent,name)
    {
        protected override void RenderUnder(Intersect.Client.Framework.Gwen.Skin.Base skin)
        {
            skin.Renderer.DrawColor=current?new Color(255,74,51,34):new Color(255,42,31,25);
            skin.Renderer.DrawFilledRect(RenderBounds);
        }
    }
}
