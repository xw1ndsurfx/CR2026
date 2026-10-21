using System.Reflection;
using Intersect.Editor.Forms.Editors;
using Intersect.Framework.Core.Achievements;
using Intersect.Framework.Core.GameObjects.Items;
internal static class AchievementRewardEditorTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static object Field(object instance,string name)=>instance.GetType().GetField(name,Hidden)!.GetValue(instance)!;
    private static void Call(object instance,string name,params object?[] values)=>instance.GetType().GetMethod(name,Hidden)!.Invoke(instance,values);
    private static Guid Selected(object instance)=>(Guid)instance.GetType().GetProperty("SelectedId",Hidden)!.GetValue(instance)!;
    internal static void Run()
    {
        var previous=AchievementConfiguration.Instance.ToJson();
        var icon="achievement-picker-"+Guid.NewGuid().ToString("N")+".png";
        var path=Path.Combine("resources","items",icon);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using(var image=new Bitmap(32,32)){using var graphics=Graphics.FromImage(image);graphics.Clear(System.Drawing.Color.Goldenrod);image.Save(path);}
        var first=new ItemDescriptor(Guid.NewGuid()){Name="Fixture Wooden Shield",Icon=icon,Description="Fixture description",ItemType=ItemType.Equipment};
        var second=new ItemDescriptor(Guid.NewGuid()){Name=first.Name,ItemType=ItemType.Equipment};
        var currency=new ItemDescriptor(Guid.NewGuid()){Name="Fixture Aureons",ItemType=ItemType.Currency};
        foreach(var item in new[]{first,second,currency})ItemDescriptor.Lookup.Set(item.Id,item);
        try
        {
            var achievement=new AchievementDefinition {Name="Fixture reward",Reward=new(){ItemId=first.Id,ItemQuantity=7,Experience=123,CurrencyItemId=currency.Id,CurrencyQuantity=50}};
            AchievementConfiguration.Load(new AchievementConfiguration{Achievements=[achievement,new(){Name="Second fixture"}]}.ToJson());
            using var editor=new FrmAchievementConfiguration {Opacity=0,ShowInTaskbar=false};editor.Show();Application.DoEvents();
            var selector=Field(editor,"_rewardItem");var moneySelector=Field(editor,"_rewardCurrency");
            Check(Selected(selector)==first.Id && Selected(moneySelector)==currency.Id,"Saved item and currency IDs must load into their preview controls.");
            var moneyItems=(ItemDescriptor[])Field(moneySelector,"_items");Check(moneyItems.All(item=>item.ItemType==ItemType.Currency),"Currency choices must contain only currency items.");
            Check(All((Control)selector).OfType<PictureBox>().Single().Image!=null,"Selected reward must display its item icon.");
            using(File.Open(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){}
            var tabs=All(editor).OfType<TabControl>().First();tabs.SelectedIndex=1;Application.DoEvents();
            if(Environment.GetEnvironmentVariable("CODEX_ACHIEVEMENT_PREVIEW") is {Length:>0} output)
            {using var image=new Bitmap(editor.Width,editor.Height);editor.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(output);}
            var dialogType=typeof(FrmAchievementConfiguration).Assembly.GetType("Intersect.Editor.Forms.Editors.AchievementRewardItemDialog")!;
            using var dialog=(Form)Activator.CreateInstance(dialogType,Hidden,null,[new[]{first,second,currency},second.Id],null)!;
            dialog.Opacity=0;dialog.ShowInTaskbar=false;dialog.Show();Application.DoEvents();
            var search=(TextBox)Field(dialog,"_search");var grid=(DataGridView)Field(dialog,"_grid");
            search.Text="WOODEN SHIELD";Application.DoEvents();Check(grid.RowCount==2,"Search must match item names without losing duplicate-name entries.");
            if(Environment.GetEnvironmentVariable("CODEX_ACHIEVEMENT_PREVIEW") is {Length:>0} pickerOutput)
            {using var image=new Bitmap(dialog.Width,dialog.Height);dialog.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(Path.GetDirectoryName(pickerOutput)!,"achievement-reward-picker.png"));}
            search.Text="no matching fixture";Application.DoEvents();Check(grid.RowCount==0 && !((Button)Field(dialog,"_select")).Enabled,"An empty search must not select or grant an unrelated item.");
            search.Text="wooden shield";Application.DoEvents();
            var visible=((IEnumerable<ItemDescriptor?>)Field(dialog,"_visible")).ToArray();var index=Array.FindIndex(visible,item=>item?.Id==second.Id);
            grid.CurrentCell=grid.Rows[index].Cells[1];Call(dialog,"Confirm");Check(Selected(dialog)==second.Id,"Duplicate names must select the intended stable item ID.");
            Call(selector,"SetSelectedId",Selected(dialog));((NumericUpDown)Field(editor,"_rewardItemQuantity")).Value=12;Call(editor,"CommitSelected");
            var saved=(AchievementDefinition)Field(editor,"_selected");Check(saved.Reward.ItemId==second.Id && saved.Reward.ItemQuantity==12 && saved.Reward.Experience==123 && saved.Reward.CurrencyItemId==currency.Id && saved.Reward.CurrencyQuantity==50,"Choosing an item must preserve the other configured rewards.");
            var unknown=Guid.NewGuid();Call(selector,"SetSelectedId",unknown);Call(editor,"CommitSelected");Check(saved.Reward.ItemId==unknown,"A missing authored item ID must remain intact until explicitly changed.");
            Call(selector,"SetSelectedId",Guid.Empty);Call(editor,"CommitSelected");Check(saved.Reward.ItemId==Guid.Empty && saved.Reward.ItemQuantity==0,"Clearing the selected item must disable only that reward.");
            Check(AchievementConfiguration.Instance.Find(achievement.Id)!.Reward.ItemId==first.Id && AchievementConfiguration.Instance.Find(achievement.Id)!.Reward.ItemQuantity==7,"Browsing and editing draft rewards must not change live configuration before Save.");
            editor.Close();System.Console.WriteLine("PASS achievement reward picker: search, icons without file locks, stable IDs, duplicate names, missing items, quantities, currency filter and independent reward preservation");
        }
        finally
        {
            AchievementConfiguration.Load(previous);foreach(var item in new[]{first,second,currency})ItemDescriptor.Lookup.DeleteAt(item.Id);File.Delete(path);
        }
    }
    private static IEnumerable<Control> All(Control root){foreach(Control child in root.Controls){yield return child;foreach(var descendant in All(child))yield return descendant;}}
}
