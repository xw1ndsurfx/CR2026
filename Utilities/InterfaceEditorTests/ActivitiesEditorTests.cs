using System.Reflection;
using System.IO.Compression;
using Intersect.Editor.Forms;
using Intersect.Framework.Core.GameObjects.Conditions;
using Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;
internal static class ActivitiesEditorTests
{
    internal static void Run()
    {
        foreach(var mode in Enum.GetValues<PremiumConditionMode>())
        {
            Condition condition=new PremiumStatusCondition { Mode=mode,RemainingDays=7 };
            var description=Intersect.Editor.Localization.Strings.GetEventConditionalDesc((dynamic)condition);
            if(!((string)description).Contains("Premium"))throw new Exception("Premium descriptions must resolve dynamically without binder failures");
        }
        using(var editor=new Intersect.Editor.Forms.Editors.FrmWorldActivities("{}"))
            if(!editor.MaximizeBox || editor.Controls.OfType<DataGridView>().Single().Columns.Count<7)throw new Exception("World activities exposes editable schedules and bonuses");
        var root=Path.Combine(Path.GetTempPath(),"cr-character-zip-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var zip=Path.Combine(root,"female.zip");
            using(var archive=ZipFile.Open(zip,ZipArchiveMode.Create))
            foreach(var name in new[]{"Female/Hair/Mov_Braid.png","Male/Hair/Mov_Braid.png","Female/Body/Mov_Body_Human.png","Female/Body/Mov_Body_Ape.png","Paperdolls/Top/Mov_Coat.png"})
            { using var stream=archive.CreateEntry(name).Open();using var bitmap=new Bitmap(16,16);using(var graphics=Graphics.FromImage(bitmap))graphics.Clear(name.StartsWith("Male/") ? Color.Blue : Color.Red);bitmap.Save(stream,System.Drawing.Imaging.ImageFormat.Png); }
            var type=typeof(FrmCharacterGenerator).Assembly.GetType("Intersect.Editor.Content.CharacterArchiveImporter",true)!;
            var import=type.GetMethod("Import",BindingFlags.NonPublic|BindingFlags.Static)!;
            var destination=Path.Combine(root,"output");
            var result=((int Imported,int Skipped,int Invalid))import.Invoke(null,[zip,destination,new[]{"Base","Body","Hair","Top"}])!;
            if(result.Imported!=5 || !File.Exists(Path.Combine(destination,"Base","Female","Mov_Body_Human.png")) || !File.Exists(Path.Combine(destination,"Body","Female","Mov_Body_Ape.png")))throw new Exception("ZIP imports do not require a Paperdolls root; normal bodies become bases and Ape stays a body layer");
            var second=((int Imported,int Skipped,int Invalid))import.Invoke(null,[zip,destination,new[]{"Base","Body","Hair","Top"}])!;
            if(second.Skipped!=5)throw new Exception("Reimport preserves existing art");
            var discover=typeof(FrmCharacterGenerator).GetMethod("DiscoverFamilies",BindingFlags.NonPublic|BindingFlags.Static)!;
            var families=(System.Collections.IEnumerable)discover.Invoke(null,[Path.Combine(destination,"Hair")])!;
            var family=families.Cast<object>().Single(f=>f.GetType().GetProperty("Name")!.GetValue(f)!.ToString()!.StartsWith("Female_"));
            if(!family.GetType().GetProperty("Name")!.GetValue(family)!.ToString()!.StartsWith("Female_"))throw new Exception("Female folder metadata must survive family discovery");
            var counterpart=typeof(FrmCharacterGenerator).GetMethod("FindOppositeFamily",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[families,family,true]);
            if(counterpart==null)throw new Exception("Matching opposite-sex paperdoll must be selected automatically");
            var output=Path.Combine(root,"variants");
            typeof(FrmCharacterGenerator).GetMethod("WriteGenderVariants",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[counterpart,family,output,"cc_hair_braid"]);
            using var male=new Bitmap(Path.Combine(output,"cc_hair_braid_male.png"));using var female=new Bitmap(Path.Combine(output,"cc_hair_braid_female.png"));
            if(male.GetPixel(0,0).ToArgb()!=Color.Blue.ToArgb() || female.GetPixel(0,0).ToArgb()!=Color.Red.ToArgb() || Directory.GetFiles(output,"*.png").Length!=12)throw new Exception("Paired export must preserve both distinct graphics and all animation overrides");
        }
        finally { Directory.Delete(root,true); }
        System.Console.WriteLine("PASS premium condition dispatch, world activity editor, real female ZIP import, body/base classification and duplicate preservation");
    }
}
