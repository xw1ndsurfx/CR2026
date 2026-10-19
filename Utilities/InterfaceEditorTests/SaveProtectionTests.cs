using System.Globalization;
using System.Reflection;
using Intersect.Editor.Classes.Maps;
using Intersect.Editor.Core;
using Intersect.Editor.Forms;
using Intersect.Editor.Forms.Editors;
using Intersect.Editor.Localization;
using Intersect.Editor.Maps;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Maps.MapList;
using Intersect.Framework.Core.Metalworking;
using Intersect.Framework.Core.WorldEvents;
using Intersect.Localization;
using Newtonsoft.Json;

internal static class SaveProtectionTests
{
    internal static void Run()
    {
        var language = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
            RoundTripForms();
            RecoveryStore();
            SaveReceipt();
            PreserveNewerMapEdits();
            PersistPreferences();
            foreach (var (source, versions) in EditorSafetyText.Catalog.Entries)
                foreach (var code in GameTextCatalog.Languages)
                    Check(versions.TryGetValue(code, out var value) && !string.IsNullOrWhiteSpace(value) && GameTextTokens.Compatible(source, value), "Missing locale/token: " + source + " " + code);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr");
            Check(EditorSafetyText.Get("Save protection") == "Protection des sauvegardes", "French locale");
            using var settings = new FrmSaveProtection();
            settings.Show(); Application.DoEvents();
            var automatic = All(settings).OfType<CheckBox>().Single(c => c.Name == "AutoSaveMaps");
            Check(automatic.Text.Contains("serveur"), "Localized explicit server autosave option");
            using var bitmap = new Bitmap(settings.Width, settings.Height);
            settings.DrawToBitmap(bitmap, new Rectangle(Point.Empty, settings.Size));
            var output = Environment.GetEnvironmentVariable("EDITOR_TEST_SCREENSHOTS");
            if (!string.IsNullOrWhiteSpace(output)) { Directory.CreateDirectory(output); bitmap.Save(Path.Combine(output, "save-protection-fr.png")); }
            // Dispose without FormClosing: this test must not change the developer's preferences.
            settings.Hide();
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de");
            Check(EditorSafetyText.Get("Save protection") == "Save protection", "English fallback");
        }
        finally { CultureInfo.CurrentUICulture = language; }
        Console.WriteLine("PASS save protection: serialized PvP/workshop forms, pending grid edits, exact diagnostics, recovery generations/corruption, translations and settings layout");
    }

    private static void RoundTripForms()
    {
        var input = new ItemDescriptor(Guid.NewGuid()) { Name = "Test ore" };
        var output = new ItemDescriptor(Guid.NewGuid()) { Name = "Test ingot" };
        ItemDescriptor.Lookup[input.Id] = input;
        ItemDescriptor.Lookup[output.Id] = output;
        var arenaMap = new MapListMap { MapId = Guid.NewGuid(), Name = "Test arena" };
        MapList.OrderedMaps.Add(arenaMap);
        try
        {
            var config = new WorldActivitiesConfiguration();
            config.Pvp.Enabled = true;
            config.Pvp.Arenas.Add(new() { Name = "Arena saved", Enabled = true, MapId = arenaMap.MapId, BlueX = 15, BlueY = 10 });
            using (var pvp = new FrmPvpConfiguration(JsonConvert.SerializeObject(config)))
            {
                pvp.Show(); Application.DoEvents();
                var tabs = pvp.Controls.OfType<TabControl>().Single();
                var grades = tabs.TabPages[1].Controls.OfType<DataGridView>().Single();
                Check(grades.Rows.Count == 10, "Opening a server snapshot must not append default grades");
                tabs.SelectedIndex = 1;
                grades.CurrentCell = grades.Rows[1].Cells[1]; grades.BeginEdit(true);
                ((TextBox)grades.EditingControl).Text = "150";
                var saved = pvp.PrepareSave();
                Check(saved.Error == null && saved.Packet != null, "Valid PvP and Capture the Chest must save: " + saved.Error);
                var roundTrip = JsonConvert.DeserializeObject<WorldActivitiesConfiguration>(saved.Packet!.Json)!;
                Check(roundTrip.IsValid && roundTrip.Pvp.Grades[1].Points == 150 && roundTrip.Pvp.Arenas.Count == 1, "Commit final grade edit and keep arena");
                grades.Rows[1].Cells[1].Value = 0;
                Check(pvp.PrepareSave().Error?.Contains("Grade 2") == true, "Point to the actual invalid grade");
                pvp.Hide();
            }
            foreach (var kind in Enum.GetValues<MetalworkKind>())
            {
                config.Metalworking.Recipes = [new() { Name = "Recipe one", Kind = kind, Enabled = true, OutputItemId = output.Id,
                    Ingredients = [new() { ItemId = input.Id, Quantity = 2 }] }];
                using var forge = new FrmMetalworking(JsonConvert.SerializeObject(config), kind);
                forge.Show(); Application.DoEvents();
                var grid = All(forge).OfType<DataGridView>().Single();
                grid.CurrentCell = grid.Rows[0].Cells[1]; grid.BeginEdit(true);
                ((TextBox)grid.EditingControl).Text = "12";
                var saved = forge.PrepareSave();
                Check(saved.Error == null && saved.Packet != null, kind + " completed recipe can be saved: " + saved.Error);
                var roundTrip = JsonConvert.DeserializeObject<WorldActivitiesConfiguration>(saved.Packet!.Json)!;
                Check(roundTrip.IsValid && roundTrip.Metalworking.Recipes[0].Ingredients[0].Quantity == 12, "Commit final material edit");
                grid.Rows[0].Cells[1].Value = 0;
                Check(forge.PrepareSave().Error?.Contains("Material row 1") == true, "Point to the actual invalid material");
                grid.Rows[0].Cells[1].Value = 12;
                var add = All(forge).OfType<Button>().Single(b => b.Text == "Add recipe");
                add.PerformClick(); Application.DoEvents();
                All(forge).OfType<TextBox>().Single(t => t.Name == "RecipeName").Text = "Newly authored recipe";
                All(forge).OfType<ComboBox>().Single(t => t.Name == "RecipeOutput").SelectedValue = output.Id;
                All(forge).OfType<CheckBox>().Single(t => t.Name == "RecipeEnabled").Checked = true;
                ((System.ComponentModel.BindingList<MetalworkIngredient>)grid.DataSource!).Add(new() { ItemId = input.Id, Quantity = 3 });
                saved = forge.PrepareSave();
                Check(saved.Error == null && saved.Packet != null, "Newly created " + kind + " recipe can save: " + saved.Error);
                roundTrip = JsonConvert.DeserializeObject<WorldActivitiesConfiguration>(saved.Packet!.Json)!;
                Check(roundTrip.IsValid && roundTrip.Metalworking.Recipes.Count == 2 && roundTrip.Metalworking.Recipes[1].Name == "Newly authored recipe", "Keep existing and new recipes without duplication");
                forge.Hide();
            }
        }
        finally { ItemDescriptor.Lookup.DeleteAt(input.Id); ItemDescriptor.Lookup.DeleteAt(output.Id); MapList.OrderedMaps.Remove(arenaMap); }
    }

    private static void RecoveryStore()
    {
        var directory = Path.Combine(Path.GetTempPath(), "intersect-recovery-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var store = new EditorRecoveryStore(directory);
            var id = Guid.NewGuid();
            var map = new MapInstance(id) { Name = "Recovery map" };
            var evt = new EventDescriptor(Guid.NewGuid(), id, 3, 4) { Name = "Changed event" };
            map.LocalEvents.Add(evt.Id, evt);
            for (var i = 0; i < 5; i++)
            {
                map.Name = "Generation " + i;
                store.Write(new() { Id = id, Name = map.Name, Map = map.SaveInternal() });
            }
            Check(Directory.GetFiles(directory, "map-*.json").Length == 3, "Keep three recovery generations");
            var latest = store.Read("map", id)!;
            store.Write(new() { Id = id, Name = map.Name, Map = map.SaveInternal() });
            Check(Directory.GetFiles(directory, "map-*.json").Length == 3 && store.Read("map", id)!.SavedUtc == latest.SavedUtc, "Unchanged drafts do not rotate away useful history");
            var saved = latest.Map!;
            var metadata = Newtonsoft.Json.Linq.JObject.Parse(saved.Metadata);
            Check((string?)metadata["Name"] == "Generation 4" && saved.EventData.Contains("Changed event"), "Recover map metadata, tiles, attributes and event bodies");
            File.WriteAllText(Directory.GetFiles(directory, "map-*.json").OrderDescending().First(), "broken JSON");
            Check(store.Read("map", id)!.Name == "Generation 3", "Corrupt latest draft falls back to preceding generation");
            Check(store.Read("map", Guid.NewGuid()) == null, "Never restore another map's data");
            store.Write(new() { Kind = "event", Id = evt.Id, Name = evt.Name, EventJson = evt.JsonData });
            Check(store.Read("event", evt.Id)?.EventJson == evt.JsonData, "Common/map event model round trip");
            Check(!Directory.GetFiles(directory, "*.tmp").Any(), "Atomic writes clean temporary files");
        }
        finally
        {
            // Only this test's explicit GUID directory and its non-recursive files.
            foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }

    private static void SaveReceipt()
    {
        using var form = new Form();
        var text = new TextBox { Text = "Work in progress" };
        form.Controls.Add(text);
        form.Show();
        Intersect.Network.Packets.WorldEvents.SaveWorldActivitiesPacket? sent = null;
        string? error = null;
        var session = new ActivitySaveSession(form, packet => sent = packet, message => error = message);
        session.Send(new());
        Check(!text.Enabled && sent?.RequestId != Guid.Empty, "Disable editing while this save is pending");
        ActivitySaveSession.Receive(new() { RequestId = Guid.NewGuid(), Saved = true });
        Check(!form.IsDisposed && !text.Enabled, "Ignore unrelated receipts");
        form.Close();
        Check(!form.IsDisposed, "Do not close and discard draft while a save is pending");
        ActivitySaveSession.Receive(new() { RequestId = sent!.RequestId, Error = "Test refusal" });
        Check(!form.IsDisposed && text.Enabled && text.Text == "Work in progress" && error == "Test refusal", "Refusal leaves draft editable");
        session.Send(new());
        ActivitySaveSession.Receive(new() { RequestId = sent!.RequestId, Saved = true });
        Check(form.IsDisposed, "Only confirmed persistence closes the draft");
    }

    private static void PersistPreferences()
    {
        var configuration = Intersect.Configuration.ClientConfiguration.Instance;
        var original = configuration.Host;
        var testHost = "editor-save-test-" + Guid.NewGuid().ToString("N") + ".invalid";
        var registryName = $"{testHost}:{configuration.Port}";
        configuration.Host = testHost;
        try
        {
            Check(Preferences.SaveReminders && !Preferences.AutoSaveMaps && Preferences.AutoSaveMinutes == 2, "Safe defaults on a new server");
            Preferences.SaveReminders = false; Preferences.AutoSaveMaps = true; Preferences.AutoSaveMinutes = 7;
            Check(!Preferences.SaveReminders && Preferences.AutoSaveMaps && Preferences.AutoSaveMinutes == 7, "Settings persist through registry reads");
            Preferences.SavePreference(nameof(Preferences.AutoSaveMinutes), "500");
            Check(Preferences.AutoSaveMinutes == 30, "Clamp invalid saved intervals");
            configuration.Host = testHost + "-other";
            Check(Preferences.SaveReminders && !Preferences.AutoSaveMaps, "Server settings remain separate");
        }
        finally
        {
            configuration.Host = original;
            // Unique test key only; DeleteSubKey is non-recursive and leaves user settings intact.
            using var parent = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\IntersectEditor", true);
            parent?.DeleteSubKey(registryName, throwOnMissingSubKey: false);
        }
    }

    private static void PreserveNewerMapEdits()
    {
        var map = new MapInstance(Guid.NewGuid()) { Name = "Sent version" };
        var sent = map.SaveInternal();
        EditorMapSaveTracker.Sent(map);
        map.Name = "Typed after sending";
        var received = new MapInstance(map.Id);
        received.LoadInternal(sent);
        received.Revision = 7;
        received.SaveStateAsUnchanged();
        Check(EditorMapSaveTracker.PreserveNewerEdits(map, received), "Detect changes made while waiting for server echo");
        Check(received.Name == "Typed after sending" && received.Revision == 7 && received.Changed(), "Keep newer edits dirty and server revision current");
        Check(!EditorMapSaveTracker.PreserveNewerEdits(map, received), "Consume each pending save once");
        EditorMapSaveTracker.Sent(received);
        received.Name = "Second outgoing save";
        EditorMapSaveTracker.Sent(received);
        var firstEcho = new MapInstance(map.Id);
        firstEcho.LoadInternal(sent);
        firstEcho.SaveStateAsUnchanged();
        Check(EditorMapSaveTracker.PreserveNewerEdits(received, firstEcho) && firstEcho.Name == "Second outgoing save", "First of two overlapping receipts preserves the second save");
        firstEcho.Name = "Third unsent edit";
        var secondEcho = new MapInstance(map.Id);
        secondEcho.LoadInternal(sent);
        secondEcho.SaveStateAsUnchanged();
        Check(EditorMapSaveTracker.PreserveNewerEdits(firstEcho, secondEcho) && secondEcho.Name == "Third unsent edit", "Second receipt preserves edits on the replacement map instance");
    }

    private static IEnumerable<Control> All(Control parent)
    { foreach (Control control in parent.Controls) { yield return control; foreach (var child in All(control)) yield return child; } }
    private static void Check(bool valid, string message) { if (!valid) throw new Exception(message); }
}
