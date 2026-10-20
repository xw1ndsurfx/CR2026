using System.Globalization;
using System.Reflection;
using Intersect.Editor.Forms.Editors;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Editor;
using Intersect.Network.Packets.Server;
using Newtonsoft.Json;

internal static class NavalSystemEditorTests
{
    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control control in root.Controls) { yield return control; foreach (var child in Descendants(control)) yield return child; }
    }
    internal static void Run()
    {
        var culture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr");
            var packet = new NavalConfigurationPacket { NavigationJson = "{\"Ships\":[]}", NavigationVersion = "nav-1",
                CatalogueJson = JsonConvert.SerializeObject(new HarbourCatalog { Enabled = true, Offers = [new() { Id = "royal" }] }),
                CatalogueVersion = "shop-1" };
            using var editor = new FrmNavalConfiguration(packet);
            if (editor.Text != "Éditeur du système naval") throw new Exception("Naval editor must be localized.");
            var tabs = editor.Controls.OfType<TabControl>().Single();
            if (tabs.TabPages.Count != 6) throw new Exception("Navigation, ships, shop, presentation Sailing and AI must be separate sections.");
            var cannonEnabled=Descendants(tabs.TabPages[0]).OfType<CheckBox>().Single(c=>c.Name=="CannonsEnabled");
            var cannonDamage=Descendants(tabs.TabPages[0]).OfType<NumericUpDown>().Single(c=>c.Name=="CannonDamage");
            cannonEnabled.Checked=true;cannonDamage.Value=17;
            var cannonSave=(NavalConfigurationRequestPacket)typeof(FrmNavalConfiguration).GetMethod("PrepareSave",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(editor,null)!;
            var cannonSettings=JsonConvert.DeserializeObject<NavalConfiguration>(cannonSave.ConfigurationJson)!;
            if(!cannonSettings.CannonsEnabled || cannonSettings.CannonDamage!=17) throw new Exception("The native naval editor must save the enabled flag and cannon damage.");
            tabs.SelectedIndex = 2;
            var remove = Descendants(tabs.TabPages[2]).OfType<Button>().Single(b => b.Text == "Retirer");
            if (remove.Enabled) throw new Exception("Published models must be retired through For sale, preserving their owners' fleets.");
            var prepare = typeof(FrmNavalConfiguration).GetMethod("PrepareSave", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var request = (NavalConfigurationRequestPacket)prepare.Invoke(editor, null)!;
            if (request.Section != 2 || request.ExpectedVersion != "shop-1") throw new Exception("Save the selected section.");
            var pendingRequest = typeof(FrmNavalConfiguration).GetField("_pendingRequest", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var acknowledgementId = Guid.NewGuid(); pendingRequest.SetValue(editor, acknowledgementId);
            editor.AcceptResponse(new() { RequestId = Guid.NewGuid(), SavedSection = 1, NavigationVersion = "stale" });
            if ((Guid)pendingRequest.GetValue(editor)! != acknowledgementId) throw new Exception("Ignore acknowledgements from another save request.");
            editor.AcceptResponse(new() { RequestId = acknowledgementId, SavedSection = 1, NavigationVersion = "nav-2", CatalogueVersion = "other-editor-shop-2" });
            request = (NavalConfigurationRequestPacket)prepare.Invoke(editor, null)!;
            if (request.ExpectedVersion != "shop-1") throw new Exception("A navigation acknowledgement must not authorize overwriting another editor's shop changes.");
            if(int.TryParse(Environment.GetEnvironmentVariable("NAVAL_EDITOR_PREVIEW_SECTION"),out var previewSection)) tabs.SelectedIndex=previewSection;
            var preview = Environment.GetEnvironmentVariable("NAVAL_EDITOR_PREVIEW");
            if (!string.IsNullOrEmpty(preview))
            {
                editor.StartPosition = FormStartPosition.Manual;
                editor.Location = new System.Drawing.Point(-20000, -20000);
                editor.ShowInTaskbar = false;
                editor.Show(); Application.DoEvents(); editor.PerformLayout();
                using var image = new System.Drawing.Bitmap(editor.Width, editor.Height);
                editor.DrawToBitmap(image, new System.Drawing.Rectangle(0, 0, image.Width, image.Height));
                image.Save(preview, System.Drawing.Imaging.ImageFormat.Png);
                editor.Hide();
            }
            Console.WriteLine("PASS localized Naval System Editor opens six sections and keeps independent save versions");
            CheckAssetPreview();
            CheckHerboniaEditor();
            CheckLootEditor();
        }
        finally { CultureInfo.CurrentUICulture = culture; }
    }

    private static void CheckHerboniaEditor()
    {
        var patrol=new HerboniaPatrol();patrol.Ship.MapId=Guid.NewGuid();patrol.Ship.X=10.5;patrol.Ship.Y=10.5;
        using var editor=new FrmNavalConfiguration(new() {NavigationJson=JsonConvert.SerializeObject(new NavalConfiguration {Herbonia=[patrol]}),CatalogueJson="{}"});
        var tabs=editor.Controls.OfType<TabControl>().Single();tabs.SelectedIndex=5;
        if(tabs.TabPages[5].Text!="Patrouilles IA Herbonia") throw new Exception("Localize the AI section.");
        Descendants(tabs.TabPages[5]).OfType<CheckBox>().Single(c=>c.Name=="Enabled").Checked=true;
        Descendants(tabs.TabPages[5]).OfType<NumericUpDown>().Single(c=>c.Name=="DetectionRange").Value=15;
        foreach(var value in new[]{("Level",8),("MaximumHealth",500),("CannonDamage",17),("SailingExperienceReward",275)})
            Descendants(tabs.TabPages[5]).OfType<NumericUpDown>().Single(c=>c.Name==value.Item1).Value=value.Item2;
        var save=(NavalConfigurationRequestPacket)typeof(FrmNavalConfiguration).GetMethod("PrepareSave",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(editor,null)!;
        var actual=JsonConvert.DeserializeObject<NavalConfiguration>(save.ConfigurationJson)!.Herbonia.Single();
        if(save.Section!=1 || !actual.Enabled || actual.DetectionRange!=15 || actual.Ship.Id!=patrol.Ship.Id || actual.Level!=8 || actual.MaximumHealth!=500 || actual.CannonDamage!=17 || actual.SailingExperienceReward!=275) throw new Exception("AI editor must preserve identity and save its patrol settings.");
        var preview=Environment.GetEnvironmentVariable("NAVAL_HERBONIA_PREVIEW");
        if(!string.IsNullOrEmpty(preview))
        {
            editor.StartPosition=FormStartPosition.Manual;editor.Location=new(-20000,-20000);editor.ShowInTaskbar=false;
            editor.Show();Application.DoEvents();editor.PerformLayout();
            using var bitmap=new System.Drawing.Bitmap(editor.Width,editor.Height);
            editor.DrawToBitmap(bitmap,new System.Drawing.Rectangle(0,0,editor.Width,editor.Height));bitmap.Save(preview,System.Drawing.Imaging.ImageFormat.Png);
            editor.Hide();
        }
        Console.WriteLine("PASS Herbonia AI editor controls save through authoritative navigation configuration");
    }

    private static void CheckLootEditor()
    {
        var item=new Intersect.Framework.Core.GameObjects.Items.ItemDescriptor(Guid.NewGuid()) {Name="Naval loot test"};
        Intersect.Framework.Core.GameObjects.Items.ItemDescriptor.Lookup.Set(item.Id,item);
        try
        {
            var patrol=new HerboniaPatrol();patrol.Ship.MapId=Guid.NewGuid();patrol.Ship.X=patrol.Ship.Y=10.5;
            patrol.Loot=[new(){ItemId=item.Id}];
            using var editor=new FrmNavalConfiguration(new() {NavigationJson=JsonConvert.SerializeObject(new NavalConfiguration {Herbonia=[patrol]}),CatalogueJson="{}"});
            var tabs=editor.Controls.OfType<TabControl>().Single();tabs.SelectedIndex=5;
            var controls=Descendants(tabs.TabPages[5]).ToArray();
            controls.OfType<NumericUpDown>().Single(c=>c.Name=="MinimumQuantity").Value=2;
            controls.OfType<NumericUpDown>().Single(c=>c.Name=="MaximumQuantity").Value=5;
            controls.OfType<NumericUpDown>().Single(c=>c.Name=="ChancePercent").Value=50;
            controls.OfType<NumericUpDown>().Single(c=>c.Name=="Quality").Value=4;
            controls.OfType<NumericUpDown>().Single(c=>c.Name=="CargoCapacity").Value=30;
            controls.OfType<ComboBox>().Single(c=>c.Name=="Kind").SelectedIndex=(int)NavalCargoKind.RoyalChest;
            var save=(NavalConfigurationRequestPacket)typeof(FrmNavalConfiguration).GetMethod("PrepareSave",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(editor,null)!;
            var actual=JsonConvert.DeserializeObject<NavalConfiguration>(save.ConfigurationJson)!.Herbonia.Single();
            if(actual.Loot[0].ItemId!=item.Id || actual.Loot[0].MaximumQuantity!=5 || actual.Loot[0].Kind!=NavalCargoKind.RoyalChest || actual.Ship.CargoCapacity!=30)
                throw new Exception("The loot table must persist actual item ids, quantities, type and hold capacity.");
            var preview=Environment.GetEnvironmentVariable("NAVAL_LOOT_PREVIEW");
            if(!string.IsNullOrEmpty(preview))
            {
                editor.StartPosition=FormStartPosition.Manual;editor.Location=new(-20000,-20000);editor.ShowInTaskbar=false;editor.Show();Application.DoEvents();
                var group=controls.OfType<GroupBox>().Single(c=>c.Text=="Table de butin naval");((ScrollableControl)group.Parent!).ScrollControlIntoView(group);Application.DoEvents();
                using var bitmap=new System.Drawing.Bitmap(editor.Width,editor.Height);editor.DrawToBitmap(bitmap,new System.Drawing.Rectangle(0,0,editor.Width,editor.Height));
                bitmap.Save(preview,System.Drawing.Imaging.ImageFormat.Png);editor.Hide();
            }
            Console.WriteLine("PASS native naval loot table: existing items, probabilities, qualities, quantities and capacity");
        }
        finally {Intersect.Framework.Core.GameObjects.Items.ItemDescriptor.Lookup.Delete(item);}
    }

    private static void CheckAssetPreview()
    {
        var directory=Path.Combine(Path.GetTempPath(),"naval-assets-"+Guid.NewGuid().ToString("N"));
        var previous=Environment.CurrentDirectory;
        Directory.CreateDirectory(Path.Combine(directory,"resources","spells"));Directory.CreateDirectory(Path.Combine(directory,"resources","sounds"));
        try
        {
            using(var bitmap=new System.Drawing.Bitmap(32,32)) bitmap.Save(Path.Combine(directory,"resources","spells","port.png"));
            File.WriteAllBytes(Path.Combine(directory,"resources","sounds","fire.wav"),[]);
            Environment.CurrentDirectory=directory;
            using var editor=new FrmNavalConfiguration(new() {NavigationJson="{}",CatalogueJson="{}"});
            var tabs=editor.Controls.OfType<TabControl>().Single();tabs.SelectedIndex=3;
            var icons=Descendants(tabs.TabPages[3]).OfType<ComboBox>().Single(c=>c.Name=="PortIcon");icons.SelectedItem="port.png";
            var image=Descendants(tabs.TabPages[3]).OfType<PictureBox>().Single(c=>c.Name=="PortIconPreview");
            if(image.Image==null || image.Image.Width!=32) throw new Exception("The selected spell icon needs an actual image preview.");
            var sounds=Descendants(tabs.TabPages[3]).OfType<ComboBox>().Single(c=>c.Name=="CannonFireSound");sounds.SelectedItem="fire.wav";
            var save=(NavalConfigurationRequestPacket)typeof(FrmNavalConfiguration).GetMethod("PrepareSave",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(editor,null)!;
            var settings=JsonConvert.DeserializeObject<NavalConfiguration>(save.ConfigurationJson)!;
            if(save.Section!=1 || settings.Presentation.PortIcon!="port.png" || settings.Presentation.CannonFireSound!="fire.wav") throw new Exception("Presentation must save in navigation, preserving shop independence.");
            using var unlocked=File.Open(Path.Combine(directory,"resources","spells","port.png"),FileMode.Open,FileAccess.ReadWrite,FileShare.None);
            Console.WriteLine("PASS spell icon selector, image preview, unlocked resource and sound selector save in navigation section");
        }
        finally
        {
            Environment.CurrentDirectory=previous;
            if(Path.GetFullPath(directory).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)) Directory.Delete(directory,true);
        }
    }
}
