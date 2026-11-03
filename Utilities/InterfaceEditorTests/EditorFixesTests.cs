using System.Reflection;
using Intersect.Editor.Forms.DockingElements;
using Intersect.Editor.Forms.Editors;
using Intersect.Editor.Forms.Styling;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Crafting;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Client.Maps;
using Newtonsoft.Json;
using Intersect.Localization;

internal static class EditorFixesTests
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    internal static void Run()
    {
        TestScroll(); TestDialogs(); TestPreviews(); TestCampfireTable(); TestRain();
        foreach (var text in new[] { "This entry has no visual resource configured.", "Use this table as the camp fire creator",
            "Adds existing campfire recipes to this table. Other recipes are kept." })
        {
            foreach (var language in GameTextCatalog.Languages)
                Check(QualityOfLifeText.Catalog.Entries[text].ContainsKey(language) &&
                    GameTextTokens.Compatible(text, QualityOfLifeText.Get(text, language)), "Locale coverage: " + text);
            Check(QualityOfLifeText.Get(text, "unknown") == text, "English fallback: " + text);
        }
        Console.WriteLine("PASS editor fixes: scroll reachability, nested modal ownership, identity previews, animation frames, campfire tables, rain transitions and locales");
    }
    private static void TestScroll()
    {
        using var layers = new FrmMapLayers { Opacity = 0, ShowInTaskbar = false };
        // Do not run the network-dependent Load handler in this isolated UI fixture.
        RemoveLoad(layers);
        layers.Show();
        foreach (var size in new[] { new Size(410, 590), new Size(314, 320), new Size(410, 900) })
        {
            layers.Size = size;
            foreach (var name in new[] { "pnlNpcs", "pnlAttributes", "pnlLights" })
            {
                var page = (Panel)layers.Controls.Find(name, true).Single();
                page.Show(); page.BringToFront(); Application.DoEvents(); page.PerformLayout();
                Check(page.AutoScroll, "Each fixed-content page scrolls.");
                var bottom = page.Controls.Cast<Control>().Where(c => c.Visible).OrderByDescending(c => c.Bottom).First();
                page.ScrollControlIntoView(bottom); Application.DoEvents();
                Check(bottom.Bottom <= page.ClientSize.Height, "The last visible control is reachable at " + size + ": " + name);
                page.AutoScrollPosition = Point.Empty;
            }
        }
        layers.Hide();
    }
    private static void RemoveLoad(Form form)
    {
        var events = (System.ComponentModel.EventHandlerList)typeof(System.ComponentModel.Component)
            .GetProperty("Events", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
        var key = typeof(Form).GetFields(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(f => f.FieldType == typeof(object) && f.Name.Contains("load", StringComparison.OrdinalIgnoreCase)).GetValue(null)!;
        if (events[key] is EventHandler load) form.Load -= load;
    }
    private static void TestDialogs()
    {
        EditorTheme.Install();
        using var owner = new Form { Opacity = 0, ShowInTaskbar = false };
        owner.Show();
        using var wizard = new Form { Opacity = 0, ShowInTaskbar = false };
        EditorWindows.Show(wizard, owner);
        foreach (var caption in new[] { "Spell Auto Creator fixture", "Item Auto Creator fixture", "Spell Auto Creator fixture" })
        {
            using var timer = new System.Windows.Forms.Timer { Interval = 25 };
            timer.Tick += (_, _) =>
            {
                var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Text == caption);
                if (dialog == null) return;
                Check(dialog.Owner == wizard && wizard.Owner == owner, "Modal ownership must point from child to parent.");
                timer.Stop(); dialog.DialogResult = DialogResult.OK;
            };
            timer.Start();
            Check(LogiklikMessageBox.Show(wizard, "Generated profile.", caption) == DialogResult.OK,
                "Repeated spell confirmations return their modal result without a cycle.");
        }
        wizard.Hide(); owner.Hide();
    }
    private static void TestPreviews()
    {
        var oldDirectory = Environment.CurrentDirectory;
        var folder = Path.Combine(Path.GetTempPath(), "LogiklikPreview-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(folder, "resources", "entities"));
        Directory.CreateDirectory(Path.Combine(folder, "resources", "animations"));
        var a = new NPCDescriptor(Guid.NewGuid()) { Name = "Same enemy", Sprite = "enemy.png" };
        var b = new NPCDescriptor(Guid.NewGuid()) { Name = "Same enemy", Sprite = "missing.png" };
        NPCDescriptor.Lookup.Set(a.Id, a); NPCDescriptor.Lookup.Set(b.Id, b);
        try
        {
            Environment.CurrentDirectory = folder;
            using (var sheet = new Bitmap(40, 40))
            {
                using var g = Graphics.FromImage(sheet); g.Clear(System.Drawing.Color.Red);
                g.FillRectangle(Brushes.Blue, 10, 0, 10, 10);
                sheet.Save(Path.Combine(folder, "resources", "entities", "enemy.png"));
                sheet.Save(Path.Combine(folder, "resources", "animations", "effect.png"));
            }
            using var source = new LogiklikComboBox(); source.Items.Add("None");
            SearchableChoices.AddNames<NPCDescriptor>(source);
            var choices = SearchableChoices.Snapshot(source);
            Check(choices[0].Descriptor == null && choices.Skip(1).Select(choice => choice.Descriptor).ToHashSet().SetEquals(new[] { a, b }),
                "Preview identities preserve duplicate names and the leading None entry.");
            using var preview = EditorObjectPreview.Create(a);
            using var frame0 = preview.Render(0); using var frame1 = preview.Render(180);
            Check(frame0 is { Width: 10, Height: 10 } && frame1 != null && frame0.GetPixel(5, 5) != frame1.GetPixel(5, 5),
                "NPC previews crop and animate one direction instead of showing the entire sheet.");
            using var missing = EditorObjectPreview.Create(b);
            Check(missing.Render() == null, "Missing sprites leave a selectable entry without inventing another image.");
            var animation = new AnimationDescriptor(Guid.NewGuid());
            animation.Lower.Sprite = "effect.png"; animation.Lower.XFrames = 4; animation.Lower.YFrames = 4;
            animation.Lower.FrameCount = 2; animation.Lower.FrameSpeed = 100;
            using var animated = EditorObjectPreview.Create(animation);
            using var effect0 = animated.Render(0); using var effect1 = animated.Render(100);
            Check(animated.Animated && effect0 != null && effect1 != null && effect0.GetPixel(5,5) != effect1.GetPixel(5,5),
                "Animation descriptors use their configured frame count and timing.");
            // Decoders release the files even while previews remain alive.
            File.Move(Path.Combine(folder, "resources", "entities", "enemy.png"), Path.Combine(folder, "moved.png"));
        }
        finally
        {
            Environment.CurrentDirectory = oldDirectory;
            NPCDescriptor.Lookup.DeleteAt(a.Id); NPCDescriptor.Lookup.DeleteAt(b.Id);
            Directory.Delete(folder, true);
        }
    }
    private static void TestCampfireTable()
    {
        var fire = new CraftingRecipeDescriptor(Guid.NewGuid()) { Campfire = new()
            { FireAnimationId = Guid.NewGuid(), AshesAnimationId = Guid.NewGuid() } };
        var normal = new CraftingRecipeDescriptor(Guid.NewGuid());
        CraftingRecipeDescriptor.Lookup.Set(fire.Id, fire); CraftingRecipeDescriptor.Lookup.Set(normal.Id, normal);
        try
        {
            var table = new CraftingTableDescriptor(Guid.NewGuid()); table.Crafts.Add(normal.Id);
            FrmCraftingTables.ConfigureCampfireTable(table, true); FrmCraftingTables.ConfigureCampfireTable(table, true);
            Check(table.Crafts.Count(id => id == fire.Id) == 1 && table.Crafts.Contains(normal.Id), "Enabling adds campfire recipes once and keeps other recipes.");
            var saved = JsonConvert.DeserializeObject<CraftingTableDescriptor>(table.JsonData)!;
            Check(saved.Crafts.Contains(fire.Id), "The normal table format persists campfire recipe membership.");
            FrmCraftingTables.ConfigureCampfireTable(table, false);
            Check(!table.Crafts.Contains(fire.Id) && table.Crafts.Contains(normal.Id), "Disabling removes only campfire recipes.");
        }
        finally { CraftingRecipeDescriptor.Lookup.DeleteAt(fire.Id); CraftingRecipeDescriptor.Lookup.DeleteAt(normal.Id); }
    }
    private static void TestRain()
    {
        var rain = new WeatherTransition();
        Check(rain.Update(true, 1000) == 0, "Rain starts transparent.");
        Check(Math.Abs(rain.Update(true, 6000) - .5f) < .001f, "Rain reaches half strength after five seconds.");
        Check(rain.Update(true, 11000) == 1, "Rain reaches full strength after ten seconds.");
        Check(Math.Abs(rain.Update(false, 16000) - .5f) < .001f, "Rain fades instead of disappearing.");
        Check(rain.Update(true, 16000) == .5f, "Reversing a transition does not jump.");
        Check(rain.Update(false, 21000) == 0, "Stopped rain finishes fading.");
        var frequent = new WeatherTransition(); frequent.Update(true, 0);
        for (var i = 1; i <= 100; i++) frequent.Update(true, i * 100);
        Check(Math.Abs(frequent.Amount - 1) < .00001f, "Duration is independent of frame rate.");
    }
}
