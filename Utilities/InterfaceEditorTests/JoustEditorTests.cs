using System.Globalization;
using System.Reflection;
using Intersect.Editor.Forms.Editors;
using Intersect.Editor.Forms.Cinematics;
using Intersect.Editor.Forms.Styling;
using Intersect.Editor.General;
using Intersect.Editor.Maps;
using Intersect.Framework.Core.Jousting;
using Intersect.Framework.Core.Mounts;
using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.QualityOfLife;
internal static class JoustEditorTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Hidden)!.GetValue(owner)!;
    private static void Call(object owner, string name) => owner.GetType().GetMethod(name, Hidden)!.Invoke(owner, null);
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static IEnumerable<Control> All(Control c) { yield return c; foreach (Control child in c.Controls) foreach (var x in All(child)) yield return x; }
    internal static void Run()
    {
        var previous = Globals.CurrentMap; var culture = CultureInfo.CurrentUICulture;
        try
        {
            foreach (var lang in new[] { "en", "fr", "es" })
            {
                CultureInfo.CurrentUICulture = new(lang); Globals.CurrentMap = null;
                var map = new MapInstance(Guid.NewGuid()) { Name = "Herbonia" };
                var source = new JoustArena { MapId = map.Id };
                using var form = new FrmJoustConfiguration(new JoustConfiguration { Arenas = [source] }.ToJson());
                var config = Field<JoustConfiguration>(form, "_config"); var arena = config.Arenas[0];
                var list = Field<ListBox>(form, "_list");
                Check(list.Items[0].ToString() == "Herbonia", "Arena list must show its name, never the CLR type.");
                Check(!All(form).OfType<PropertyGrid>().Any(), "No raw GUID property grid in the setup workflow.");
                var mount = MountConfiguration.Prototype(); mount.Mounts[0].Name = "William"; mount.Mounts[0].IntegratedRider = true;
                form.ApplyMounts(mount.ToJson());
                var picker = Field<ComboBox>(form, "_mount"); Check(picker.Items.Count == 2 && picker.Items[1].ToString() == "William", "Named mount picker is populated by the server configuration.");
                picker.SelectedIndex = 1; Check(arena.MountId == mount.Mounts[0].Id, "Choosing William writes his actual identity.");
                Field<TextBox>(form, "_name").Text = "Lice royale";
                Check(arena.Name == "Lice royale" && list.SelectedIndex == 0, "Renaming retains selection and updates the arena draft.");
                Field<CheckBox>(form, "_enabled").Checked = true;
                form.RenderPreview = _ => {
                    var b = new Bitmap(Intersect.Options.Instance.Map.MapWidth * Intersect.Options.Instance.Map.TileWidth, Intersect.Options.Instance.Map.MapHeight * Intersect.Options.Instance.Map.TileHeight);
                    using var g = Graphics.FromImage(b); g.Clear(Color.FromArgb(51, 74, 54));
                    g.DrawString("TEST — SYNTHETIC MAP", SystemFonts.DefaultFont, Brushes.White, 20, 20); return b;
                };
                Globals.CurrentMap = map; Call(form, "LoadArena");
                var evt = new EventDescriptor(Guid.NewGuid(), map.Id, 0, 0) { Name = "Arrivée du roi", Pages = [new EventPage { Cinematic = JoustCinematics.Create(arena, JoustCinematicKind.Introduction, s => s) }] };
                evt.Pages[0].Cinematic!.Name = "CUSTOM SCENE TO PRESERVE"; map.LocalEvents[evt.Id] = evt; arena.IntroductionEventId = evt.Id;
                Call(form, "LoadArena");
                var scenes = Field<Dictionary<JoustCinematicKind, ComboBox>>(form, "_scenes");
                Check(scenes[JoustCinematicKind.Introduction].SelectedItem!.ToString() == "Arrivée du roi", "Native scenes are selected by name.");
                using var director = form.CreateDirector(JoustCinematicKind.Introduction)!;
                Globals.CurrentMap = null; director.Show(); Application.DoEvents();
                Check(Field<Guid>(director, "_eventId") == evt.Id && Field<CinematicScene>(director, "_scene").Name == "CUSTOM SCENE TO PRESERVE", "Reopening keeps the existing scene identity and authored content.");
                director.Hide(); Globals.CurrentMap = map;
                var numbers = Field<Dictionary<string, NumericUpDown>>(form, "_numbers"); numbers["Passes"].Value = 5;
                Check(arena.Rules.Passes == 5, "Named numeric fields edit the actual rule draft.");
                arena.EndX = 29; Call(form, "ValidateSetup");
                var diagnosis = Field<Label>(form, "_check").Text;
                Check(diagnosis.Contains(QualityOfLifeText.Get("The track needs an even distance of 4 to 64 tiles.", lang)), "Invalid odd tracks explain the correction.");
                arena.EndX = 26; Call(form, "LoadArena");
                Check(JoustConfiguration.FromJson(config.ToJson()).Arenas[0].MountId == mount.Mounts[0].Id, "Resource selections survive save/reopen serialization.");
                form.Size = new Size(1280, 820); EditorTheme.Apply(form); form.Show(); Application.DoEvents();
                var tabs = All(form).OfType<System.Windows.Forms.TabControl>().Single();
                Check(Enumerable.Range(0, 5).All(i => tabs.GetTabRect(i).Right <= tabs.ClientSize.Width && tabs.GetTabRect(i).Top == tabs.GetTabRect(0).Top), "All five steps stay visible in order on one row after theming.");
                var picture = Field<PictureBox>(form, "_map");
                void Place(string button, int x, int y)
                {
                    All(form).OfType<Button>().Single(b => b.Text == QualityOfLifeText.Get(button, lang)).PerformClick();
                    var image = picture.Image!; var scale = Math.Min(picture.Width / (float)image.Width, picture.Height / (float)image.Height);
                    var px = (int)((picture.Width - image.Width * scale) / 2 + (x + .5) * Intersect.Options.Instance.Map.TileWidth * scale);
                    var py = (int)((picture.Height - image.Height * scale) / 2 + (y + .5) * Intersect.Options.Instance.Map.TileHeight * scale);
                    typeof(Control).GetMethod("OnMouseClick", Hidden)!.Invoke(picture, [new MouseEventArgs(MouseButtons.Left, 1, px, py, 0)]);
                }
                Place("Place finish", 24, 18); Place("Place opponent lane", 10, 18);
                Check(arena.EndX == 24 && arena.EndY == arena.StartY && arena.OpponentY == 18, "Visible placement buttons and normal clicks place both tracks without modifier keys.");
                var save = Field<Button>(form, "_save"); save.PerformClick();
                var request = Field<Guid>(form, "_request"); Check(!save.Enabled && request != Guid.Empty, "Save waits for the server acknowledgement.");
                Field<TextBox>(form, "_name").Text = "Arène royale";
                form.Response(new() { RequestId = Guid.NewGuid(), Saved = true }); Check(!save.Enabled, "Unrelated acknowledgements cannot confirm this draft.");
                form.Response(new() { RequestId = request, Saved = true }); Check(save.Enabled && Field<bool>(form, "_dirty"), "A save acknowledgement cannot mark newer edits as saved.");
                save.PerformClick(); request = Field<Guid>(form, "_request");
                form.Response(new() { RequestId = request, Saved = true }); Check(save.Enabled && !Field<bool>(form, "_dirty"), "Confirmed save retains the visible editable arena.");
                if (Environment.GetEnvironmentVariable("CODEX_UI_PREVIEW") is { Length: > 0 } folder)
                {
                    Directory.CreateDirectory(folder); using var bitmap = new Bitmap(form.Width, form.Height);
                    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(folder, "joust-editor-" + lang + ".png"));
                }
                form.Hide(); Globals.CurrentMap = null;
            }
            Console.WriteLine("PASS joust editor: named resources, no GUID entry, real draft changes, exact diagnostics, native scene reopening and en/fr/es layouts.");
        }
        finally { Globals.CurrentMap = previous; CultureInfo.CurrentUICulture = culture; }
    }
}
