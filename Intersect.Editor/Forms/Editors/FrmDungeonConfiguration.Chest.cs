using DarkUI.Forms;
using Intersect.Framework.Core.Dungeons;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.QualityOfLife;
namespace Intersect.Editor.Forms.Editors;
public sealed partial class FrmDungeonConfiguration
{
    private static string ChestText(string text)=>QualityOfLifeText.Get(text,System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    private void ConfigureChest()
    {
        if(_selected==null)return;
        var settings=System.Text.Json.JsonSerializer.Deserialize<DungeonCompletionChest>(System.Text.Json.JsonSerializer.Serialize(_selected.Chest))!;
        using var dialog=new DarkForm {Text=ChestText("Completion chest"),Width=960,Height=690,StartPosition=FormStartPosition.CenterParent};
        var enabled=new CheckBox{Text=ChestText("Wait for chest collection before leaving"),Checked=settings.Enabled,Left=20,Top=20,Width=650,ForeColor=System.Drawing.Color.Gainsboro};
        dialog.Controls.Add(enabled);
        ComboBox Animation(string title,int y,Guid selected)
        {
            dialog.Controls.Add(new Label{Text=ChestText(title),Left=20,Top=y+4,Width=240,ForeColor=System.Drawing.Color.Gainsboro});
            var combo=new ComboBox{Left=270,Top=y,Width=650,DropDownStyle=ComboBoxStyle.DropDownList};
            combo.Items.Add(new IdChoice(Guid.Empty,ChestText("None")));
            foreach(var item in AnimationDescriptor.Lookup.Values.OfType<AnimationDescriptor>().OrderBy(item=>item.Name))combo.Items.Add(new IdChoice(item.Id,item.Name));
            combo.SelectedItem=combo.Items.Cast<IdChoice>().FirstOrDefault(item=>item.Id==selected)??combo.Items[0];dialog.Controls.Add(combo);return combo;
        }
        var animation=Animation("Chest animation",60,settings.AnimationId);
        var celebration=Animation("Completion animation",100,settings.CompletionAnimationId);
        dialog.Controls.Add(new Label{Text=ChestText("Collection time (seconds)"),Left=20,Top=145,Width=240,ForeColor=System.Drawing.Color.Gainsboro});
        var seconds=new NumericUpDown{Minimum=15,Maximum=600,Value=Math.Clamp(settings.CollectionSeconds,15,600),Left=270,Top=140,Width=130};dialog.Controls.Add(seconds);
        var list=new System.ComponentModel.BindingList<DungeonChestDrop>(settings.Drops.ToList());
        var grid=new DataGridView{Left=20,Top=185,Width=900,Height=370,Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right,
            AutoGenerateColumns=false,AllowUserToAddRows=true,AllowUserToDeleteRows=true,BackgroundColor=System.Drawing.Color.FromArgb(35,35,40),DataSource=list};
        var items=ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>().Select(item=>new IdChoice(item.Id,item.Name)).OrderBy(item=>item.Text).ToList();
        items.Insert(0,new IdChoice(Guid.Empty,ChestText("None")));
        grid.Columns.Add(new DataGridViewComboBoxColumn{HeaderText=ChestText("Item"),DataPropertyName="ItemId",DataSource=items,DisplayMember="Text",ValueMember="Id",Width=450});
        foreach(var (property,title) in new[]{("Minimum","Minimum quantity"),("Maximum","Maximum quantity"),("Chance","Chance (%)")})
            grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText=ChestText(title),DataPropertyName=property,Width=130});
        grid.DataError+=(_,args)=>args.ThrowException=false;dialog.Controls.Add(grid);
        var save=new Button{Text=ChestText("Save"),Left=710,Top=590,Width=100,Height=32,Anchor=AnchorStyles.Bottom|AnchorStyles.Right};
        var cancel=new Button{Text=ChestText("Cancel"),Left=820,Top=590,Width=100,Height=32,Anchor=AnchorStyles.Bottom|AnchorStyles.Right,DialogResult=DialogResult.Cancel};
        save.Click+=(_,_)=>
        {
            grid.EndEdit();((CurrencyManager)dialog.BindingContext[list]).EndCurrentEdit();
            settings.Enabled=enabled.Checked;settings.AnimationId=((IdChoice)animation.SelectedItem).Id;
            settings.CompletionAnimationId=((IdChoice)celebration.SelectedItem).Id;settings.CollectionSeconds=(int)seconds.Value;settings.Drops=list.ToArray();
            if(!settings.IsValid){MessageBox.Show(dialog,ChestText("Check item quantities and drop chances (0–100%)."));return;}
            _selected.Chest=settings;dialog.DialogResult=DialogResult.OK;
        };
        dialog.Controls.AddRange([save,cancel]);dialog.CancelButton=cancel;dialog.ShowDialog(this);
    }
}
