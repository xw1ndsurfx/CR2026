using System.Reflection;
using Intersect.Editor.Forms.Editors;
using Intersect.Framework.Core.Naval;
using Newtonsoft.Json;

internal static class NavalPatrolCreatorEditorTests
{
    private static IEnumerable<Control> All(Control root)
    {foreach(Control child in root.Controls){yield return child;foreach(var next in All(child))yield return next;}}
    internal static void Run()
    {
        var template=new HerboniaPatrol {Level=4,Ship=new(){MapId=Guid.NewGuid(),X=8.5,Y=9.5,Heading=-90,Name="Patrol fixture",Graphics="custom.png"},
            Loot=[new(){ItemId=Guid.NewGuid()}],BoardingDungeonId=Guid.NewGuid(),BoardingEntryMapId=Guid.NewGuid()};
        var config=new NavalConfiguration {Herbonia=[template]};
        using(var editor=new FrmNavalConfiguration(new(){NavigationJson=JsonConvert.SerializeObject(config),CatalogueJson="{}"}))
        {
            if(!All(editor).OfType<Button>().Any(c=>c.Name=="NavalPatrolAutoCreator"))throw new Exception("Patrol tab must expose the auto creator.");
        }
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        using(var creator=new FrmNavalPatrolCreator(template,config,32,26){Opacity=0,ShowInTaskbar=false})
        {
            creator.Show();Application.DoEvents();
            All(creator).OfType<NumericUpDown>().Single(c=>c.Name=="PatrolLevel").Value=42;
            All(creator).OfType<ComboBox>().Single(c=>c.Name=="PatrolDifficulty").SelectedIndex=(int)NavalPatrolDifficulty.Elite;
            var preview=All(creator).OfType<DataGridView>().Single(c=>c.Name=="PatrolBalancePreview");
            if(preview.Rows.Count!=14 || !preview.ReadOnly)throw new Exception("Creator needs a complete read-only live stat preview.");
            All(creator).OfType<Button>().Single(c=>c.Name=="GeneratePatrol").PerformClick();Application.DoEvents();
            var result=(HerboniaPatrol?)typeof(FrmNavalPatrolCreator).GetProperty("Result",flags)!.GetValue(creator);
            if(result==null || result.Level!=42 || result.Ship.Heading!=270 || !result.IsValid() || result.Ship.Id!=template.Ship.Id || result.Ship.Graphics!="custom.png" || result.Loot[0].ItemId!=template.Loot[0].ItemId || result.BoardingDungeonId!=template.BoardingDungeonId)
                throw new Exception("Applying balance must preserve authored ship, loot, boarding and identity.");
            if(template.Level!=4 || config.Herbonia[0].Level!=4)throw new Exception("The modal creator must not mutate the original draft until accepted by its parent.");
        }
        using(var full=new FrmNavalPatrolCreator(template,config,32,26,false))
        {
            var generate=All(full).OfType<Button>().Single(c=>c.Name=="GeneratePatrol");
            if(!generate.Enabled)throw new Exception("A full list must still allow updating an existing patrol.");
            All(full).OfType<CheckBox>().Single(c=>c.Name=="ReplacePatrol").Checked=false;
            if(generate.Enabled)throw new Exception("Creation cannot bypass the 32-patrol limit.");
            var newPatrol=(HerboniaPatrol)typeof(FrmNavalPatrolCreator).GetMethod("BuildPatrol",flags)!.Invoke(full,null)!;
            if(newPatrol.Ship.Id==template.Ship.Id)throw new Exception("A new patrol must have an independent id.");
        }
        System.Console.WriteLine("PASS naval patrol creator: level/profile preview, accepted draft, preserved content, fresh ids and list limit");
    }
}
