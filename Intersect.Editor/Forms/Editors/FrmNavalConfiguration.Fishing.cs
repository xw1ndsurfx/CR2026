using Intersect.Framework.Core.Naval;
namespace Intersect.Editor.Forms.Editors;
public sealed partial class FrmNavalConfiguration
{
    private Control FishingZoneField(ShipDefinition ship,int section)
    {
        var panel=new Panel {Dock=DockStyle.Fill};
        var refresh=new Button {Dock=DockStyle.Right,Width=100,Text=Intersect.Editor.Localization.FishingEditorText.Get("Refresh zones")};
        var box=new ComboBox {Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,DropDownWidth=400};
        bool loading=false;
        void Reload()
        {
            if(panel.IsDisposed)return;loading=true;
            try
            {
                var choices=new List<Choice>{new(Guid.Empty,T("Automatic / not selected"))};
                choices.AddRange(FishingEditorState.Configuration.Zones.OrderBy(z=>z.Name).Select(z=>new Choice(z.Id,z.Name)));
                if(!choices.Any(c=>c.Id==ship.FishingZoneId))choices.Add(new(ship.FishingZoneId,ship.FishingZoneId.ToString()));
                box.Items.Clear();box.Items.AddRange(choices.ToArray());box.SelectedItem=choices.First(c=>c.Id==ship.FishingZoneId);
                refresh.Enabled=!FishingEditorState.IsRefreshing;
            }
            finally{loading=false;}
        }
        box.SelectedIndexChanged+=(_,_)=>{if(!loading && box.SelectedItem is Choice choice){ship.FishingZoneId=choice.Id;Changed(section);}};
        refresh.Click+=(_,_)=>FishingEditorState.Request();
        panel.Controls.Add(box);panel.Controls.Add(refresh);
        FishingEditorState.Changed+=Reload;panel.Disposed+=(_,_)=>FishingEditorState.Changed-=Reload;
        Reload();
        if(Networking.Network.EditorLidgrenNetwork!=null && !FishingEditorState.HasConfiguration)FishingEditorState.Request();
        return panel;
    }
}
