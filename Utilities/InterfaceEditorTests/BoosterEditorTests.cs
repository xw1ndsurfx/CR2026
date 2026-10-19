using System.Reflection;
using Intersect.Editor.Forms.Editors;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.WorldEvents;
internal static class BoosterEditorTests
{
    internal static void Run()
    {
        using(var events=new FrmWorldActivities("{}",WorldActivitiesSection.Loot))
        {
            var grid=events.Controls.OfType<DataGridView>().Single();if(!events.Text.Contains("Loot") || !grid.Columns.Cast<DataGridViewColumn>().Any(c=>c.HeaderText=="Extra loot chance (%)"))throw new Exception("Loot events need independent schedule and percentage controls");
        }
        using(var shop=new FrmShop())
        {
            var field=typeof(FrmShop).GetField("_buybackDiscount",BindingFlags.Instance|BindingFlags.NonPublic)!;var input=(NumericUpDown)field.GetValue(shop)!;if(input.Maximum!=99 || input.Right>input.Parent!.ClientSize.Width || input.Bottom>input.Parent.ClientSize.Height)throw new Exception("Buyback discount must fit in Shop Editor");
        }
        using(var editor=new FrmItem())
        {
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var item=new ItemDescriptor(Guid.NewGuid());typeof(FrmItem).GetField("mEditorItem",flags)!.SetValue(editor,item);
            typeof(FrmItem).GetMethod("InitLocalization",flags)!.Invoke(editor,null);
            var choice=(ComboBox)typeof(FrmItem).GetField("cmbConsume",flags)!.GetValue(editor)!;
            if(choice.Items.Count!=4)throw new Exception("EXP boost potion must appear as a fourth consumable effect");
            choice.SelectedIndex=(int)ConsumableType.ExperienceBoost;
            var seconds=(NumericUpDown)typeof(FrmItem).GetField("nudInterval",flags)!.GetValue(editor)!;
            var percent=(NumericUpDown)typeof(FrmItem).GetField("nudIntervalPercentage",flags)!.GetValue(editor)!;
            seconds.Value=1800;percent.Value=50;
            if(seconds.Maximum!=604800 || percent.Maximum!=1000 || item.Consumable.Value!=1800 || item.Consumable.Percentage!=50)throw new Exception("EXP potion editor must store duration and percentage independently");
        }
        var flavors=typeof(FrmItem).Assembly.GetType("Intersect.Editor.Forms.Editors.ItemFlavorDescription",true)!;var build=flavors.GetMethod("Build",BindingFlags.Static|BindingFlags.NonPublic)!;
        foreach(var name in new[]{"Sword of Oak","Iron Axe","Hunter Bow","Staff of Dawn","Shield","Boots","Helmet","Golden Ring","Tunic"})
        {
            var description=(string)build.Invoke(null,[name,ItemType.Equipment,ConsumableType.Health])!;if(description.Contains("Auto Creator") || description.Any(char.IsDigit) || description.Contains("Damage:"))throw new Exception("Generated descriptions must be narrative without tool markers or stats");
        }
        var sword=(string)build.Invoke(null,["Sword",ItemType.Equipment,ConsumableType.Health])!;var bag=(string)build.Invoke(null,["Bag",ItemType.Bag,ConsumableType.Health])!;if(sword==bag || !sword.Contains("blade"))throw new Exception("Descriptions should correspond to the object");
        System.Console.WriteLine("PASS independent loot schedule editor, visible shop discount and object-specific narrative descriptions");
    }
}
