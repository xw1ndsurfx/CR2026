using System.Globalization;
using System.Reflection;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Matchmaking;
using Intersect.Framework.Core.QualityOfLife;
using Newtonsoft.Json;

internal static class MatchmakingEditorTests
{
    private static IEnumerable<Control> All(Control root){foreach(Control c in root.Controls){yield return c;foreach(var n in All(c))yield return n;}}
    private static void Check(bool pass,string message){if(!pass)throw new Exception(message);}
    internal static void Run()
    {
        var previous=CultureInfo.CurrentUICulture;
        try
        {
            foreach(var locale in new[]{"en","fr","es"})
            {
                CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo(locale);
                var first=new MatchmakingEditorChoice{Id=Guid.NewGuid(),Activity=MatchmakingActivity.Expedition,Name="Fleet trial",MinimumPlayers=2,MaximumPlayers=13};
                var second=new MatchmakingEditorChoice{Id=Guid.NewGuid(),Activity=MatchmakingActivity.Expedition,Name="Fleet trial",MinimumPlayers=4,MaximumPlayers=24};
                var source=new LobbyConfiguration();var entry=new MatchmakingEntry{Activity=MatchmakingActivity.Expedition,ContentId=first.Id,TargetPlayers=7,Enabled=true};source.Matchmaking.Entries.Add(entry);
                using var form=new Intersect.Editor.Forms.Editors.FrmLobbyConfiguration(JsonConvert.SerializeObject(source),JsonConvert.SerializeObject(new[]{first,second}));
                form.Size=new Size(850,660);form.CreateControl();form.PerformLayout();
                var working=(LobbyConfiguration)form.GetType().GetField("_working",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(form)!;
                var editor=All(form).Single(c=>c.GetType().Name=="MatchmakingEditor");
                ComboBox Combo(string name)=>All(editor).OfType<ComboBox>().Single(c=>c.Name==name);
                NumericUpDown Number(string name)=>All(editor).OfType<NumericUpDown>().Single(c=>c.Name==name);
                Number("Readytimeout(seconds)").Value=60;Number("Searchtimeout(seconds)").Value=600;
                Combo("Activity").SelectedIndex=2;
                Check(working.Matchmaking.Entries[0].ContentId==second.Id,"Duplicate names select wrong content id");
                Number("Targetplayers").Value=13;
                Check(source.Matchmaking.ReadyTimeoutSeconds==45&&entry.ContentId==first.Id,"Editing mutates original config");
                var clone=JsonConvert.DeserializeObject<LobbyConfiguration>(JsonConvert.SerializeObject(working))!;clone.Validate();
                Check(clone.Matchmaking.ReadyTimeoutSeconds==60&&clone.Matchmaking.QueueTimeoutSeconds==600&&clone.Matchmaking.Entries[0].TargetPlayers==13,"Settings not persisted");
                Check(Combo("Backgroundimage").Text=="matchmaking_background_v1.png"&&Combo("Groupfoundsound").Text=="matchmaking/found_v1.wav","Asset defaults missing from pickers");
                Check(All(form).OfType<TabPage>().Any(t=>t.Text==QualityOfLifeText.Get("Matchmaking",locale)),"Matchmaking tab untranslated");
                Check(All(editor).OfType<Button>().Any(b=>b.Text==QualityOfLifeText.Get("Add activity",locale)),"Add activity untranslated");
                Combo("Activitytype").SelectedIndex=0;
                Check(All(editor).OfType<ComboBox>().Any(c=>c.Name=="Dungeonentrymap"),"Dungeon entry selector missing");
                Check(working.Matchmaking.Entries[0].ContentId==Guid.Empty,"Switch type retains another type's id");
                Check(Combo("Dungeonentrymap").Items.Count==1,"Unrelated maps selectable without a dungeon");
                System.Console.WriteLine($"PASS matchmaking editor {locale}: content ids, limits, destination selector, media selectors, localization, round-trip");
            }
        }
        finally{CultureInfo.CurrentUICulture=previous;}
    }
}
