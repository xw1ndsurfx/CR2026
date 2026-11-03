using DarkUI.Forms;
using Intersect.Editor.Forms.Styling;
using Intersect.Framework.Core;
using Intersect.Network.Packets.PartyLobbies;
using Newtonsoft.Json;
namespace Intersect.Editor.Forms.Editors;
public sealed class FrmLobbyConfiguration : DarkForm
{
    private readonly LobbyConfiguration _working;
    private readonly List<(LobbyPresentation Value,ComboBox Image,NumericUpDown Maximum)> _rows=[];
    private static string T(string s)=>Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get(s,System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    public FrmLobbyConfiguration(string json)
    {
        _working=JsonConvert.DeserializeObject<LobbyConfiguration>(json)??new();Text=T("Lobby Editor");Size=new Size(850,660);MinimumSize=new Size(700,450);StartPosition=FormStartPosition.CenterParent;
        var activities=Enum.GetValues<LobbyActivity>();
        var viewport=new Panel { Dock=DockStyle.Fill,AutoScroll=true };
        var grid=new TableLayoutPanel { Name="LobbyActivities",Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=3,RowCount=activities.Length+1,Padding=new Padding(12) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,30));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,20));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute,32));
        var titles=new[]{"Activity","Image","Maximum party size"};
        for(var column=0;column<3;column++)grid.Controls.Add(new Label {Text=T(titles[column]),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},column,0);
        for(var index=0;index<activities.Length;index++)
        {
            var activity=activities[index];var row=index+1;grid.RowStyles.Add(new RowStyle(SizeType.Absolute,44));
            var value=_working.Activities.FirstOrDefault(a=>a.Activity==activity);
            if(value==null)_working.Activities.Add(value=new(){Activity=activity});
            var name=activity switch {LobbyActivity.RollTheDice=>"Roll the Dice",LobbyActivity.WorldBoss=>"World Boss",LobbyActivity.HouseParty=>"House Party",LobbyActivity.Naval=>"Naval crew",LobbyActivity.Jousting=>"Royal Jousting",_=>activity.ToString()};
            grid.Controls.Add(new Label{Text=T(name),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,row);
            var image=new ComboBox {Name="LobbyImage"+activity,DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Top,DropDownWidth=460,Margin=new Padding(3,9,8,3)};
            image.Items.Add("");SearchableChoices.AddTextureNames(image,Intersect.Editor.Content.GameContentManager.TextureType.Gui);
            if(value.Image.Length>0 && !image.Items.Contains(value.Image))image.Items.Add(value.Image);
            image.SelectedIndex=Math.Max(0,image.FindStringExact(value.Image));grid.Controls.Add(image,1,row);
            var max=new NumericUpDown{Minimum=1,Maximum=100,Value=value.MaximumMembers,Dock=DockStyle.Top,Margin=new Padding(3,9,3,3)};grid.Controls.Add(max,2,row);_rows.Add((value,image,max));
        }
        viewport.Controls.Add(grid);
        var help=new Label {Dock=DockStyle.Top,Height=58,Padding=new Padding(12),Text=T("Choose lobby images using the image selector and preview. Party size also respects dungeon, ship and server limits.")};
        var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=44,FlowDirection=FlowDirection.RightToLeft};
        var close=new Button{Text=T("Close"),Width=120};close.Click+=(_,_)=>Close();footer.Controls.Add(close);
        var save=new Button{Text=T("Save"),Width=120};save.Click+=(_,_)=>
        {
            foreach(var row in _rows){row.Value.Image=row.Image.Text.Trim();row.Value.MaximumMembers=(int)row.Maximum.Value;}
            try{_working.Validate();Networking.Network.SendPacket(new LobbyConfigurationRequestPacket{Save=true,Json=JsonConvert.SerializeObject(_working)});}
            catch{MessageBox.Show(this,T("Invalid lobby settings."));}
        };footer.Controls.Add(save);Controls.Add(viewport);Controls.Add(footer);Controls.Add(help);EditorTheme.Apply(this);
    }
}
