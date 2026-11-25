using System.Globalization;
using System.Reflection;
using Intersect.Framework.Core.MountRacing;
using Intersect.Framework.Core.Mounts;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Network.Packets.Client;
internal static class MountRaceEditorTests
{
    private static IEnumerable<Control> All(Control root) { foreach (Control c in root.Controls) { yield return c; foreach (var n in All(c)) yield return n; } }
    private static void Check(bool pass, string message) { if (!pass) throw new Exception(message); }
    internal static void Run()
    {
        var old = CultureInfo.CurrentUICulture; const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            foreach (var locale in new[] { "en", "fr", "es" })
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(locale);
                var source = new MountRaceConfiguration { Courses = [new() { Name = "Royal circuit" }] };
                using var form = new Intersect.Editor.Forms.Editors.FrmMountRaceConfiguration(source.ToJson(), MountConfiguration.Prototype().ToJson());
                form.CreateControl(); form.PerformLayout(); var sent = new List<MountRaceRequestPacket>();
                form.GetType().GetProperty("SendRequest", flags)!.SetValue(form, new Action<MountRaceRequestPacket>(sent.Add));
                NumericUpDown Number(string name) => All(form).OfType<NumericUpDown>().Single(c => c.Name == name);
                ComboBox Combo(string name) => All(form).OfType<ComboBox>().Single(c => c.Name == name);
                Check(form.Text == QualityOfLifeText.Get("Royal Races Editor", locale), "Editor title not translated");
                var entry = Intersect.Editor.Forms.EditorHub.HubRegistry.Definitions.Single(d => d.Id == "mountRaceEditor");
                Check(entry.Name == form.Text && entry.Description.ContainsKey(locale), "Race editor missing from translated hub");
                Number("Laps").Value = 5; Number("Millisecondspertile").Value = 220; Number("Maximumracers").Value = 16;
                var checkpoints = All(form).OfType<DataGridView>().Single(); checkpoints.Rows[0].Cells[0].Value = 14;
                var json = (string)form.GetType().GetProperty("DraftJson", flags)!.GetValue(form)!; var working = MountRaceConfiguration.FromJson(json);
                Check(working.Courses[0].Laps == 5 && working.Courses[0].MovementMilliseconds == 220 && working.Courses[0].MaximumPlayers == 16 && working.Courses[0].Checkpoints[0].X == 14, "Rules/checkpoints not stored");
                Check(source.Courses[0].Laps == 3 && source.Courses[0].Checkpoints[0].X == 15, "Source changed before save");
                Check(Combo("Backgroundimage").Text == "racing_background_v1.png" && Combo("Countdownsound").Text == "racing/countdown_v1.wav", "Media pickers lost defaults");
                var save = All(form).OfType<Button>().Single(b => b.Text == QualityOfLifeText.Get("Save", locale));
                form.GetType().GetMethod("SaveDraft", flags)!.Invoke(form, null); Check(sent.Count == 1 && sent[0].Action == MountRaceAction.SaveConfiguration, "Save request missing");
                Check(!save.Enabled, "Save does not wait for acknowledgement");
                form.GetType().GetMethod("SaveResponse", flags)!.Invoke(form, [false, "Race settings could not be saved. Check the circuit, mount and prizes.", Guid.NewGuid()]);
                Check(!save.Enabled, "Stale acknowledgement accepted");
                form.GetType().GetMethod("SaveResponse", flags)!.Invoke(form, [false, "Race settings could not be saved. Check the circuit, mount and prizes.", sent[0].RequestId]);
                Check(save.Enabled, "Matching failure did not reenable editing");
                checkpoints.Rows[0].Cells[0].Value = "invalid"; form.GetType().GetMethod("SaveDraft", flags)!.Invoke(form, null);
                Check(sent.Count == 1, "Invalid checkpoint submitted");
                System.Console.WriteLine($"PASS mount race editor {locale}: circuit fields, mount/maps/media, checkpoint edits, clone, round-trip, save token, invalid input");
            }
        }
        finally { CultureInfo.CurrentUICulture = old; }
    }
}
