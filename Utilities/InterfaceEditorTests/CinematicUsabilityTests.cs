using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Intersect.Editor.Forms.Cinematics;
using Intersect.Editor.Forms.EditorHub;
using Intersect.Editor.Forms.Editors.Events;
using Intersect.Editor.Forms.Editors.Events.Event_Commands;
using Intersect.Editor.Maps;
using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.NPCs;

internal static class CinematicUsabilityTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static IEnumerable<Control> All(Control root)
    { yield return root; foreach (Control child in root.Controls) foreach (var item in All(child)) yield return item; }
    private static void Save(Control control, string name)
    {
        if (Environment.GetEnvironmentVariable("CODEX_UI_PREVIEW") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder);
        using var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(Path.Combine(folder, name + ".png"));
    }
    internal static void Run()
    {
        CultureInfo.CurrentUICulture = new("fr-FR");
        TestTextTranslation(); TestPicker(); TestCommands(); TestDirector(); TestHub();
        using var stream = typeof(CinematicText).Assembly.GetManifestResourceStream("Cinematics.Texts.json")!;
        var catalog = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(stream)!;
        foreach (var pair in catalog)
            foreach (var lang in new[] { "en", "fr", "es" })
                Check(pair.Value.TryGetValue(lang, out var text) && !string.IsNullOrWhiteSpace(text), "Missing locale: " + pair.Key);
        CultureInfo.CurrentUICulture = new("de-DE");
        Check(CinematicText.T("SearchChoices") == catalog["SearchChoices"]["en"], "English fallback");
        Console.WriteLine("PASS cinematic usability: scroll text, searchable scene/chapter choices, seconds, map selection, action drag-and-drop onto map/timeline, NPC drops, rejected targets, menus, locales and responsive previews.");
    }
    private static void TestTextTranslation()
    {
        using var bitmap = new Bitmap(220, 120);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.Black); g.TranslateTransform(0, -80);
        EditorHubView.DrawText(g, "VISIBLE AFTER SCROLL", SystemFonts.DefaultFont, new Rectangle(8, 90, 200, 25), Color.White);
        var pixels = 0;
        for (var y = 10; y < 35; y++) for (var x = 8; x < 208; x++)
            if (bitmap.GetPixel(x, y).R > 100) pixels++;
        Check(pixels > 30, "GDI text must follow the same scroll transform as card backgrounds and images.");
        using var art = new HubArt();
        Check(art.Logo is { Width: >= 512 }, "Hub uses the high resolution brand asset.");
    }
    private sealed record Choice(Guid Id, string Name) { public override string ToString() => Name; }
    private static void TestPicker()
    {
        using var picker = new CinematicPicker<Choice>();
        var a = new Choice(Guid.NewGuid(), "Épée"); var b = new Choice(Guid.NewGuid(), "Épée");
        picker.SetChoices([a, b], b);
        var search = All(picker).OfType<TextBox>().Single();
        var list = All(picker).OfType<ListBox>().Single();
        search.Text = "epee";
        Check(list.Items.Count == 2 && ReferenceEquals(picker.SelectedItem, b), "Accent-insensitive search preserves duplicate-name identity.");
        search.Text = "missing";
        Check(picker.SelectedItem == null, "No hidden selection can be applied.");
        search.Clear();
        Check(ReferenceEquals(picker.SelectedItem, b), "Selection restores after clearing the filter.");
    }
    private static void TestCommands()
    {
        var map = new MapInstance(Guid.NewGuid()) { Name = "Village" };
        var evt = new EventDescriptor(Guid.NewGuid(), map.Id, 1, 1) { Name = "Arrivée du roi" };
        evt.Pages[0].Cinematic = new() { Name = evt.Name, MapId = map.Id };
        map.LocalEvents.Add(evt.Id, evt);
        using var editor = new FrmEvent(map) { MyMap = map };
        using var host = new Form { Size = new(700, 580), ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-20000, -20000) };
        var play = new PlayCinematicCommand();
        using (var control = new EventCommandCinematic(play, editor) { Dock = DockStyle.Fill })
        {
            host.Controls.Add(control); host.Show(); Application.DoEvents();
            Check(!All(control).OfType<PropertyGrid>().Any(), "Beginner commands do not expose GUID grids.");
            var list = All(control).OfType<ListBox>().Single();
            list.SelectedIndex = 0;
            Check(play.MapId == map.Id && play.SceneEventId == evt.Id, "Selecting the readable scene writes both reference IDs.");
            Save(host, "cinematic-command");
            All(control).OfType<TextBox>().Single().Text = "no result";
            Check(All(control).OfType<Button>().Single(b => b.Text == CinematicText.T("Accept")).Enabled == false, "Cannot apply an empty search result.");
            Check(play.SceneEventId == evt.Id, "Filtering must not overwrite the command.");
            host.Controls.Remove(control);
        }
        var waiting = new WaitForCinematicCommand();
        using (var control = new EventCommandCinematic(waiting, editor))
        {
            var list = All(control).OfType<ListBox>().Single();
            Check(list.SelectedIndex == 0 && waiting.SceneEventId == Guid.Empty, "All active scenes remains the explicit default.");
            var duration = All(control).OfType<NumericUpDown>().Single();
            Check(duration.Value == 60 && duration.Minimum == .1m && duration.Maximum == 600, "Wait uses seconds and server limits.");
            duration.Value = 1.234m;
            Check(waiting.TimeoutMs == 1234, "Milliseconds round-trip without losing precision.");
        }
        var chapter = new EventDescriptor(Guid.NewGuid(), true) { Name = "Chapitre un", Pages = [new()] };
        chapter.Pages[0].CinematicChapter = new();
        EventDescriptor.Lookup.Set(chapter.Id, chapter);
        try
        {
            var start = new StartCinematicChapterCommand();
            using var control = new EventCommandCinematic(start, editor);
            var list = All(control).OfType<ListBox>().Single();
            list.SelectedIndex = Enumerable.Range(0, list.Items.Count).Single(i => list.Items[i]!.ToString() == chapter.Name);
            Check(start.ChapterEventId == chapter.Id, "Chapter selection uses the actual common event identity.");
        }
        finally { EventDescriptor.Lookup.Delete(chapter); }
    }
    private static void TestDirector()
    {
        using var director = new FrmCinematicDirector { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-20000, -20000) };
        director.Show();
        ((System.Windows.Forms.Timer)typeof(FrmCinematicDirector).GetField("_timer", Hidden)!.GetValue(director)!).Stop();
        var scene = new CinematicScene { Name = "Scène de démonstration" };
        var actor = new CinematicActor { Name = "Le roi", StartX = 2, StartY = 3 };
        scene.Actors.Add(actor);
        var clip = new CinematicClip { Name = "Entrée du roi", Track = CinematicTrack.Movement, ActorId = actor.TargetId };
        scene.Clips.Add(clip);
        typeof(FrmCinematicDirector).GetMethod("LoadScene", Hidden)!.Invoke(director, [scene, Guid.Empty]);
        var properties = new CinematicProperties(clip, scene: scene).GetProperties()["ActorId"]!;
        Check(properties.Converter.ConvertToString(clip.ActorId) == actor.Name && properties.Converter.GetStandardValuesExclusive(), "Actor property offers names rather than editable GUIDs.");
        var camera = new CinematicClip { Track = CinematicTrack.Camera };
        Check(new CinematicProperties(camera).GetProperties()["FieldOfView"] == null && new CinematicProperties(camera).GetProperties()["ActorId"] == null,
            "Flat cameras hide perspective and inactive follow-target settings.");
        camera.Projection = CinematicProjection.Perspective2_5D; camera.FollowActor = true;
        Check(new CinematicProperties(camera).GetProperties()["FieldOfView"] != null && new CinematicProperties(camera).GetProperties()["ActorId"] != null,
            "Advanced camera settings remain available when their mode is selected.");
        var viewport = All(director).OfType<CinematicViewport>().Single();
        using var map = new Bitmap(1024, 768);
        using (var g = Graphics.FromImage(map))
        {
            g.Clear(Color.FromArgb(38, 63, 54));
            using var grid = new Pen(Color.FromArgb(65, 89, 75));
            for (var x = 0; x < map.Width; x += 32) g.DrawLine(grid, x, 0, x, map.Height);
            for (var y = 0; y < map.Height; y += 32) g.DrawLine(grid, 0, y, map.Width, y);
            g.DrawString("APERÇU DE TEST — CARTE SYNTHÉTIQUE", SystemFonts.DefaultFont, Brushes.White, 100, 100);
        }
        viewport.MapImage = map;
        using (var bitmap = new Bitmap(viewport.Width, viewport.Height)) viewport.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        var screen = (PointF)typeof(CinematicViewport).GetMethod("Screen", Hidden)!.Invoke(viewport, [2f, 3f])!;
        typeof(CinematicViewport).GetMethod("OnMouseDown", Hidden)!.Invoke(viewport, [new MouseEventArgs(MouseButtons.Left, 1, (int)screen.X, (int)screen.Y, 0)]);
        Check(ReferenceEquals(viewport.SelectedActor, actor), "Click selects the visible actor on the map.");
        Check(All(director).OfType<TreeView>().Single().SelectedNode?.Tag == actor, "Map and explorer selection agree.");
        var timeline = All(director).OfType<CinematicTimeline>().Single();
        var dropPoint = timeline.PointToScreen(new Point(320, 80));
        var drop = new DragEventArgs(CinematicDragData.Action(CinematicTrack.Camera), 0, dropPoint.X, dropPoint.Y, DragDropEffects.Copy, DragDropEffects.None);
        typeof(CinematicTimeline).GetMethod("OnDragEnter", Hidden)!.Invoke(timeline, [drop]);
        Check(drop.Effect == DragDropEffects.Copy, "Timeline accepts palette actions.");
        typeof(CinematicTimeline).GetMethod("OnDragDrop", Hidden)!.Invoke(timeline, [drop]);
        Check(scene.Clips.Last() is { Track: CinematicTrack.Camera, StartMs: 2000 }, "Dropping at two seconds authors a camera clip at that time.");
        var count = scene.Clips.Count;
        dropPoint = timeline.PointToScreen(new Point(20, 80));
        drop = new DragEventArgs(CinematicDragData.Action(CinematicTrack.Camera), 0, dropPoint.X, dropPoint.Y, DragDropEffects.Copy, DragDropEffects.None);
        typeof(CinematicTimeline).GetMethod("OnDragDrop", Hidden)!.Invoke(timeline, [drop]);
        Check(scene.Clips.Count == count && drop.Effect == DragDropEffects.None, "Timeline row labels reject drops.");
        dropPoint = viewport.PointToScreen(new Point((int)screen.X, (int)screen.Y));
        drop = new DragEventArgs(CinematicDragData.Action(CinematicTrack.Camera), 0, dropPoint.X, dropPoint.Y, DragDropEffects.Copy, DragDropEffects.None);
        typeof(CinematicViewport).GetMethod("OnDragDrop", Hidden)!.Invoke(viewport, [drop]);
        Check(scene.Clips.Last() is { Track: CinematicTrack.Camera, X: 2, Y: 3 }, "Map drops use tile coordinates.");
        viewport.CameraView = true;
        typeof(CinematicViewport).GetMethod("OnDragEnter", Hidden)!.Invoke(viewport, [drop]);
        Check(drop.Effect == DragDropEffects.None, "Camera preview does not accept authoring drops.");
        viewport.CameraView = false;
        var add = All(director).OfType<ToolStrip>().SelectMany(s => s.Items.OfType<ToolStripDropDownButton>()).Single(b => b.Text == CinematicText.T("AddAction"));
        Check(add.DropDownItems.OfType<ToolStripMenuItem>().Sum(m => m.DropDownItems.Count) == Enum.GetValues<CinematicTrack>().Length, "Every action remains available in grouped menus.");
        foreach (var size in new[] { new Size(1000,700), new Size(1440,960) })
        {
            director.Size = size; director.PerformLayout(); Application.DoEvents();
            // Deferred WinForms callbacks cannot render without a graphics device in this isolated fixture.
            if (viewport.MapImage == null)
            {
                viewport.MapImage = new Bitmap(1024, 768);
                using var g = Graphics.FromImage(viewport.MapImage);
                g.Clear(Color.FromArgb(38, 63, 54));
                g.DrawString("APERÇU DE TEST — CARTE SYNTHÉTIQUE", SystemFonts.DefaultFont, Brushes.White, 100, 100);
            }
            Check(viewport.Width >= 300, "Map remains usable at " + size);
            var toolbar = All(director).OfType<ToolStrip>().Single(t => t.Dock == DockStyle.Top);
            Check(toolbar.Visible && toolbar.Height > 15 && viewport.PointToScreen(Point.Empty).Y >= toolbar.PointToScreen(Point.Empty).Y + toolbar.Height,
                "Dock order preserves the main toolbar above the map.");
            Save(director, "cinematic-director-" + size.Width);
        }
        var npc = new NPCDescriptor(Guid.NewGuid()) { Name = "Garde de test", Sprite = "fixture.png" };
        NPCDescriptor.Lookup.Set(npc.Id, npc);
        try
        {
            // The real renderer is deliberately absent; the drop still authors and selects a real scene actor.
            viewport.MapImage ??= new Bitmap(1024, 768);
            using (var bitmap = new Bitmap(viewport.Width, viewport.Height)) viewport.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            dropPoint = viewport.PointToScreen(new Point(viewport.Width / 2, viewport.Height / 2));
            drop = new DragEventArgs(CinematicDragData.Npc(npc.Id), 0, dropPoint.X, dropPoint.Y, DragDropEffects.Copy, DragDropEffects.None);
            typeof(CinematicViewport).GetMethod("OnDragDrop", Hidden)!.Invoke(viewport, [drop]);
            Check(scene.Actors.Last().NPCId == npc.Id && viewport.SelectedActor == scene.Actors.Last(), "Dropping an NPC creates and selects a temporary actor.");
        }
        finally { NPCDescriptor.Lookup.Delete(npc); }
        viewport.MapImage?.Dispose(); viewport.MapImage = null;
    }
    private static void TestHub()
    {
        using var menu = new ToolStripMenuItem("Editors") { Name = "contentEditorsToolStripMenuItem" };
        foreach (var definition in HubRegistry.Definitions)
            menu.DropDownItems.Add(new ToolStripMenuItem(definition.Name) { Name = definition.Id });
        using var registry = new HubRegistry([menu], _ => { });
        using var view = new EditorHubView(registry, new HubPreferences(_ => "", (_, _) => { })) { Dock = DockStyle.Fill };
        using var host = new Form { Size = new(1280,720), ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-20000,-20000) };
        host.Controls.Add(view); host.Show(); view.SetCategory("Game"); Application.DoEvents();
        var canvas = All(view).OfType<ScrollableControl>().Single(c => c.GetType().Name == "Canvas");
        using (var bitmap = new Bitmap(canvas.Width, canvas.Height))
        {
            canvas.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            canvas.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        }
        foreach (var scroll in new[] { 230, 460, 0, 230 })
        { canvas.AutoScrollPosition = new Point(0, scroll); canvas.Invalidate(); Application.DoEvents(); }
        Check(canvas.AutoScrollPosition.Y < 0, "Hub regression fixture must really be scrolled.");
        Save(host, "editor-hub-scrolled");
    }
}
