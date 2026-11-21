using Intersect.Editor.Content;
using Intersect.Editor.Forms.Editors;
using Intersect.Network.Packets.Server;

internal static class ImportStatusAndNavalScrollTests
{
    internal static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "import-status-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "source.png");
            var folder = Path.Combine(root, "game");
            var cache = Path.Combine(root, "cache");
            Directory.CreateDirectory(folder);
            File.WriteAllText(source, "source content");
            Check(ImportedResourceStatus.Find(source, folder, cache, default) == null, "Unimported source");
            var copy = Path.Combine(folder, "renamed.png");
            File.Copy(source, copy);
            Check(ImportedResourceStatus.Find(source, folder, cache, default) == "renamed.png", "Historical renamed copy");
            File.WriteAllText(copy, "converted audio");
            ImportedResourceStatus.Record(source, copy, cache);
            Check(ImportedResourceStatus.Find(source, folder, cache, default) == "renamed.png", "Converted import receipt");
            Check(ImportedResourceStatus.Find(source, root, cache, default) != "renamed.png", "Destination types remain independent");
            File.WriteAllText(copy, "replacement content");
            Check(ImportedResourceStatus.Find(source, folder, cache, default) == null, "Replaced destination invalidates receipt");
            File.Delete(copy);
            Check(ImportedResourceStatus.Find(source, folder, cache, default) == null, "Deleted destination invalidates receipt");

            using var editor = new FrmNavalConfiguration(new NavalConfigurationPacket
            {
                NavigationJson = "{}", CatalogueJson = "{}", NavigationVersion = "1", CatalogueVersion = "1",
            }) { Opacity = 0, ShowInTaskbar = false };
            editor.Show();
            var tabs = editor.Controls.OfType<TabControl>().Single();
            tabs.SelectedIndex = 7;
            var split = tabs.SelectedTab!.Controls.OfType<SplitContainer>().Single();
            var list = split.Panel1.Controls.OfType<ListBox>().Single();
            list.SelectedIndex = 0;
            foreach (var size in new[] { new Size(1050, 780), new Size(850, 620) })
            {
                editor.Size = size;
                Application.DoEvents();
                var viewport = split.Panel2.Controls.OfType<Panel>().Single();
                var table = viewport.Controls.OfType<TableLayoutPanel>().Single();
                Check(table.Height > viewport.ClientSize.Height && viewport.VerticalScroll.Visible, "Full table must scroll at laptop sizes");
                var controls = table.Controls.Cast<Control>().Where(c => table.GetColumn(c) == 1).OrderBy(c => table.GetRow(c)).ToArray();
                for (var i = 1; i < controls.Length; i++)
                    Check(controls[i].Top >= controls[i - 1].Bottom, "Naval fields must not overlap");
                var last = controls[^1];
                viewport.ScrollControlIntoView(last);
                Application.DoEvents();
                var bounds = viewport.RectangleToClient(last.RectangleToScreen(last.ClientRectangle));
                Check(bounds.Top >= 0 && bounds.Bottom <= viewport.ClientSize.Height, "Last naval parameter must be reachable");
            }
            using var npcEditor = new FrmNpc();
            const System.Reflection.BindingFlags hidden = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var npc = new Intersect.Framework.Core.GameObjects.NPCs.NPCDescriptor(Guid.NewGuid())
                { DeathSound = "death.wav", ProximitySound = "growl.wav", ProximitySoundRadius = 8 };
            typeof(FrmNpc).GetField("mEditorItem", hidden)!.SetValue(npcEditor, npc);
            typeof(FrmNpc).GetMethod("LoadNpcSounds", hidden)!.Invoke(npcEditor, null);
            var death = (ComboBox)typeof(FrmNpc).GetField("_npcDeathSound", hidden)!.GetValue(npcEditor)!;
            var approach = (ComboBox)typeof(FrmNpc).GetField("_npcProximitySound", hidden)!.GetValue(npcEditor)!;
            var radius = (NumericUpDown)typeof(FrmNpc).GetField("_npcSoundRadius", hidden)!.GetValue(npcEditor)!;
            Check(death.Text == "death.wav" && approach.Text == "growl.wav" && radius.Value == 8,
                "NPC sound controls retain selections even when a resource is missing locally");
            death.SelectedItem = "";
            radius.Value = 12;
            Check(npc.DeathSound == "" && npc.ProximitySoundRadius == 12 && npc.ProximitySound == "growl.wav",
                "NPC sound controls edit independent descriptor settings");
            Console.WriteLine("PASS NPC sound editor loads missing resources safely and saves independent choices and radius");
            Console.WriteLine("PASS import status identities, conversion receipts, stale copies, destination types and naval scrolling");
        }
        finally { Directory.Delete(root, true); }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
