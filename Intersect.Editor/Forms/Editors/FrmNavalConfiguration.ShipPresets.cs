using Intersect.Framework.Core.Naval;
namespace Intersect.Editor.Forms.Editors;
public sealed partial class FrmNavalConfiguration
{
    private NavalShipModel? PickShipModel()
    {
        using var dialog=new Form {Text=T("Choose a ship appearance"),ClientSize=new(560,185),StartPosition=FormStartPosition.CenterParent,
            FormBorderStyle=FormBorderStyle.FixedDialog,ShowInTaskbar=false,MinimizeBox=false,MaximizeBox=false};
        var box=new ComboBox {Name="ShipPresetModel",Bounds=new(16,16,528,28),DropDownStyle=ComboBoxStyle.DropDownList,FormattingEnabled=true};
        box.Format+=(_,e)=>{if(e.ListItem is NavalShipModel model)e.Value=T(model.Caption);};
        box.Items.AddRange(NavalShipModels.All.Cast<object>().ToArray());box.SelectedIndex=0;
        var info=new Label {Bounds=new(16,56,528,65),Text=T("Sets the four visual states, crew capacity and deck stations. Configure prices, access requirements and map placement before saving.")};
        var buttons=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=44,FlowDirection=FlowDirection.RightToLeft};
        var apply=new Button {Text=T("Use this model"),AutoSize=true,DialogResult=DialogResult.OK};
        var cancel=new Button {Text=T("Cancel"),AutoSize=true,DialogResult=DialogResult.Cancel};
        buttons.Controls.AddRange([apply,cancel]);dialog.Controls.AddRange([box,info,buttons]);
        dialog.AcceptButton=apply;dialog.CancelButton=cancel;MiniGameEditorTheme.Apply(dialog);
        return dialog.ShowDialog(this)==DialogResult.OK?box.SelectedItem as NavalShipModel:null;
    }
}
