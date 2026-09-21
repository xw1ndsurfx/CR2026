using System.Drawing;
using System.Windows.Forms;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.MiniGames;
using DrawingColor=System.Drawing.Color;
namespace Intersect.Editor.Forms.Editors.Events;

/// <summary>Shared event configuration. Cancel never mutates the original command.</summary>
internal sealed class MiniGameCommandDialog : Form
{
    private sealed record AnimationChoice(Guid Id,string Name){public override string ToString()=>Name;}
    private sealed record CurrencyChoice(Guid Id,string Name){public override string ToString()=>Name;}
    public MiniGameCommandDialog(StartMiniGameCommand command)
    {
        Text="Start Mini-Game";StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.Sizable;
        MaximizeBox=MinimizeBox=false;ShowInTaskbar=false;AutoScaleMode=AutoScaleMode.Font;
        ClientSize=new Size(680,Math.Min(740,Math.Max(480,(Screen.PrimaryScreen?.WorkingArea.Height??900)-140)));
        MinimumSize=new Size(580,420);BackColor=DrawingColor.FromArgb(45,45,48);ForeColor=DrawingColor.Gainsboro;
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12),ColumnCount=2,RowCount=21,AutoScroll=true};
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,42));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,58));
        for(var r=0;r<21;r++)layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var buttons=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Bottom,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(12,8,12,8)};
        Controls.Add(layout);Controls.Add(buttons);
        var hint=new Label{AutoSize=true,MaximumSize=new Size(610,0),Margin=new Padding(3,3,3,12),Text="Same map instance + mini-game + Table ID = shared table. Use identical settings on all access events. "+
            "25 XP per positive-net round; each mini-game has its own levels and B1-B6 unlocks. Show the buy-in in a Yes/No event before this command."};
        layout.Controls.Add(hint,0,0);layout.SetColumnSpan(hint,2);
        var game=new ComboBox{Name="MiniGameType",DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
        game.Items.AddRange(new object[]{"Poker - Texas hold'em","Blackjack - versus dealer"});game.SelectedIndex=(int)command.Game is 0 or 1?(int)command.Game:0;
        var table=new TextBox{Name="TableId",Text=command.TableId??"",MaxLength=64,Dock=DockStyle.Fill};
        var seats=Number(command.MaxPlayers,2,6);seats.Name="MaximumSeats";
        var currency=CurrencyPicker(command.CurrencyItemId);var chips=Number(command.StartingChips,1,1_000_000_000);
        var reserve=Number(command.NpcReserve,0,1_000_000_000);reserve.Name="NpcReserve";
        var small=Number(command.SmallBlind,1,1_000_000_000);var big=Number(command.BigBlind,1,1_000_000_000);
        var seconds=Number(command.TurnSeconds,5,300);
        var dealer=new CheckBox{Text="Marlow / Croupier",Checked=command.DealerPlays,AutoSize=true};
        var npcs=Number(command.NpcPlayers,0,5);npcs.Name="NpcPlayers";
        var automatic=new CheckBox{Text="Next round after 5 seconds",Checked=command.AutoStart,AutoSize=true};
        var animation=AnimationPicker(command.DealAnimationId);var victory=AnimationPicker(command.VictoryAnimationId);
        var announce=new CheckBox{Text="Human name + positive net win",Checked=command.AnnounceWins,AutoSize=true};
        var backs=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
        for(var id=0;id<6;id++)backs.Items.Add($"B{id+1} (B{id+1}.png)");backs.SelectedIndex=Math.Clamp(command.NpcCardBackId,0,5);
        var minimum=Number(command.BlackjackMinimumBet,2,1_000_000_000);minimum.Name="BlackjackMinimumBet";minimum.Increment=2;
        var maximum=Number(command.BlackjackMaximumBet,2,1_000_000_000);maximum.Name="BlackjackMaximumBet";maximum.Increment=2;
        var soft17=new CheckBox{Name="BlackjackHitSoft17",Text="Hit soft 17 (unchecked = stand)",AutoSize=true,Checked=command.BlackjackHitSoft17};
        void LimitNpcs(){var m=seats.Value-1-(game.SelectedIndex==1 || dealer.Checked?1:0);if(npcs.Value>m)npcs.Value=m;npcs.Maximum=m;}
        seats.ValueChanged+=(_,_)=>LimitNpcs();dealer.CheckedChanged+=(_,_)=>LimitNpcs();
        AddRow(layout,1,"Mini-game",game);AddRow(layout,2,"Table ID (letters, digits, - or _)",table);
        AddRow(layout,3,"Maximum seats (includes dealer)",seats);AddRow(layout,4,"Table currency / Monnaie",currency);
        var chipsLabel=AddRow(layout,5,"Starting test chips",chips);
        AddRow(layout,6,"Poker: small blind",small);AddRow(layout,7,"Poker: big blind",big);
        AddRow(layout,8,"Betting / turn timeout (seconds)",seconds);AddRow(layout,9,"Poker: dealer plays and deals",dealer);
        AddRow(layout,10,"Other NPC opponents",npcs);AddRow(layout,11,"Automatic rounds",automatic);
        AddRow(layout,12,"Dealing animation",animation);AddRow(layout,13,"Announce wins in GLOBAL chat",announce);
        AddRow(layout,14,"Victory animation (winner only)",victory);AddRow(layout,15,"Dealer / NPC card back",backs);
        AddRow(layout,16,"Initial house / NPC reserve (once)",reserve);
        AddRow(layout,17,"Blackjack: minimum bet (even)",minimum);AddRow(layout,18,"Blackjack: maximum bet (even)",maximum);
        AddRow(layout,19,"Blackjack: dealer rule",soft17);
        var status=new Label{Name="CurrencyStatus",AutoSize=true,MaximumSize=new Size(610,0),Margin=new Padding(3,12,3,12)};
        layout.Controls.Add(status,0,20);layout.SetColumnSpan(status,2);
        void RefreshSettings()
        {
            var blackjack=game.SelectedIndex==1;
            small.Enabled=big.Enabled=dealer.Enabled=!blackjack;minimum.Enabled=maximum.Enabled=soft17.Enabled=blackjack;LimitNpcs();
            var id=(currency.SelectedItem as CurrencyChoice)?.Id??Guid.Empty;reserve.Enabled=id!=Guid.Empty;
            chipsLabel.Text=id==Guid.Empty?"Starting test chips":"Buy-in (inventory item units)";
            status.ForeColor=id==Guid.Empty?DrawingColor.Gainsboro:DrawingColor.Gold;
            status.Text=id==Guid.Empty?"TEST mode: no inventory items are taken or paid. Test progression stays separate.":
                !MiniGameCurrency.IsCompatible(ItemDescriptor.Get(id))?"The selected item is missing or incompatible. Choose another item or test chips.":
                $"Selected item: {ItemDescriptor.GetName(id)}. ID: {id}.\nFUNDED mode (SQLite): buy-in is taken once; the remaining balance is returned after the round. "+
                "The house budget is created ONCE, shared across map instances. Reopening does not refill it. Back up the entire player database.";
            if(blackjack)status.Text+="\nBlackjack: 6 decks per round, 3:2 natural, 1:1 other wins, one split, double after split, one card to split aces. "+
                "No insurance/surrender. Maximum bet must not exceed buy-in. The dealer always plays; funded tables require a house reserve even with no NPC guests.";
        }
        game.SelectedIndexChanged+=(_,_)=>RefreshSettings();currency.SelectedIndexChanged+=(_,_)=>RefreshSettings();RefreshSettings();
        var cancel=new Button{Name="Cancel",Text="Cancel",AutoSize=true,DialogResult=DialogResult.Cancel};
        var save=new Button{Name="Save",Text="Save",AutoSize=true};buttons.Controls.Add(cancel);buttons.Controls.Add(save);AcceptButton=save;CancelButton=cancel;
        save.Click+=(_,_)=>
        {
            var id=(currency.SelectedItem as CurrencyChoice)?.Id??Guid.Empty;
            if(id!=Guid.Empty && !MiniGameCurrency.IsCompatible(ItemDescriptor.Get(id)))
            {MessageBox.Show(this,"Choose a valid Currency or stackable item.","Invalid currency",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
            var draft=new StartMiniGameCommand{Game=(MiniGameType)game.SelectedIndex,TableId=table.Text,MaxPlayers=(int)seats.Value,
                CurrencyItemId=id,StartingChips=(long)chips.Value,NpcReserve=(long)reserve.Value,SmallBlind=(long)small.Value,BigBlind=(long)big.Value,
                TurnSeconds=(int)seconds.Value,DealerPlays=dealer.Checked,NpcPlayers=(int)npcs.Value,AutoStart=automatic.Checked,
                DealAnimationId=((AnimationChoice)animation.SelectedItem!).Id,VictoryAnimationId=((AnimationChoice)victory.SelectedItem!).Id,
                AnnounceWins=announce.Checked,NpcCardBackId=backs.SelectedIndex,BlackjackMinimumBet=(long)minimum.Value,
                BlackjackMaximumBet=(long)maximum.Value,BlackjackHitSoft17=soft17.Checked};
            if(!draft.HasValidSettings())
            {MessageBox.Show(this,"Check table ID, seat limits and betting amounts. Blackjack initial bets must be even, with minimum <= maximum <= buy-in.","Invalid table",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
            command.Game=draft.Game;command.TableId=draft.TableId;command.MaxPlayers=draft.MaxPlayers;command.CurrencyItemId=draft.CurrencyItemId;
            command.StartingChips=draft.StartingChips;command.NpcReserve=draft.NpcReserve;command.SmallBlind=draft.SmallBlind;command.BigBlind=draft.BigBlind;
            command.TurnSeconds=draft.TurnSeconds;command.DealerPlays=draft.DealerPlays;command.NpcPlayers=draft.NpcPlayers;command.AutoStart=draft.AutoStart;
            command.DealAnimationId=draft.DealAnimationId;command.VictoryAnimationId=draft.VictoryAnimationId;command.AnnounceWins=draft.AnnounceWins;
            command.NpcCardBackId=draft.NpcCardBackId;command.BlackjackMinimumBet=draft.BlackjackMinimumBet;
            command.BlackjackMaximumBet=draft.BlackjackMaximumBet;command.BlackjackHitSoft17=draft.BlackjackHitSoft17;
            DialogResult=DialogResult.OK;Close();
        };
    }
    private static ComboBox CurrencyPicker(Guid id)
    {
        var p=new ComboBox{Name="TableCurrency",DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill,DropDownWidth=590,
            AutoCompleteSource=AutoCompleteSource.ListItems,AutoCompleteMode=AutoCompleteMode.SuggestAppend};
        p.Items.Add(new CurrencyChoice(Guid.Empty,"None / Test chips (no inventory)"));
        foreach(var i in MiniGameCurrency.CompatibleItems(ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>()))p.Items.Add(new CurrencyChoice(i.Id,MiniGameCurrency.DisplayName(i)));
        var selected=p.Items.Cast<CurrencyChoice>().FirstOrDefault(c=>c.Id==id);
        if(selected==null){selected=new CurrencyChoice(id,"Missing / incompatible item: "+id);p.Items.Add(selected);}p.SelectedItem=selected;return p;
    }
    private static ComboBox AnimationPicker(Guid id)
    {
        var p=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};p.Items.Add(new AnimationChoice(Guid.Empty,"None / Aucune"));
        foreach(var i in AnimationDescriptor.Lookup.Values.OfType<AnimationDescriptor>().OrderBy(a=>a.Name,StringComparer.OrdinalIgnoreCase))p.Items.Add(new AnimationChoice(i.Id,i.Name));
        var selected=p.Items.Cast<AnimationChoice>().FirstOrDefault(c=>c.Id==id);
        if(selected==null){selected=new AnimationChoice(id,"Missing animation: "+id);p.Items.Add(selected);}p.SelectedItem=selected;return p;
    }
    private static NumericUpDown Number(long value,long min,long max)=>new(){Minimum=min,Maximum=max,Value=Math.Clamp(value,min,max),DecimalPlaces=0,ThousandsSeparator=true,Dock=DockStyle.Fill};
    private static Label AddRow(TableLayoutPanel p,int row,string text,Control input)
    {var l=new Label{Text=text,AutoSize=true,Anchor=AnchorStyles.Left,Margin=new Padding(3,8,3,8)};p.Controls.Add(l,0,row);input.Margin=new Padding(3,6,3,6);p.Controls.Add(input,1,row);return l;}
}
