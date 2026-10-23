using System.Globalization;
using System.Reflection;
using Intersect.Editor.Forms.Editors;
using Intersect.Framework.Core.Mounts;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Localization;

internal static class MountEditorTests
{
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    private static T Field<T>(object o, string key) => (T)o.GetType().GetField(key, Hidden)!.GetValue(o)!;
    private static void Call(object o, string method, params object[] args) => o.GetType().GetMethod(method, Hidden)!.Invoke(o, args);
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    internal static void Run()
    {
        var directory = Environment.CurrentDirectory;
        var fixture = Path.Combine(Path.GetTempPath(), "mount-editor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        try
        {
            Environment.CurrentDirectory = fixture;
            var input = Path.Combine(fixture, "source"); Directory.CreateDirectory(input);
            var paths = new List<string>();
            foreach (var (phase, frames) in new[] { ("Idle", 4), ("Move_1_Start", 6), ("Move_2_Loop", 6), ("Move_3_End", 6) })
            {
                var path = Path.Combine(input, "Test_Horse_" + phase + ".png");
                using var image = new Bitmap(128 * frames, 512);
                using var graphics = Graphics.FromImage(image); graphics.Clear(System.Drawing.Color.Transparent);
                for (var row = 0; row < 4; row++) for (var frame = 0; frame < frames; frame++)
                    graphics.FillRectangle(Brushes.SaddleBrown, frame * 128 + 40, row * 128 + 45, 48, 35);
                image.Save(path); paths.Add(path);
            }
            var original = MountConfiguration.Prototype();
            using var editor = new FrmMountConfiguration(original.ToJson()) { Opacity = 0, ShowInTaskbar = false };
            editor.Show(); Application.DoEvents();
            var fields = Field<Dictionary<string, Control>>(editor, "_fields");
            var selected = Field<MountDefinition>(editor, "_selected");
            editor.ImportSheetFiles(paths);
            Check(selected.IdleFrames == 4 && selected.LoopFrames == 6 && selected.StartFrames == 6 && selected.EndFrames == 6,
                "Importer identifies idle/start/loop/end and their independent frame counts.");
            foreach (var path in paths) using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            ((NumericUpDown)fields["SeatX"]).Value = 70;
            ((NumericUpDown)fields["RiderScale"]).Value = 1.25m;
            Check(selected.Seats[1].X == 70 && selected.RiderScale == 1.25, "Rider settings update the selected direction and live draft.");
            var direction = Field<ComboBox>(editor, "_direction"); direction.SelectedIndex = 2;
            Check(((NumericUpDown)fields["SeatX"]).Value == 64, "Directions keep independent saddle positions.");
            ((NumericUpDown)fields["CorrectionY"]).Value = -3;
            Check(selected.FrameOffsets.Single().Direction == 2 && selected.FrameOffsets.Single().Y == -3, "A frame correction affects only its selected direction/phase/frame.");
            Check(original.Mounts[0].Seats[1].X == 64, "Editing never mutates the original configuration.");
            var config = Field<MountConfiguration>(editor, "_config");
            var reopened = MountConfiguration.FromJson(config.ToJson());
            Check(reopened.Mounts[0].Seats[1].X == 70 && reopened.Mounts[0].FrameOffsets.Single().Y == -3, "Both direction and frame corrections survive save/reopen JSON.");
            var duplicateText = QualityOfLifeText.Get("Duplicate", CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
            Descendants(editor).OfType<Button>().Single(b => b.Text == duplicateText).PerformClick();
            Check(config.Mounts.Length == 2 && config.Mounts[0].Id != config.Mounts[1].Id &&
                !ReferenceEquals(config.Mounts[0].Seats, config.Mounts[1].Seats), "Duplicate creates an independent entry with a fresh identity.");
            var deleteText = QualityOfLifeText.Get("Delete", CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
            Descendants(editor).OfType<Button>().Single(b => b.Text == deleteText).PerformClick();
            Check(config.Mounts.Length == 1, "Delete removes only the selected mount draft.");
            Call(editor, "SaveDraft");
            Check(Field<bool>(editor, "_saving") && !Field<Button>(editor, "_save").Enabled && !editor.IsDisposed,
                "Save waits for the server confirmation instead of discarding the open draft.");
            editor.SaveResponse(true, "", Guid.NewGuid());
            Check(!editor.IsDisposed && Field<bool>(editor, "_saving"), "An unrelated or stale save acknowledgement cannot close the draft.");
            editor.SaveResponse(false, "Mount configuration could not be saved.");
            Check(!Field<bool>(editor, "_saving") && Field<Button>(editor, "_save").Enabled, "Save errors retain an editable draft.");
            Call(editor, "SaveDraft"); editor.SaveResponse(true, "");
            Check(editor.IsDisposed, "A confirmed server save closes the editor.");

            foreach (var text in Labels)
            {
                Check(QualityOfLifeText.Catalog.Entries.TryGetValue(text, out var entries), "Missing mount label: " + text);
                foreach (var language in GameTextCatalog.Languages)
                    Check(entries!.ContainsKey(language) && GameTextTokens.Compatible(text, entries[language]), "Mount label has compatible en/fr/es: " + text);
                Check(QualityOfLifeText.Get(text, "unknown") == text, "Mount labels fall back to English.");
            }
            Console.WriteLine("PASS mount editor: real sheet import, released file handles, independent direction/frame placement, draft isolation, save confirmation and en/fr/es");
        }
        finally { Environment.CurrentDirectory = directory; Directory.Delete(fixture, true); }
    }
    internal static void Preview()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr");
            var prototype = MountConfiguration.Prototype();
            if (Environment.GetEnvironmentVariable("CODEX_MOUNT_CONFIGURATION_FILE") is { Length: > 0 } configurationFile)
                File.WriteAllText(configurationFile, prototype.ToJson());
            using var editor = new FrmMountConfiguration(prototype.ToJson()) { Opacity = 0, ShowInTaskbar = false };
            editor.Show(); Application.DoEvents();
            var tabs = Descendants(editor).OfType<TabControl>().First(); tabs.SelectedIndex = 2;
            Application.DoEvents();
            using var image = new Bitmap(editor.Width, editor.Height);
            editor.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height));
            image.Save(Environment.GetEnvironmentVariable("CODEX_MOUNT_PREVIEW_FILE")!);
            editor.Close();
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }
    private static IEnumerable<Control> Descendants(Control root)
    { foreach (Control child in root.Controls) { yield return child; foreach (var item in Descendants(child)) yield return item; } }
    internal static readonly string[] Labels = [
        "Mounts", "Stable", "Farm horse", "New mount", "Mount Editor", "Duplicate", "Delete", "Enable mounts", "Mount name", "Unlocked for everyone",
        "Allow indoors", "Speed multiplier", "Movement loop", "Idle", "Start", "Loop", "End", "Stop", "Frames: Idle", "Frames: Start",
        "Frames: Loop", "Frames: End", "Frame width", "Frame height", "Milliseconds per frame", "Import animal sheets", "Rider placement",
        "Direction", "Animation", "Frame correction", "Down", "Left", "Right", "Up", "Seat X", "Seat Y", "Foreground starts at Y", "Hide rider below Y",
        "Frame offset X", "Frame offset Y", "Animal scale", "Rider scale", "Hip position ratio", "Rider bob", "Mask lower legs", "Ground anchor X",
        "Ground anchor Y", "Moving preview", "Load rider sprite", "Add paperdoll", "Clear rider", "Unlock item (reusable)", "Mount unlocked.",
        "This mount is not unlocked.", "You cannot ride here or during combat.", "Mount summoned.", "Unlocked only", "Ride favorite / Dismount",
        "Ride", "Dismount", "Favorite mount", "Set favorite", "Choose a favorite mount first.", "Mounts are disabled.", "No mounts match your search."
    ];
}
