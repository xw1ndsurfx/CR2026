using System.Globalization;
using System.Reflection;
using Intersect.Editor.Forms.Editors;
using Intersect.Framework.Core.GameObjects.Maps.MapList;
using Intersect.Framework.Core.GuildHeadquarters;
using Intersect.Network.Packets.GuildHeadquarters;
using Newtonsoft.Json;
internal static class GuildHeadquartersEditorTests
{
    private static IEnumerable<Control> All(Control root){foreach(Control child in root.Controls){yield return child;foreach(var n in All(child))yield return n;}}
    private static void Check(bool pass,string text){if(!pass)throw new Exception(text);}
    private static void Click(Button button)=>typeof(Button).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(button,[EventArgs.Empty]);
    internal static void Run()
    {
        var before=CultureInfo.CurrentUICulture;var maps=MapList.OrderedMaps.ToArray();var map=Guid.NewGuid();
        MapList.OrderedMaps.Clear();MapList.OrderedMaps.Add(new(){MapId=map,Name="Guild hall"});
        try
        {
            foreach(var language in new[]{"en","fr","es"})
            {
                CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo(language);string T(string key)=>HeadquartersText.Get(key,language);
                var sent=new List<HeadquartersConfigurationSavePacket>();
                var cfg=new HeadquartersConfiguration{Enabled=true,Tiers=[new(){Name="Hall",MapId=map}],Assets=new(){Background="missing-custom.png",OpenSound="custom.wav"}};
                using var form=new FrmGuildHeadquarters(JsonConvert.SerializeObject(cfg),"v1",sent.Add);form.Size=new(800,600);form.CreateControl();form.PerformLayout();
                Check(form.Text==T("Guild headquarters"),"Editor untranslated");
                var tabs=All(form).OfType<TabControl>().Single();tabs.SelectedIndex=1;
                Check(All(form).OfType<ComboBox>().Any(c=>c.Text=="Guild hall"),"Unopened map missing from picker");
                Check(!All(form).OfType<Button>().Single(b=>b.Text==T("Remove last unsaved tier")).Enabled,"Saved tier removable");
                Click(All(form).OfType<Button>().Single(b=>b.Text==T("Add tier")));
                Check(All(form).OfType<ListBox>().Single().Items.Count==2,"Cannot add tier");
                Click(All(form).OfType<Button>().Single(b=>b.Text==T("Remove last unsaved tier")));
                tabs.SelectedIndex=2;Check(All(tabs.SelectedTab!).OfType<ComboBox>().Any(c=>c.Text=="missing-custom.png"),"Missing image selection was discarded");
                Click(All(form).OfType<Button>().Single(b=>b.Text==T("Save headquarters")));
                Check(sent.Count==1&&sent[0].Revision=="v1","Wrong configuration revision");
                var saved=JsonConvert.DeserializeObject<HeadquartersConfiguration>(sent[0].Json)!;
                Check(saved.Tiers[0].Id==cfg.Tiers[0].Id&&saved.Tiers[0].MapId==map&&saved.Assets.OpenSound=="custom.wav","Editor lost stable ids or asset overrides");
                typeof(FrmGuildHeadquarters).GetMethod("Acknowledge",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(form,[new HeadquartersConfigurationPacket{Error="Invalid headquarters configuration."}]);
                Check(All(form).OfType<Label>().Any(l=>l.Text==T("Invalid headquarters configuration.")),"Server error did not keep draft open");
                Click(All(form).OfType<Button>().Single(b=>b.Text==T("Save headquarters")));
                typeof(FrmGuildHeadquarters).GetMethod("Acknowledge",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(form,[new HeadquartersConfigurationPacket{Revision="v2"}]);
                Check(All(form).OfType<Label>().Any(l=>l.Text==T("Headquarters saved.")),"Missing saved acknowledgment");
                Click(All(form).OfType<Button>().Single(b=>b.Text==T("Save headquarters")));
                typeof(FrmGuildHeadquarters).GetMethod("SaveTimedOut",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(form,[]);
                Check(!All(form).OfType<Button>().Single(b=>b.Text==T("Save headquarters")).Enabled,"Unconfirmed save can be submitted twice");
                Check(All(form).OfType<Label>().Any(l=>l.Text==T("No server confirmation. Reopen the editor to check the saved configuration.")),"Timeout is not explained");
                var closing=new FormClosingEventArgs(CloseReason.UserClosing,false);
                typeof(Form).GetMethod("OnFormClosing",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(form,[closing]);
                Check(!closing.Cancel,"Timeout traps user in editor");
                Console.WriteLine("PASS headquarters editor: maps, stable tiers, missing assets, revisions, server errors and acknowledgment / "+language);
            }
        }
        finally{CultureInfo.CurrentUICulture=before;MapList.OrderedMaps.Clear();MapList.OrderedMaps.AddRange(maps);}
    }
}
