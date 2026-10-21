using System.Globalization;
using Intersect.Editor.Forms.Editors;
using Intersect.Framework.Core.WorldEvents;
using Newtonsoft.Json;
internal static class NavalCombatMusicEditorTests
{
    internal static void Run()
    {
        var previous=CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo("fr");
            using var form=new FrmWorldActivities(JsonConvert.SerializeObject(new WorldActivitiesConfiguration {NavalCombatMusic="naval-test.ogg"}),WorldActivitiesSection.CombatMusic);
            var controls=Descendants(form).ToArray();
            var selector=controls.OfType<ComboBox>().Single(c=>c.Name=="NavalCombatMusic");
            if(selector.SelectedItem?.ToString()!="naval-test.ogg" || !selector.Items.Contains("")) throw new Exception("Native naval music selector must preserve its configured filename and allow disabling it.");
            if(!controls.OfType<Label>().Any(c=>c.Text=="Musique de combat naval")) throw new Exception("Naval combat music label must use the localization catalogue.");
            var preview=Environment.GetEnvironmentVariable("NAVAL_MUSIC_EDITOR_PREVIEW");
            if(!string.IsNullOrEmpty(preview))
            {
                form.Show();Application.DoEvents();using var bitmap=new Bitmap(form.Width,form.Height);
                form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height));bitmap.Save(preview);form.Hide();
            }
            Console.WriteLine("PASS Combat Music native naval selector preserves configured files and localized caption");
        }
        finally {CultureInfo.CurrentUICulture=previous;}
    }
    private static IEnumerable<Control> Descendants(Control root)
    {foreach(Control child in root.Controls) {yield return child;foreach(var nested in Descendants(child)) yield return nested;}}
}
