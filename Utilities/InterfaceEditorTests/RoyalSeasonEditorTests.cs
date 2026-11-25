using System.Globalization;
using System.Reflection;
using Intersect.Editor.Forms.Editors;
using Intersect.Framework.Core.Dungeons;
using Intersect.Framework.Core.RoyalSeasons;
using Intersect.Network.Packets.RoyalSeasons;
internal static class RoyalSeasonEditorTests
{
    private static IEnumerable<Control> All(Control root) { foreach (Control child in root.Controls) { yield return child; foreach (var nested in All(child)) yield return nested; } }
    private static void Check(bool pass, string message) { if (!pass) throw new Exception(message); }
    private static void Click(Button button) => typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(button, [EventArgs.Empty]);
    private static void Capture(Form form, string path)
    {
        form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new(-20000, -20000);
        form.Show(); Application.DoEvents(); form.PerformLayout();
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(path);
        form.Hide();
    }
    internal static void Run()
    {
        var before = CultureInfo.CurrentUICulture;
        try
        {
            foreach (var language in new[] { "en", "fr", "es" })
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language); string T(string key) => RoyalSeasonText.Get(key, language);
                var sent = new List<RoyalSeasonConfigurationSavePacket>();
                var season = new RoyalSeason { Name = "Summer", Objectives = [new() { Name = "Victory" }], Tiers = [new() { Name = "Tier 1", Free = new() { ItemId = Guid.NewGuid() } }] };
                var cfg = new RoyalSeasonConfiguration { Seasons = [season] };
                using var form = new FrmRoyalSeasons(new() { Json = RoyalSeasonRules.Json(cfg), Revision = "v1" }, sent.Add);
                form.Size = new(800, 600); form.CreateControl(); form.PerformLayout();
                var output = Path.Combine(Directory.GetCurrentDirectory(), "validation-local", "seasons-ui"); Directory.CreateDirectory(output);
                Capture(form, Path.Combine(output, "editor-seasons-" + language + ".png"));
                Button Find(string caption) => All(form).OfType<Button>().Single(b => b.Text == T(caption));
                Check(form.Text == T("Royal seasons"), "Editor untranslated");
                Check(All(form).OfType<ComboBox>().Any(c => c.Text.Contains(season.Tiers[0].Free.ItemId.ToString())), "Missing reference erased");
                All(form).OfType<TextBox>().Single(c => c.Name == "Name").Text = "Season test";
                Click(Find("Save seasons")); Check(sent.Count == 1 && sent[0].Revision == "v1", "Wrong revision or missing save");
                Check(!Find("Save seasons").Enabled, "Save request not protected");
                var saved = RoyalSeasonRules.Read<RoyalSeasonConfiguration>(sent[0].Json);
                Check(saved.Seasons[0].Id == season.Id && saved.Seasons[0].Name == "Season test" && saved.Seasons[0].Tiers[0].Free.ItemId == season.Tiers[0].Free.ItemId, "Draft lost names, ids or rewards");
                form.Acknowledge(new() { Error = "Choose existing reward items." });
                Check(Find("Save seasons").Enabled && All(form).OfType<Label>().Any(l => l.Text == T("Choose existing reward items.")), "Server rejection not recoverable");
                Click(Find("Save seasons")); form.Acknowledge(new() { Revision = "v2" });
                Click(Find("Save seasons")); Check(sent.Last().Revision == "v2", "Acknowledged revision not used");
                typeof(FrmRoyalSeasons).GetMethod("SaveTimedOut", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, []);
                Check(!Find("Save seasons").Enabled, "Unconfirmed request can be duplicated");
                var closing = new FormClosingEventArgs(CloseReason.UserClosing, false);
                typeof(Form).GetMethod("OnFormClosing", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, [closing]); Check(!closing.Cancel, "Timeout traps editor");
                season.Enabled = true; season.StartsAt = DateTimeOffset.UtcNow.AddHours(-1);
                using var locked = new FrmRoyalSeasons(new() { Json = RoyalSeasonRules.Json(cfg) }, _ => { }); locked.CreateControl();
                Check(!All(locked).OfType<TextBox>().Single(c => c.Name == "Name").Enabled, "Started season editable");
                var modifier = new DungeonModifierConfiguration { Enabled = true, Pool = [new() { Name = "Elite", EnemyHealthPercent = 125 }] };
                using var mods = new FrmDungeonModifiers(modifier); mods.Size = new(700, 500); mods.CreateControl(); mods.PerformLayout();
                Capture(mods, Path.Combine(output, "editor-modifiers-" + language + ".png"));
                foreach (var field in All(mods).Where(c => c is ComboBox or NumericUpDown))
                    Check(mods.PointToClient(field.PointToScreen(new Point(field.Width, 0))).X <= mods.ClientSize.Width, "Modifier input extends beyond the window: " + field.Name);
                All(mods).OfType<NumericUpDown>().Single(c => c.Name == "Enemy health (%)").Value = 175;
                All(mods).OfType<NumericUpDown>().Single(c => c.Name == "Completion experience (%)").Value = 150;
                Check(modifier.Pool[0].EnemyHealthPercent == 125, "Cancel mutates original dungeon");
                Click(All(mods).OfType<Button>().Single(b => b.Text == T("Apply to dungeon")));
                Check(mods.DialogResult == DialogResult.OK && mods.Draft.Pool[0].EnemyHealthPercent == 175 && mods.Draft.Pool[0].CompletionExperiencePercent == 150, "Modifier editor discards values");
                System.Console.WriteLine("PASS seasons/modifiers editor: translations, drafts, stable references, pending saves, acknowledgments and frozen seasons / " + language);
            }
        }
        finally { CultureInfo.CurrentUICulture = before; }
    }
}
