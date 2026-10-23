using System.Reflection;
using System.ComponentModel;
using System.Drawing.Design;
using System.Windows.Forms.Design;
using Intersect.Editor.Content;
using Intersect.Editor.Forms.Editors;
using Intersect.Editor.Forms.Styling;
using Intersect.Editor.Maps;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Localization;

internal static class SearchableChoiceTests
{
    private sealed record Choice(Guid Id, string Name) { public override string ToString() => Name; }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    internal static void Run()
    {
        var a = new Choice(Guid.NewGuid(), "Same name"); var b = new Choice(Guid.NewGuid(), "Same name");
        using var host = new Form { Opacity = 0, ShowInTaskbar = false };
        var source = new LogiklikComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250 };
        source.Items.AddRange(["None", "blue sword.png", "red sword.png", "Épée bleue.wav", a, b]);
        source.SelectedIndex = 0; host.Controls.Add(source); host.Show(); Application.DoEvents();
        var changes = 0; source.SelectedIndexChanged += (_, _) => changes++;
        var entries = SearchableChoices.Snapshot(source);
        var filtered = SearchableChoices.Filter(entries, "SWORD blue", ".png", 2, false, []);
        Check(filtered.Length == 1 && filtered[0].Index == 1, "Multiple search words match in any order without case sensitivity.");
        Check(SearchableChoices.Filter(entries, "epee BLEUE", ".wav", 0, false, []).Single().Index == 3,
            "Accents do not prevent finding a resource.");
        Check(SearchableChoices.ShouldSearch(source), "Media lists open the picker even when short.");
        using (var picker = new EditorChoicePicker(source, entries, null, []))
        {
            picker.Opacity = 0; picker.Show(); Application.DoEvents();
            picker.SetFilter("Same name", sort: 2);
            Check(picker.VisibleCount == 2 && source.SelectedIndex == 0 && changes == 0,
                "Searching and sorting never change the original selection or raise its handlers.");
            picker.DialogResult = DialogResult.Cancel; picker.Hide();
        }
        Check(source.SelectedIndex == 0 && changes == 0, "Cancel leaves the source untouched.");
        Check(SearchableChoices.TryCommit(source, entries[5]) && ReferenceEquals(source.SelectedItem, b) && changes == 1,
            "A confirmed duplicate caption selects the correct original object exactly once.");
        var recent = SearchableChoices.Filter(entries, "", null, 0, true, [b]);
        Check(recent.Length == 1 && ReferenceEquals(recent[0].Item, b), "Recent filtering keeps identities distinct.");
        source.Items[5] = new Choice(Guid.NewGuid(), "Same name");
        Check(!SearchableChoices.TryCommit(source, entries[5]), "A stale popup cannot select a different object after a refresh.");

        using var tiny = new LogiklikComboBox(); tiny.Items.AddRange(["North", "South"]);
        Check(!SearchableChoices.ShouldSearch(tiny), "Small non-media setting lists stay direct.");
        using var large = new LogiklikComboBox();
        large.Items.AddRange(Enumerable.Range(0, 20_000).Select(index => (object)$"Resource {index:00000}.png").ToArray());
        Check(SearchableChoices.ShouldSearch(large), "Large catalogs use searchable selection.");
        var many = SearchableChoices.Snapshot(large);
        Check(SearchableChoices.Filter(many, "19999", null, 1, false, []).Single().Index == 19_999,
            "Search supports a large catalog without changing its original indices.");

        // Exercise the actual DropDown -> refreshed list -> modal picker -> commit path.
        source.Items.Clear(); source.Items.AddRange(["None", "first.png", "wanted.png"]); source.SelectedIndex = 0; changes = 0;
        var opening = 0; source.DropDown += (_, _) => opening++;
        var openedPicker = false;
        using (var closePicker = new System.Windows.Forms.Timer { Interval = 25 })
        {
            closePicker.Tick += (_, _) =>
            {
                var picker = Application.OpenForms.OfType<EditorChoicePicker>().FirstOrDefault();
                if (picker == null) return;
                openedPicker = true; closePicker.Stop(); picker.SetFilter("wanted"); picker.Choose();
            };
            closePicker.Start(); source.DroppedDown = true; Application.DoEvents();
        }
        Check(openedPicker && opening == 1 && source.SelectedIndex == 2 && changes == 1,
            "Opening the real combo preserves refresh handlers and commits only the final choice.");
        TestPreviewsAndSoundRefresh();
        TestMapPropertyChoices();
        foreach (var text in new[] { "Choose an entry", "Search by name or several words...", "All file types", "Original order",
            "Name: A to Z", "Name: Z to A", "Recent choices", "Listen", "Stop", "Select", "No matching entries.",
            "{0} of {1} entries", "Select an entry to preview it.", "Preview unavailable. You can still select this entry.",
            "Listen before selecting this sound.", "The selected resource is unavailable.", "Missing sound: {0}" })
        {
            foreach (var language in GameTextCatalog.Languages)
                Check(QualityOfLifeText.Catalog.Entries[text].ContainsKey(language) &&
                    GameTextTokens.Compatible(text, QualityOfLifeText.Get(text, language)), "Picker translation and tokens: " + text);
            Check(QualityOfLifeText.Get(text, "unknown") == text, "Picker translations fall back to English.");
        }
        host.Hide();
        Console.WriteLine("PASS searchable choices: live dropdown, 20k entries, multiword/accent search, duplicate identities, stable indices, cancel, stale refresh, previews, missing sounds and en/fr/es");
    }

    private static void TestMapPropertyChoices()
    {
        var map = new MapDescriptor(Guid.NewGuid())
        {
            Music = "missing.ogg", Sound = "missing.wav", Fog = "missing-fog.png",
            Panorama = "missing-panorama.png", OverlayGraphic = "missing-overlay.png",
        };
        var properties = new MapProperties(map);
        Check(properties.Music == "missing.ogg" && properties.Sound == "missing.wav" && properties.Fog == "missing-fog.png" &&
            properties.Panorama == "missing-panorama.png" && properties.OverlayGraphic == "missing-overlay.png" &&
            map.Music == "missing.ogg" && map.Sound == "missing.wav" && map.Fog == "missing-fog.png" &&
            map.Panorama == "missing-panorama.png" && map.OverlayGraphic == "missing-overlay.png",
            "Reading property-grid choices must preserve missing authored resource references.");
        var descriptors = TypeDescriptor.GetProperties(properties);
        foreach (var name in new[] { "Music", "Sound", "Fog", "Panorama", "OverlayGraphic", "WeatherRainSound", "WeatherThunderSound", "WeatherRainFog", "WeatherLightningAnimation", "WeatherSplashAnimation" })
            Check(descriptors[name]!.GetEditor(typeof(UITypeEditor)) is ResourcePropertyEditor,
                "Map resource fields expose the shared searchable editor: " + name);
        var descriptor = descriptors["Music"]!;
        var context = new PropertyContext(properties, descriptor);
        var editor = (ResourcePropertyEditor)descriptor.GetEditor(typeof(UITypeEditor))!;
        var service = new PickerService(false);
        Check(Equals(editor.EditValue(context, service, map.Music), "missing.ogg") && map.Music == "missing.ogg",
            "Cancelling the property-grid chooser leaves missing references intact.");
        service = new PickerService(true);
        Check(Equals(editor.EditValue(context, service, map.Music), "missing.ogg") && service.SawMissingChoice,
            "Property-grid selection includes the current missing file and returns its exact original value.");
    }

    private sealed class PropertyContext(object instance, PropertyDescriptor property) : ITypeDescriptorContext
    {
        public IContainer? Container => null;
        public object Instance => instance;
        public PropertyDescriptor PropertyDescriptor => property;
        public object? GetService(Type serviceType) => null;
        public bool OnComponentChanging() => true;
        public void OnComponentChanged() { }
    }

    private sealed class PickerService(bool accept) : IServiceProvider, IWindowsFormsEditorService
    {
        internal bool SawMissingChoice;
        public object? GetService(Type type) => type == typeof(IWindowsFormsEditorService) ? this : null;
        public void CloseDropDown() { }
        public void DropDownControl(Control? control) => throw new InvalidOperationException("The shared resource picker is modal.");
        public DialogResult ShowDialog(Form dialog)
        {
            var picker = (EditorChoicePicker)dialog; picker.SetFilter("missing");
            SawMissingChoice = picker.VisibleCount == 1;
            if (accept) picker.Choose();
            return accept ? DialogResult.OK : DialogResult.Cancel;
        }
    }

    private static void TestPreviewsAndSoundRefresh()
    {
        var root = Path.Combine(Path.GetTempPath(), "CR2026-picker-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var previousTextures = GameContentManager.AllTextures.Count;
        try
        {
            var file = Path.Combine(root, "preview.png");
            using (var image = new Bitmap(128, 64))
            {
                using var graphics = System.Drawing.Graphics.FromImage(image); graphics.Clear(System.Drawing.Color.ForestGreen);
                image.Save(file);
            }
            using (var loaded = EditorResourcePreview.LoadImage(file))
            {
                Check(loaded?.Width == 128, "Actual image previews load without requiring the GPU.");
                using var unlocked = File.Open(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            Check(EditorResourcePreview.ResolveFile("preview.png", root) == file, "A declared category resolves the correct file.");
            Check(EditorResourcePreview.ResolveFile("../preview.png", root) == null, "Previews stay inside their resource directory.");
            using var sound = new EditorSoundPicker(root); sound.FileName = "missing.wav";
            var changed = 0; sound.SelectedIndexChanged += (_, _) => changed++; sound.ReloadSounds();
            Check(sound.FileName == "missing.wav" && changed == 0,
                "Opening/refilling a sound list preserves missing assets without changing authored values.");
            var duplicateName = Guid.NewGuid().ToString("N") + ".png";
            var first = Path.Combine(root, duplicateName); File.Copy(file, first); _ = new Texture(first);
            var secondDirectory = Path.Combine(root, "other"); Directory.CreateDirectory(secondDirectory);
            var second = Path.Combine(secondDirectory, duplicateName); File.Copy(file, second); _ = new Texture(second);
            Check(EditorResourcePreview.ResolveFile(duplicateName, null) == null, "Ambiguous resource categories do not show the wrong preview.");
            if (Environment.GetEnvironmentVariable("CODEX_EDITOR_PICKER_PREVIEW") is { Length: > 0 } output)
            {
                using (var stream = typeof(EditorSoundPicker).Assembly.GetManifestResourceStream("Intersect.Editor.studio-logiklik.png"))
                    if (stream != null) { using var logo = Image.FromStream(stream); logo.Save(Path.Combine(root, "studio-logiklik.png")); }
                using var combo = new LogiklikComboBox();
                combo.Items.AddRange(["None", "studio-logiklik.png", "preview.png", "missing.wav"]); combo.SelectedIndex = 1;
                using var picker = new EditorChoicePicker(combo, SearchableChoices.Snapshot(combo), root, []);
                picker.Opacity = 0; picker.Show(); Application.DoEvents();
                typeof(EditorChoicePicker).GetMethod("UpdatePreview", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(picker, null);
                using var bitmap = new Bitmap(picker.Width, picker.Height);
                picker.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                Directory.CreateDirectory(Path.GetDirectoryName(output)!); bitmap.Save(output); picker.Hide();
            }
        }
        finally
        {
            GameContentManager.AllTextures.RemoveRange(previousTextures, GameContentManager.AllTextures.Count - previousTextures);
            Directory.Delete(root, recursive: true);
        }
    }
}
