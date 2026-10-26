using DarkUI.Forms;
using Intersect.Editor.General;
using Intersect.Editor.Maps;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.GameObjects.Events;

namespace Intersect.Editor.Forms.Cinematics;

// Scene type and camera projection are independent authoring choices. These helpers only
// prepare editable scenes; ship models, placement and gameplay keep their native checks.
internal static class CinematicAuthoring
{
    internal static CinematicScene Starter(Guid mapId, int width, int height, string name,
        CinematicSceneMode mode, CinematicProjection projection)
    {
        if (mapId == Guid.Empty || width <= 0 || height <= 0 || !Enum.IsDefined(mode) || !Enum.IsDefined(projection))
            throw new ArgumentException(nameof(mode));
        var scene = mode switch
        {
            CinematicSceneMode.NavalScene => CinematicTemplates.Create(CinematicTemplateKind.NavalBattle, mapId, width, height, CinematicText.T),
            CinematicSceneMode.BattlefieldScene => CinematicTemplates.Create(CinematicTemplateKind.Battlefield, mapId, width, height, CinematicText.T),
            _ => new CinematicScene
            {
                MapId = mapId,
                Actors = [new() { Name = CinematicText.T("Player"), StartX = width / 2, StartY = height / 2 }],
                Clips = [new() { Name = CinematicText.T("Camera"), Track = CinematicTrack.Camera, X = width / 2, Y = height / 2 }],
            },
        };
        scene.Name = name; scene.Mode = mode;
        InitializeProjection(scene, projection);
        return scene;
    }

    internal static void InitializeProjection(CinematicScene scene, CinematicProjection projection)
    {
        if (!Enum.IsDefined(projection)) throw new ArgumentException(nameof(projection));
        foreach (var clip in scene.Clips.Where(c => c.Track == CinematicTrack.Camera))
        {
            clip.Projection = projection;
            if (projection != CinematicProjection.Perspective2_5D) continue;
            // Match the existing camera presets without replacing their zoom or follow target.
            clip.Heading = 0; clip.FieldOfView = 65;
            clip.EyeHeight = clip.Zoom <= 1 ? 10 : 2.5f;
            clip.PitchDegrees = clip.Zoom <= 1 ? -45 : -15;
            clip.OffsetY = clip.Zoom <= 1 ? 8 : 4;
        }
    }
}

internal sealed partial class FrmCinematicDirector
{
    private sealed record SceneSetup(string Name, CinematicSceneMode Mode, CinematicProjection Projection);
    private sealed record ModeChoice(CinematicSceneMode Value)
    { public override string ToString() => T(Value.ToString()); }
    private sealed record ProjectionChoice(CinematicProjection Value)
    { public override string ToString() => T(Value.ToString()); }

    private ToolStrip CreateAuthoringToolbar(out ToolStripDropDownButton views)
    {
        var toolbar = new ToolStrip { Name = "AuthoringToolbar", Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden, AutoSize = true };
        static ToolStripDropDownButton Menu(string key) => new(T(key)) { Name = key };
        static ToolStripMenuItem Item(string key, Action action) => new(T(key), null, (_, _) => action()) { Name = key };
        static void Add(ToolStripDropDownItem parent, string key, Action action) => parent.DropDownItems.Add(Item(key, action));

        var create = Menu("CreateSceneMenu");
        Add(create, "CreateGeneralScene", () => CreateScene(CinematicSceneMode.GeneralScene));
        Add(create, "CreateBattlefieldScene", () => CreateScene(CinematicSceneMode.BattlefieldScene));
        Add(create, "CreateNavalScene", () => CreateScene(CinematicSceneMode.NavalScene));
        create.DropDownItems.Add(new ToolStripSeparator());
        Add(create, "AutoCreator", () => OpenAutoCreator(false));
        Add(create, "DescriptionCreator", () => OpenAutoCreator(true));
        Add(create, "Templates", ChooseTemplate);

        var actors = Menu("ActorsRoutesMenu");
        Add(actors, "TemporaryNpc", AddTemporaryActor); Add(actors, "Actor", AddActor); Add(actors, "Formation", AddFormation);
        actors.DropDownItems.Add(new ToolStripSeparator());
        Add(actors, "Movement", () => AddClip(CinematicTrack.Movement)); Add(actors, "GroupPath", CopyGroupPath);
        var fleet = Item("NavalTools", () => { });
        Add(fleet, "TemporaryVessel", AddVessel); Add(fleet, "FleetFormation", AddFleetFormation);
        Add(fleet, "Naval", () => AddClip(CinematicTrack.Naval)); actors.DropDownItems.Add(fleet);

        var direction = Menu("DirectionMenu");
        var cameras = Item("Camera", () => { });
        Add(cameras, "AddFlatCamera", () => AddCamera(CinematicProjection.Flat2D));
        Add(cameras, "AddPerspectiveCamera", () => AddCamera(CinematicProjection.Perspective2_5D));
        Add(cameras, "ShotPreset", ChooseShotPreset); direction.DropDownItems.Add(cameras);
        foreach (var track in new[] { CinematicTrack.Animation, CinematicTrack.Presentation, CinematicTrack.Dialogue,
                     CinematicTrack.Choice, CinematicTrack.Sound, CinematicTrack.Music, CinematicTrack.Environment })
        { var type = track; Add(direction, track.ToString(), () => AddClip(type)); }
        var battle = Item("BattlefieldScene", () => { });
        Add(battle, "GroupAnimation", GroupAnimation); Add(battle, "GroupCharge", GroupCharge); Add(battle, "Reinforcements", Reinforcements);
        Add(battle, "Projectile", () => AddClip(CinematicTrack.Projectile)); direction.DropDownItems.Add(battle);
        direction.DropDownItems.Add(new ToolStripSeparator());
        Add(direction, "Marker", () =>
        {
            var marker = new CinematicMarker { Name = T("Marker"), TimeMs = _timeline.CursorMs };
            _scene.Markers.Add(marker); SelectObject(marker); CommitEdit();
        });
        Add(direction, "GameplayTransition", CreateGameplayTrigger);

        var preview = Menu("PreviewMenu");
        Add(preview, "Play", () => { _playing = !_playing; _lastTick = Environment.TickCount64; });
        Add(preview, "Stop", () => { _playing = false; _previewBarriers.Clear(); SetCursor(0); });
        Add(preview, "Step", () => SetCursor(Math.Min(_scene.DurationMs, _timeline.CursorMs + 50)));
        preview.DropDownItems.Add(new ToolStripSeparator()); Add(preview, "Test", TestInGame);
        var edit = Menu("EditingMenu");
        Add(edit, "Undo", () => Restore(_undo, _redo)); Add(edit, "Redo", () => Restore(_redo, _undo));
        Add(edit, "Scene", () => SelectObject(_scene));
        var files = Menu("SceneFilesMenu");
        Add(files, "ImportSceneJson", Import); Add(files, "ExportSceneJson", Export);
        files.DropDownItems.Add(new ToolStripSeparator());
        Add(files, "ChapterDirector", () => { using var chapter = new FrmCinematicChapters(); chapter.ShowDialog(this); });
        views = Menu("ViewMenu");

        toolbar.Items.AddRange([
            create,
            new ToolStripButton(T("OpenSavedScene"), null, (_, _) => OpenSavedScene()) { Name = "OpenSavedScene", ToolTipText = T("OpenSavedSceneHint") },
            new ToolStripSeparator(), actors, direction, preview,
            new ToolStripButton(T("SaveShort"), null, (_, _) => SaveScene()) { Name = "SaveScene" },
            new ToolStripSeparator(), edit, files, views,
        ]);
        return toolbar;
    }

    private SceneSetup? ChooseSceneSetup(string initialName, CinematicSceneMode initialMode)
    {
        using var dialog = new DarkForm
        {
            Name = "SceneSetup", Text = T("NewSceneSetup"), Width = 560, Height = 360,
            MinimumSize = new(520, 340), StartPosition = FormStartPosition.CenterParent,
        };
        var fields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(12) };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38)); fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        var name = new TextBox { Name = "SceneName", Text = initialName, MaxLength = 120, Dock = DockStyle.Fill };
        var mode = new ComboBox { Name = "SceneType", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        var projection = new ComboBox { Name = "SceneProjection", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        foreach (var kind in Enum.GetValues<CinematicSceneMode>()) mode.Items.Add(new ModeChoice(kind));
        foreach (var kind in Enum.GetValues<CinematicProjection>()) projection.Items.Add(new ProjectionChoice(kind));
        mode.SelectedIndex = (int)initialMode; projection.SelectedIndex = 0;
        var controls = new (string Key, Control Input)[] { ("Name", name), ("SceneType", mode), ("Projection", projection) };
        foreach (var (key, input) in controls)
        {
            var row = fields.RowCount++; fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            fields.Controls.Add(new Label { Text = T(key), AutoSize = true, Margin = new Padding(3, 8, 8, 8) }, 0, row);
            input.Margin = new Padding(3, 5, 3, 5); fields.Controls.Add(input, 1, row);
        }
        var hint = new Label { Name = "SceneSetupHint", Dock = DockStyle.Fill, Padding = new Padding(16, 6, 16, 6) };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(10), FlowDirection = FlowDirection.RightToLeft };
        var cancel = new Button { Text = T("Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel };
        var accept = new Button { Text = T("CreateScene"), AutoSize = true, DialogResult = DialogResult.OK };
        buttons.Controls.Add(cancel); buttons.Controls.Add(accept);
        dialog.Controls.Add(hint); dialog.Controls.Add(fields); dialog.Controls.Add(buttons);
        dialog.AcceptButton = accept; dialog.CancelButton = cancel;
        void Update()
        {
            var naval = (mode.SelectedItem as ModeChoice)?.Value == CinematicSceneMode.NavalScene;
            var validMap = !naval || IsVesselMap();
            accept.Enabled = validMap && !string.IsNullOrWhiteSpace(name.Text);
            hint.Text = T("SceneProjectionHint") + Environment.NewLine + Environment.NewLine +
                T(naval ? validMap ? "SceneNavalSetupHint" : "vessel-ocean-map" : "SceneLandSetupHint");
        }
        mode.SelectedIndexChanged += (_, _) => Update(); name.TextChanged += (_, _) => Update(); Update();
        Intersect.Editor.Forms.Styling.EditorTheme.Apply(dialog);
        return dialog.ShowDialog(this) == DialogResult.OK && mode.SelectedItem is ModeChoice chosenMode &&
            projection.SelectedItem is ProjectionChoice chosenProjection
            ? new(name.Text.Trim(), chosenMode.Value, chosenProjection.Value) : null;
    }

    private void CreateScene(CinematicSceneMode mode)
    {
        if (Globals.CurrentMap is not { } map) { _status.Text = T("MapRequired"); return; }
        if (ChooseSceneSetup(T(mode == CinematicSceneMode.GeneralScene ? "New" : mode.ToString()), mode) is not { } setup || !CanDiscard()) return;
        LoadGeneratedScene(CinematicAuthoring.Starter(map.Id, Options.Instance.Map.MapWidth, Options.Instance.Map.MapHeight,
            setup.Name, setup.Mode, setup.Projection));
        if (setup.Mode == CinematicSceneMode.NavalScene) _status.Text = T("SceneNavalSetupHint");
    }

    private void AddCamera(CinematicProjection projection)
    {
        AddClip(CinematicTrack.Camera, projection: projection);
        RenderMap();
    }

    internal static IEnumerable<EventDescriptor> SavedScenes(MapInstance map) => map.LocalEvents.Values
        .Where(evt => !evt.Global && !evt.CommonEvent && evt.MapId == map.Id && evt.Pages.Count == 1 &&
            evt.Pages[0].Cinematic is { } source && source.MapId == map.Id)
        .OrderBy(evt => evt.Name);

    private void OpenSavedScene()
    {
        if (Globals.CurrentMap is not { } map) { _status.Text = T("MapRequired"); return; }
        var scenes = SavedScenes(map).ToArray();
        if (scenes.Length == 0) { MessageBox.Show(this, T("NoSavedScenes"), T("OpenSavedScene")); return; }
        if (Pick(scenes.Select(evt => new Resource(evt.Id, evt.Name)), "OpenSavedScene") is { } selected) LoadSavedScene(selected.Id);
    }

    internal bool LoadSavedScene(Guid eventId)
    {
        if (Globals.CurrentMap is not { } map || SavedScenes(map).FirstOrDefault(evt => evt.Id == eventId) is not { } saved)
        { _status.Text = T("SceneSourceUnavailable"); return false; }
        if (!CanDiscard()) return false;
        LoadScene(Clone(saved.Pages[0].Cinematic!), saved.Id);
        return true;
    }

    internal EventDescriptor StoreScene(MapInstance map, EventPage page)
    {
        if (map.Id != _scene.MapId) throw new InvalidOperationException("MapRequired");
        if (_eventId != Guid.Empty && (!map.LocalEvents.TryGetValue(_eventId, out var existing) || existing.Global ||
                existing.CommonEvent || existing.MapId != map.Id || existing.Pages.Count != 1))
            throw new InvalidOperationException("SceneSourceUnavailable");
        if (_eventId == Guid.Empty) _eventId = Guid.NewGuid();
        var evt = map.LocalEvents.GetValueOrDefault(_eventId) ?? new EventDescriptor(_eventId, map.Id, 0, 0);
        if (evt.Pages.Count > 0)
        {
            // Keep the native trigger, graphic, conditions and position when editing an existing scene.
            var metadata = evt.Pages[0]; metadata.Cinematic = page.Cinematic; metadata.CommandLists = page.CommandLists; page = metadata;
        }
        evt.Name = _scene.Name; evt.Global = false; evt.Pages = [page]; map.LocalEvents[_eventId] = evt;
        return evt;
    }
}
