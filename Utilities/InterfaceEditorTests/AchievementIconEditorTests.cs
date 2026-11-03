using System.Globalization;
using System.Reflection;
using Intersect.Editor.Forms.Editors;
using Intersect.Framework.Core.Achievements;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Localization;

internal static class AchievementIconEditorTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static object Field(object instance, string name) => instance.GetType().GetField(name, Hidden)!.GetValue(instance)!;
    private static void Call(object instance, string name, params object?[] args) => instance.GetType().GetMethod(name, Hidden)!.Invoke(instance, args);
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    internal static void Run()
    {
        var previous = AchievementConfiguration.Instance.ToJson(); var directory = Environment.CurrentDirectory;
        var culture = CultureInfo.CurrentUICulture;
        var fixture = Path.Combine(Path.GetTempPath(), "achievement-icons-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(fixture, "resources", "achievements", "nested"));
        try
        {
            Environment.CurrentDirectory = fixture; CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr");
            foreach (var (name, color) in new[] { ("unlocked.png", System.Drawing.Color.Gold), ("nested/locked.png", System.Drawing.Color.SlateGray) })
            {
                using var image = new Bitmap(64, 64); using var graphics = Graphics.FromImage(image);
                graphics.Clear(color); image.Save(Path.Combine("resources", "achievements", name));
            }
            var achievement = new AchievementDefinition { Name = "Icon fixture", Icon = "unlocked.png", LockedIcon = "nested/locked.png",
                SteamApiName = "UNCHANGED_API_NAME", Reward = new() { Experience = 123 } };
            AchievementConfiguration.Load(new AchievementConfiguration { Achievements = [achievement, new() { Name = "Legacy fixture", Icon = "unlocked.png" }] }.ToJson());
            using var editor = new FrmAchievementConfiguration { Opacity = 0, ShowInTaskbar = false };
            Intersect.Editor.Forms.Styling.EditorTheme.Apply(editor);
            editor.Show(); Application.DoEvents();
            var unlocked = (ComboBox)Field(editor, "_icon"); var locked = (ComboBox)Field(editor, "_lockedIcon");
            var unlockedPreview = (PictureBox)Field(editor, "_unlockedPreview"); var lockedPreview = (PictureBox)Field(editor, "_lockedPreview");
            Check(unlocked.Text == achievement.Icon && locked.Text == achievement.LockedIcon &&
                unlockedPreview.Image != null && lockedPreview.Image != null, "Both saved icons load into independently previewed searchable selectors.");
            foreach (var file in new[] { "unlocked.png", "nested/locked.png" })
                using (File.Open(Path.Combine("resources", "achievements", file), FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            Check(editor.Controls.Find("AchievementLockedIcon", true).Length == 1, "The locked icon has its own editor field.");
            Check(Descendants(editor).OfType<TabControl>().First().TabPages[0].AutoScroll,
                "Icon rows do not hide the remaining settings on a small window.");
            if (Environment.GetEnvironmentVariable("CODEX_ACHIEVEMENT_ICONS_PREVIEW") is { Length: > 0 } output)
            {
                using var image = new Bitmap(editor.Width, editor.Height);
                editor.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height)); image.Save(output);
            }
            locked.SelectedIndex = 0; Call(editor, "CommitSelected");
            var selected = (AchievementDefinition)Field(editor, "_selected");
            Check(selected.LockedIcon == "" && selected.Icon == "unlocked.png" && lockedPreview.Image != null &&
                (bool)Field(editor, "_lockedPreviewFallback"), "Clearing only the locked icon uses a dimmed preview without clearing the unlocked icon.");
            Call(editor, "SelectIcon", "missing-locked.png", locked); Call(editor, "CommitSelected");
            Check(selected.LockedIcon == "missing-locked.png" && selected.Reward.Experience == 123 && selected.SteamApiName == "UNCHANGED_API_NAME",
                "Missing authored filenames, rewards and Steam names are preserved while editing icons.");
            var list = (ListBox)Field(editor, "_list"); list.SelectedIndex = 1; Application.DoEvents();
            Check(locked.SelectedIndex == 0 && locked.Text == QualityOfLifeText.Get("None", "fr") && unlocked.Text == "unlocked.png",
                "Switching achievements reloads each icon independently with a localized empty choice.");
            list.SelectedIndex = 0; Application.DoEvents(); Check(locked.Text == "missing-locked.png", "An unresolved locked icon survives switching selections.");
            Check(AchievementConfiguration.Instance.Find(achievement.Id)!.LockedIcon == "nested/locked.png", "Draft icon edits never change live configuration before Save.");
            var working = (AchievementConfiguration)Field(editor, "_working");
            Check(AchievementConfiguration.FromJson(working.ToJson()).Find(achievement.Id)!.LockedIcon == "missing-locked.png", "The editor draft serializes both icon fields.");
            ((List<AchievementDefinition>)Field(editor, "_achievements")).Clear();
            Call(editor, "RefreshList", (object?)null);
            Check(unlockedPreview.Image == null && lockedPreview.Image == null, "An empty selection clears both previews.");
            editor.Hide();
            foreach (var text in new[] { "Unlocked icon (resources/achievements)", "Locked icon (optional)", "Icon previews", "Locked", "Unlocked",
                "Without a locked image, the normal icon is dimmed and marked with a lock." })
            {
                foreach (var language in GameTextCatalog.Languages)
                    Check(QualityOfLifeText.Catalog.Entries[text].ContainsKey(language) && GameTextTokens.Compatible(text, QualityOfLifeText.Get(text, language)), "Achievement icon labels cover en/fr/es.");
                Check(QualityOfLifeText.Get(text, "unknown") == text, "Achievement icon labels fall back to English.");
            }
            Console.WriteLine("PASS achievement icon editor: independent images/previews, unlocked files, missing values, draft isolation, selection switching, persistence and en/fr/es");
        }
        finally
        {
            Environment.CurrentDirectory = directory; CultureInfo.CurrentUICulture = culture;
            AchievementConfiguration.Load(previous); Directory.Delete(fixture, true);
        }
    }
    private static IEnumerable<Control> Descendants(Control root)
    { foreach (Control child in root.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; } }
}
