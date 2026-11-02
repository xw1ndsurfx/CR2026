using DarkUI.Forms;
using Intersect.Framework.Core;
using Intersect.Network.Packets.PartyLobbies;
using Newtonsoft.Json;
namespace Intersect.Editor.Forms.Editors;
public sealed class FrmLobbyConfiguration : DarkForm
{
    private readonly LobbyConfiguration _working;
    private readonly List<(LobbyPresentation Value,TextBox Image,NumericUpDown Maximum)> _rows=[];
    private static string T(string s)=>Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get(s,System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    public FrmLobbyConfiguration(string json)
    {
        _working=JsonConvert.DeserializeObject<LobbyConfiguration>(json)??new();Text=T("Lobby Editor");Size=new Size(850,660);MinimumSize=new Size(700,450);StartPosition=FormStartPosition.CenterParent;
        var grid=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=3,AutoScroll=true,Padding=new Padding(12)};
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,30));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,20));
        foreach(var title in new[]{"Activity","Image filename (GUI)","Maximum party size"})grid.Controls.Add(new Label{Text=T(title),AutoSize=true});
        foreach(var activity in Enum.GetValues<LobbyActivity>())
        {
            var value=_working.Activities.FirstOrDefault(a=>a.Activity==activity);
            if(value==null)_working.Activities.Add(value=new(){Activity=activity});
            var name=activity switch { LobbyActivity.RollTheDice=>"Roll the Dice",LobbyActivity.WorldBoss=>"World Boss",LobbyActivity.HouseParty=>"House Party",LobbyActivity.Naval=>"Naval crew",LobbyActivity.Jousting=>"Royal Jousting",_=>activity.ToString()};
            grid.Controls.Add(new Label{Text=T(name),AutoSize=true,Anchor=AnchorStyles.Left});
            var image=new TextBox {Text=value.Image,Dock=DockStyle.Fill,MaxLength=160};grid.Controls.Add(image);
            var max=new NumericUpDown{Minimum=1,Maximum=100,Value=value.MaximumMembers,Dock=DockStyle.Fill};grid.Controls.Add(max);_rows.Add((value,image,max));
        }
        var help=new Label{Dock=DockStyle.Top,Height=58,Padding=new Padding(12),Text=T("Place lobby images in the client GUI folder. The party limit also respects dungeon, ship and server limits.")};
        var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=44,FlowDirection=FlowDirection.RightToLeft};
        var close=new Button{Text=T("Close"),Width=120};close.Click+=(_,_)=>Close();footer.Controls.Add(close);
        var save=new Button{Text=T("Save"),Width=120};save.Click+=(_,_)=>
        {
            foreach(var row in _rows){row.Value.Image=row.Image.Text.Trim();row.Value.MaximumMembers=(int)row.Maximum.Value;}
            try{_working.Validate();Networking.Network.SendPacket(new LobbyConfigurationRequestPacket{Save=true,Json=JsonConvert.SerializeObject(_working)});}
            catch{MessageBox.Show(this,T("Invalid lobby settings."));}
        };footer.Controls.Add(save);Controls.Add(grid);Controls.Add(footer);Controls.Add(help);
    }
}
