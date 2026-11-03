using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Intersect.Editor.Forms.Cinematics;
using Intersect.Editor.Forms.EditorHub;
using Intersect.Editor.Forms.Editors.Events;
using Intersect.Editor.Forms.Editors.Events.Event_Commands;
using Intersect.Editor.Maps;
using Intersect.Editor.General;
using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Maps.MapList;

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
        TestTextTranslation(); TestPicker(); TestCommands(); TestAuthoringChoices(); TestSceneSetupDialog(); TestSavedSceneEditing(); TestDirector(); TestMediaWorkflow(); TestHub();
        using var stream = typeof(CinematicText).Assembly.GetManifestResourceStream("Cinematics.Texts.json")!;
        var catalog = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(stream)!;
        foreach (var pair in catalog)
            foreach (var lang in new[] { "en", "fr", "es" })
                Check(pair.Value.TryGetValue(lang, out var text) && !string.IsNullOrWhiteSpace(text), "Missing locale: " + pair.Key);
        CultureInfo.CurrentUICulture = new("de-DE");
        Check(CinematicText.T("SearchChoices") == catalog["SearchChoices"]["en"], "English fallback");
        Console.WriteLine("PASS cinematic usability: scene reopening and stable event identity, independent scene type/projection, ordered authoring menus, scroll text, searchable choices, seconds, drag-and-drop, locales and responsive previews.");
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
        Check(new CinematicProperties(scene).GetProperties()["MapId"] == null,
            "Scene properties keep the source map association without exposing its technical GUID.");
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
            Check(timeline.Width >= director.ClientSize.Width - 24 && timeline.PointToScreen(Point.Empty).Y >= viewport.PointToScreen(Point.Empty).Y + viewport.Height,
                "Timeline spans the full workspace below the library, map and inspector.");
            var toolbar = All(director).OfType<ToolStrip>().Single(t => t.Dock == DockStyle.Top);
            Check(toolbar.Items.Cast<ToolStripItem>().Where(item => item is not ToolStripSeparator).Take(7).Select(item => item.Name)
                    .SequenceEqual(["CreateSceneMenu", "OpenSavedScene", "ActorsRoutesMenu", "DirectionMenu", "PreviewMenu", "SaveScene", "EditingMenu"]),
                "The primary workflow follows create/open, actors/routes, directing, preview and save.");
            var create = (ToolStripDropDownButton)toolbar.Items["CreateSceneMenu"]!;
            Check(create.DropDownItems.ContainsKey("CreateNavalScene") && create.DropDownItems.ContainsKey("CreateBattlefieldScene"),
                "Naval and land battle starters are directly discoverable in Create.");
            Check(((ToolStripDropDownButton)toolbar.Items["SceneFilesMenu"]!).DropDownItems.ContainsKey("ImportSceneJson"),
                "JSON import is explicitly separate from reopening a saved map scene.");
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
    private static void TestAuthoringChoices()
    {
        foreach (var mode in Enum.GetValues<CinematicSceneMode>())
        foreach (var projection in Enum.GetValues<CinematicProjection>())
        {
            var scene = CinematicAuthoring.Starter(Guid.NewGuid(), 20, 16, "Choix de création", mode, projection);
            Check(scene.Mode == mode && scene.Clips.Any(c => c.Track == CinematicTrack.Camera) &&
                    scene.Clips.Where(c => c.Track == CinematicTrack.Camera).All(c => c.Projection == projection),
                "Every scene type independently supports both 2D and 2.5D at creation.");
            Check(scene.Actors.All(a => a.Kind == CinematicActorKind.PersonalEvent) &&
                    scene.Clips.All(c => c.Track is CinematicTrack.Camera or CinematicTrack.Presentation),
                "Battle starters prepare shots without inventing fleet resources, NPCs or gameplay.");
            CinematicCompiler.Compile(scene, 20, 16);
        }
        var context = new CinematicCreationContext { MapId = Guid.NewGuid(), Width = 20, Height = 16 };
        using var creator = new FrmCinematicAutoCreator(context);
        var projectionPicker = All(creator).OfType<ComboBox>().Single(c => c.Name == "CreatorProjection");
        projectionPicker.SelectedIndex = (int)CinematicProjection.Perspective2_5D;
        Check(creator.GeneratePreview() && creator.Result!.Clips.Where(c => c.Track == CinematicTrack.Camera)
                .All(c => c.Projection == CinematicProjection.Perspective2_5D),
            "The guided creator exposes and applies the selected 2.5D projection.");
        foreach (var prompt in new[] { "Une scène en 2,5D.", "A 2.5D scene.", "Una escena en 2,5D." })
            Check(CinematicAutoCreator.Describe(prompt, context).Plan.Projection == CinematicProjection.Perspective2_5D,
                "Description input recognizes the requested projection independently of the scenario language.");
    }
    private static void TestSceneSetupDialog()
    {
        var previousMap = Globals.CurrentMap; var previousList = MapList.List;
        var ocean = new MapInstance(Guid.NewGuid()) { Name = "Ocean — fixture de création" };
        Globals.CurrentMap = ocean;
        MapList.List = new()
        {
            Items = [new MapListFolder
            {
                Name = "Ocean",
                Children = new() { Items = [new MapListMap { MapId = ocean.Id, Name = ocean.Name }] },
            }],
        };
        try
        {
            using var director = new FrmCinematicDirector { ShowInTaskbar = false };
            ((System.Windows.Forms.Timer)typeof(FrmCinematicDirector).GetField("_timer", Hidden)!.GetValue(director)!).Stop();
            var snapshot = typeof(FrmCinematicDirector).GetMethod("Snapshot", Hidden)!;
            var original = snapshot.Invoke(director, null);
            Exception? failure = null; var inspected = false;
            using var inspect = new System.Windows.Forms.Timer { Interval = 200 };
            inspect.Tick += (_, _) =>
            {
                inspect.Stop();
                var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(form => form.Name == "SceneSetup");
                try
                {
                    Check(dialog != null && dialog.Visible, "The real scene creation dialog is visible.");
                    var type = All(dialog!).OfType<ComboBox>().Single(c => c.Name == "SceneType");
                    var projection = All(dialog!).OfType<ComboBox>().Single(c => c.Name == "SceneProjection");
                    Check(type.Visible && projection.Visible && type.Items.Count == 3 && projection.Items.Count == 2,
                        "Creation visibly exposes all three scene types and both camera projections.");
                    Check(type.Items.Cast<object>().Select(item => item.ToString()).SequenceEqual(
                            Enum.GetValues<CinematicSceneMode>().Select(mode => CinematicText.T(mode.ToString()))) &&
                          projection.Items.Cast<object>().Select(item => item.ToString()).SequenceEqual(
                            Enum.GetValues<CinematicProjection>().Select(mode => CinematicText.T(mode.ToString()))),
                        "Creation choices use the localized scene and projection names.");
                    foreach (var mode in Enum.GetValues<CinematicSceneMode>())
                    {
                        type.SelectedIndex = (int)mode;
                        Check(type.Text == CinematicText.T(mode.ToString()), "Each scene type is selectable in the native dialog.");
                    }
                    type.SelectedIndex = (int)CinematicSceneMode.NavalScene;
                    foreach (var mode in Enum.GetValues<CinematicProjection>())
                    {
                        projection.SelectedIndex = (int)mode;
                        Check(projection.Text == CinematicText.T(mode.ToString()), "Both projections are selectable for a naval scene.");
                    }
                    var accept = All(dialog!).OfType<Button>().Single(button => button.Text == CinematicText.T("CreateScene"));
                    Check(accept.Enabled, "A map under the native Ocean folder permits a naval scene starter.");
                    dialog!.PerformLayout(); dialog.Refresh();
                    foreach (var input in new Control[] { type, projection, accept })
                        Check(input.Visible && dialog.ClientRectangle.Contains(dialog.RectangleToClient(input.RectangleToScreen(input.ClientRectangle))),
                            "Creation choices and the action remain inside the visible dialog.");
                    Save(dialog, "cinematic-scene-setup");
                    inspected = true;
                }
                catch (Exception error) { failure = error; }
                finally
                {
                    if (dialog != null) { dialog.DialogResult = DialogResult.Cancel; dialog.Close(); }
                }
            };
            inspect.Start();
            var result = typeof(FrmCinematicDirector).GetMethod("ChooseSceneSetup", Hidden)!
                .Invoke(director, ["Bataille navale — test d’interface", CinematicSceneMode.NavalScene]);
            inspect.Stop();
            if (failure != null) throw failure;
            Check(inspected && result == null && Equals(original, snapshot.Invoke(director, null)),
                "Cancelling the inspected dialog leaves the authoring scene unchanged.");
        }
        finally { Globals.CurrentMap = previousMap; MapList.List = previousList; }
    }
    private static void TestSavedSceneEditing()
    {
        var previous = Globals.CurrentMap;
        var map = new MapInstance(Guid.NewGuid()) { Name = "Carte de scènes" };
        var first = new EventDescriptor(Guid.NewGuid(), map.Id, 2, 3) { Name = "Même nom" };
        var second = new EventDescriptor(Guid.NewGuid(), map.Id, 5, 6) { Name = "Même nom" };
        first.Pages[0].Cinematic = CinematicAuthoring.Starter(map.Id, 20, 16, first.Name, CinematicSceneMode.GeneralScene, CinematicProjection.Flat2D);
        second.Pages[0].Cinematic = CinematicAuthoring.Starter(map.Id, 20, 16, second.Name, CinematicSceneMode.BattlefieldScene, CinematicProjection.Perspective2_5D);
        second.Pages[0].TriggerCommand = "keep-this-native-trigger"; second.Pages[0].Description = "Keep native page metadata.";
        var originalPage = second.Pages[0]; var originalConditions = originalPage.ConditionLists;
        map.LocalEvents.Add(first.Id, first); map.LocalEvents.Add(second.Id, second);
        try
        {
            Globals.CurrentMap = map;
            using var director = new FrmCinematicDirector();
            ((System.Windows.Forms.Timer)typeof(FrmCinematicDirector).GetField("_timer", Hidden)!.GetValue(director)!).Stop();
            Check(FrmCinematicDirector.SavedScenes(map).Count() == 2 && director.LoadSavedScene(second.Id),
                "Opening saved content preserves identity even when two scenes have the same name.");
            var edited = (CinematicScene)typeof(FrmCinematicDirector).GetField("_scene", Hidden)!.GetValue(director)!;
            Check((Guid)typeof(FrmCinematicDirector).GetField("_eventId", Hidden)!.GetValue(director)! == second.Id &&
                    !ReferenceEquals(edited, second.Pages[0].Cinematic),
                "The editor retains the Event ID and edits a detached source copy.");
            edited.Name = "Scène renommée"; edited.Clips[0].Zoom = 2;
            Check(second.Pages[0].Cinematic!.Name == "Même nom" && second.Pages[0].Cinematic!.Clips[0].Zoom != 2,
                "Changes do not mutate saved content before saving.");
            var stored = director.StoreScene(map, CinematicCompiler.Compile(edited, 20, 16));
            Check(stored.Id == second.Id && ReferenceEquals(stored, second) && map.LocalEvents.Count == 2 &&
                    stored.Name == "Scène renommée" && first.Name == "Même nom",
                "Saving updates the selected Event instead of creating a duplicate or replacing a same-name scene.");
            Check(ReferenceEquals(stored.Pages[0], originalPage) && ReferenceEquals(stored.Pages[0].ConditionLists, originalConditions) &&
                    stored.Pages[0].TriggerCommand == "keep-this-native-trigger" && stored.Pages[0].Description == "Keep native page metadata." &&
                    stored.SpawnX == 5 && stored.SpawnY == 6,
                "Editing preserves native trigger, conditions, page metadata and Event placement.");
            var eventId = stored.Id; map.LocalEvents.Remove(eventId);
            try { director.StoreScene(map, CinematicCompiler.Compile(edited, 20, 16)); throw new Exception("Missing saved Event was silently recreated."); }
            catch (InvalidOperationException error) { Check(error.Message == "SceneSourceUnavailable", "Deleted sources fail clearly."); }
            Check(!director.LoadSavedScene(Guid.NewGuid()), "An unavailable reference cannot silently open a fresh scene.");
        }
        finally { Globals.CurrentMap = previous; }
    }
    private static void TestMediaWorkflow()
    {
        var originalDirectory = Environment.CurrentDirectory;
        var folder = Path.GetFullPath(Path.Combine(originalDirectory, "..", "media-fixture"));
        Directory.CreateDirectory(Path.Combine(folder, "resources", "entities"));
        Directory.CreateDirectory(Path.Combine(folder, "resources", "animations"));
        using (var sprite = new Bitmap(96, 128))
        using (var g = Graphics.FromImage(sprite))
        {
            g.FillEllipse(Brushes.Gold, 9, 1, 14, 14); g.FillRectangle(Brushes.RoyalBlue, 7, 15, 18, 16);
            sprite.Save(Path.Combine(folder, "resources", "entities", "guard.png"));
        }
        using (var sprite = new Bitmap(64, 64))
        using (var g = Graphics.FromImage(sprite))
        { g.FillEllipse(Brushes.OrangeRed, 2, 2, 28, 28); sprite.Save(Path.Combine(folder, "resources", "animations", "fire.png")); }
        var npc = new NPCDescriptor(Guid.NewGuid()) { Name = "Garde royal", Sprite = "guard.png" };
        var animation = new AnimationDescriptor(Guid.NewGuid()) { Name = "Flamme" };
        animation.Lower.Sprite = "fire.png"; animation.Lower.XFrames = animation.Lower.YFrames = 2;
        NPCDescriptor.Lookup.Set(npc.Id, npc); AnimationDescriptor.Lookup.Set(animation.Id, animation);
        Environment.CurrentDirectory = folder;
        try
        {
            using var director = new FrmCinematicDirector { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-20000, -20000) };
            director.Show(); ((System.Windows.Forms.Timer)typeof(FrmCinematicDirector).GetField("_timer", Hidden)!.GetValue(director)!).Stop();
            var scene = new CinematicScene { Name = "Montage — démonstration" };
            scene.Actors.Add(new() { Name = "Joueur", StartX = 1, StartY = 1 });
            typeof(FrmCinematicDirector).GetMethod("LoadScene", Hidden)!.Invoke(director, [scene, Guid.Empty]);
            var timeline = All(director).OfType<CinematicTimeline>().Single();
            var viewport = All(director).OfType<CinematicViewport>().Single();
            var library = All(director).OfType<System.Windows.Forms.TabControl>().Single(t => t.Name == "MediaLibrary");
            Check(library.Dock == DockStyle.Fill && library.TabPages.Count == 5, "Full-height object library provides characters, effects, audio, actions and scene.");
            void Drop(IDataObject data, int x = 320)
            {
                timeline.AutoScrollPosition = Point.Empty;
                var point = timeline.PointToScreen(new Point(x, 80));
                typeof(CinematicTimeline).GetMethod("OnDragDrop", Hidden)!.Invoke(timeline,
                    [new DragEventArgs(data, 0, point.X, point.Y, DragDropEffects.Copy, DragDropEffects.None)]);
            }
            Drop(CinematicDragData.Npc(npc.Id));
            var guard = scene.Actors.Last();
            Check(scene.Clips.Single() is { Track: CinematicTrack.Actors, StartMs: 2000 } && viewport.SelectedActor == guard,
                "Dropping a character onto the timeline creates and selects its appearance clip.");
            Check(!CinematicPreview.ActorVisible(scene, guard, 1999) && CinematicPreview.ActorVisible(scene, guard, 2000),
                "The actor appears at the dropped time rather than at the start of the scene.");
            var undo = (Stack<string>)typeof(FrmCinematicDirector).GetField("_undo", Hidden)!.GetValue(director)!;
            Check(undo.Count == 1, "One object drop is one undo operation, including actor creation.");
            void FixtureMap()
            {
                viewport.MapImage?.Dispose(); viewport.MapImage = new Bitmap(1024, 768);
                using var g = Graphics.FromImage(viewport.MapImage);
                g.Clear(Color.FromArgb(38, 63, 54));
                g.DrawString("CARTE SYNTHÉTIQUE — TEST D’INTERFACE", SystemFonts.DefaultFont, Brushes.White, 60, 60);
                using var bitmap = new Bitmap(viewport.Width, viewport.Height);
                viewport.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            }
            void Place(int x, int y)
            {
                FixtureMap();
                var point = (PointF)typeof(CinematicViewport).GetMethod("Screen", Hidden)!.Invoke(viewport, [(float)x, (float)y])!;
                var mouse = new MouseEventArgs(MouseButtons.Left, 1, (int)point.X, (int)point.Y, 0);
                typeof(CinematicViewport).GetMethod("OnMouseDown", Hidden)!.Invoke(viewport, [mouse]);
                typeof(CinematicViewport).GetMethod("OnMouseMove", Hidden)!.Invoke(viewport, [mouse]);
                typeof(CinematicViewport).GetMethod("OnMouseUp", Hidden)!.Invoke(viewport, [mouse]);
            }
            Place(7, 6);
            Check(guard.StartX == 7 && guard.StartY == 6 && undo.Count == 2, "Select timeline object, position it on the map and undo the placement independently.");
            Drop(CinematicDragData.Resource(new(CinematicTrack.Animation, animation.Id, animation.Name)), 400);
            var effect = scene.Clips.Last();
            Check(effect is { Track: CinematicTrack.Animation, StartMs: 3000 } && effect.ResourceId == animation.Id,
                "Dragging a named effect creates a configured clip without another resource dialog.");
            Place(5, 4);
            Check(effect.X == 5 && effect.Y == 4, "The selected effect can be placed directly in the map preview.");
            Drop(CinematicDragData.Resource(new(CinematicTrack.Sound, Guid.Empty, "Voix", "voice.ogg", true)), 480);
            Check(scene.Clips.Last() is { Track: CinematicTrack.Sound, StartMs: 4000, AudioFile: "voice.ogg", VoiceSound: true },
                "Audio drag preserves the selected voice file and dropped time.");
            // Re-select the character from the timeline as in a video editor.
            timeline.Selected = scene.Clips.First(); viewport.Selected = timeline.Selected;
            typeof(FrmCinematicDirector).GetMethod("SelectObject", Hidden)!.Invoke(director, [timeline.Selected]);
            viewport.CursorMs = timeline.CursorMs = 2000;
            director.PerformLayout(); Application.DoEvents(); FixtureMap();
            Check(All(library).OfType<ListBox>().Any(l => l.DrawMode == DrawMode.OwnerDrawFixed && l.ItemHeight >= 60), "Library objects have visual thumbnail cards.");
            Save(director, "cinematic-montage-library");
            library.SelectedIndex = 1; Application.DoEvents(); FixtureMap(); Save(director, "cinematic-montage-effects");
            viewport.MapImage?.Dispose(); viewport.MapImage = null;
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;
            NPCDescriptor.Lookup.Delete(npc); AnimationDescriptor.Lookup.Delete(animation);
        }
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
