using Intersect.Framework.Core.Naval;
using Newtonsoft.Json;
namespace Intersect.Editor.Forms.Editors;
public sealed partial class FrmNavalConfiguration
{
    private Control DamageZoneField(ShipDefinition ship,int section)
    {
        var button=new Button {Text=T("Configure damage zones..."),Dock=DockStyle.Fill};
        button.Click+=(_,_)=>
        {
            var zones=JsonConvert.DeserializeObject<List<ShipDamageZone>>(JsonConvert.SerializeObject(ship.DamageZones))??[];
            if(zones.Count==0 && ship.UseDefaultDamageZones)zones=ShipDamageZone.Defaults().ToList();
            using var dialog=new Form {Text=T("Damage zones"),ClientSize=new(790,480),MinimumSize=new(680,440),StartPosition=FormStartPosition.CenterParent,ShowInTaskbar=false};
            var split=new SplitContainer {Size=new(790,438),Dock=DockStyle.Fill,SplitterDistance=210};
            var list=new ListBox {Dock=DockStyle.Fill};split.Panel1.Controls.Add(list);
            var buttons=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=42,FlowDirection=FlowDirection.RightToLeft};
            var save=new Button {Text=T("Save"),Width=100};var cancel=new Button {Text=T("Cancel"),Width=100,DialogResult=DialogResult.Cancel};
            var add=new Button {Text=T("Add"),Width=100};var remove=new Button {Text=T("Remove"),Width=100};
            buttons.Controls.AddRange([save,cancel,remove,add]);dialog.Controls.Add(split);dialog.Controls.Add(buttons);
            void Populate(int index)
            {
                list.Items.Clear();foreach(var zone in zones)list.Items.Add(zone.Id);
                if(zones.Count>0)list.SelectedIndex=Math.Clamp(index,0,zones.Count-1);
                add.Enabled=zones.Count<16;remove.Enabled=zones.Count>0;
            }
            list.SelectedIndexChanged+=(_,_)=>
            {
                foreach(Control child in split.Panel2.Controls.Cast<Control>().ToArray())child.Dispose();
                if(list.SelectedIndex>=0)split.Panel2.Controls.Add(ScrollFields(Fields(zones[list.SelectedIndex],section,["Id"])));
                MiniGameEditorTheme.Apply(split.Panel2);
            };
            add.Click+=(_,_)=>{if(zones.Count>=16)return;zones.Add(new());Populate(zones.Count-1);};
            remove.Click+=(_,_)=>{if(list.SelectedIndex<0)return;var index=list.SelectedIndex;zones.RemoveAt(index);Populate(index);};
            save.Click+=(_,_)=>
            {
                if(zones.Any(z=>!z.IsValid())){MessageBox.Show(dialog,T("Choose valid damage coordinates, hull thresholds and repair settings."),T("Damage zones"));return;}
                ship.DamageZones=zones.ToArray();if(zones.Count==0)ship.UseDefaultDamageZones=false;
                Changed(section);dialog.DialogResult=DialogResult.OK;
            };
            Populate(0);MiniGameEditorTheme.Apply(dialog);dialog.ShowDialog(this);
        };
        return button;
    }
}
