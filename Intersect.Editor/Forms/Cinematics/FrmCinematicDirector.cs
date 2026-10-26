using System.Diagnostics;
using DarkUI.Forms;
using Intersect.Editor.Core;
using Intersect.Editor.Content;
using Intersect.Editor.Forms.Editors.Events;
using Intersect.Editor.General;
using Intersect.Editor.Maps;
using Intersect.Editor.Networking;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;
using Intersect.Framework.Core.GameObjects.NPCs;
using Newtonsoft.Json;
using Render = Intersect.Editor.Core.Graphics;
using DrawingColor = System.Drawing.Color;

namespace Intersect.Editor.Forms.Cinematics;

internal sealed partial class FrmCinematicDirector : DarkForm
{
    private CinematicScene _scene = new();
    private Guid _eventId;
    private readonly TreeView _explorer = new() { Dock = DockStyle.Fill, HideSelection = false };
    private readonly PropertyGrid _properties = new() { Dock = DockStyle.Fill, ToolbarVisible = false };
    private readonly ListBox _points = new() { Dock = DockStyle.Bottom, Height = 110 };
    private readonly CinematicViewport _viewport = new() { Dock = DockStyle.Fill };
    private readonly CinematicTimeline _timeline = new() { Dock = DockStyle.Fill };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 36, AutoEllipsis = true };
    private readonly Label _selectionTitle = new() { Dock = DockStyle.Top, Height = 38, AutoEllipsis = true };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 50 };
    private readonly Stack<string> _undo = new(), _redo = new();
    private string _lastState = "", _savedState = "";
    private long _lastTick, _lastRender;
    private bool _playing, _updating;
    private object? _selection;
    private readonly HashSet<Guid> _previewBarriers = [];
    private static string T(string key) => CinematicText.T(key);
    private static string Explain(string error)
    { var parts = error.Split(':', 2); return T(parts[0]) + (parts.Length == 2 ? ": " + parts[1] : ""); }
    private Action<Guid>? _joustSceneSaved;
    private string Snapshot() => JsonConvert.SerializeObject(_scene);
    private sealed record Resource(Guid Id, string Name, bool Voice = false)
    { public override string ToString() => Name; }

    internal FrmCinematicDirector() : this(null, null) { }
    internal FrmCinematicDirector(CinematicScene? initialScene, Action<Guid>? onSaved = null, Guid existingEventId = default)
    {
        _joustSceneSaved = onSaved;
        Text = T("Title"); Width = 1440; Height = 960; MinimumSize = new(1000, 700);
        StartPosition = FormStartPosition.CenterParent; KeyPreview = true; Icon = Program.Icon;
        var toolbar = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden, AutoSize = true };
        var files = new ToolStripDropDownButton(T("FileMenu"));
        var actors = new ToolStripDropDownButton(T("ActorsMenu"));
        var tools = new ToolStripDropDownButton(T("SceneToolsMenu"));
        var views = new ToolStripDropDownButton(T("ViewMenu"));
        var creators = new ToolStripDropDownButton(T("CreatorMenu"));
        void Button(string key, Action action)
        {
            var menu = key switch
            {
                "Open" or "Export" or "ChapterDirector" => files,
                "AutoCreator" or "DescriptionCreator" or "Templates" => creators,
                "TemporaryNpc" or "TemporaryVessel" or "Formation" or "FleetFormation" or "GroupPath" => actors,
                "ShotPreset" or "Marker" or "NavalScene" or "BattlefieldScene" or "GroupAnimation" or "GroupCharge" or "Reinforcements" or "GameplayTransition" or "Step" => tools,
                _ => null,
            };
            if (menu != null) menu.DropDownItems.Add(T(key), null, (_, _) => action());
            else toolbar.Items.Add(new ToolStripButton(T(key == "Save" ? "SaveShort" : key == "Play" ? "PreviewShort" : key), null, (_, _) => action()));
        }
        Button("New", () => { if (CanDiscard()) NewScene(); });
        Button("Open", Import); Button("Save", () => SaveScene()); Button("Export", Export);
        Button("Undo", () => Restore(_undo, _redo)); Button("Redo", () => Restore(_redo, _undo));
        Button("Play", () => { _playing = !_playing; _lastTick = Environment.TickCount64; });
        Button("Stop", () => { _playing = false; _previewBarriers.Clear(); SetCursor(0); });
        Button("Step", () => SetCursor(Math.Min(_scene.DurationMs, _timeline.CursorMs + 50)));
        Button("Test", TestInGame);
        Button("Templates", ChooseTemplate);
        Button("AutoCreator", () => OpenAutoCreator(false));
        Button("DescriptionCreator", () => OpenAutoCreator(true));
        Button("ChapterDirector", () => { using var chapter = new FrmCinematicChapters(); chapter.ShowDialog(this); });
        Button("ShotPreset", ChooseShotPreset);
        Button("Marker", () => { var marker = new CinematicMarker { Name = T("Marker"), TimeMs = _timeline.CursorMs };
            _scene.Markers.Add(marker); SelectObject(marker); CommitEdit(); });
        Button("TemporaryNpc", AddTemporaryActor);
        Button("Formation", AddFormation);
        Button("GroupPath", CopyGroupPath);
        Button("TemporaryVessel",AddVessel);Button("FleetFormation",AddFleetFormation);
        Button("NavalScene",()=>{ _scene.Mode=CinematicSceneMode.NavalScene;SelectObject(_scene);CommitEdit(); });
        Button("BattlefieldScene",()=>{ _scene.Mode=CinematicSceneMode.BattlefieldScene;SelectObject(_scene);CommitEdit(); });
        Button("GroupAnimation",GroupAnimation);Button("GroupCharge",GroupCharge);Button("Reinforcements",Reinforcements);
        Button("GameplayTransition",CreateGameplayTrigger);
        var middle = new SplitContainer { Name = "EditingWorkspace", Size = new(1400, 780), Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 460, Panel1MinSize = 250, Panel2MinSize = 150 };
        var left = new SplitContainer { Name = "MediaWorkspace", Size = new(1400, 460), Dock = DockStyle.Fill, SplitterDistance = 260, FixedPanel = FixedPanel.Panel1, Panel1MinSize = 210, Panel2MinSize = 600 };
        var right = new SplitContainer { Name = "PreviewWorkspace", Size = new(1140, 460), Dock = DockStyle.Fill, SplitterDistance = 820, FixedPanel = FixedPanel.Panel2, Panel1MinSize = 300, Panel2MinSize = 260 };
        left.Panel1.Controls.Add(_explorer); left.Panel1.Controls.Add(new Label { Text = T("Explorer"), Dock = DockStyle.Top, Height = 30 });
        var explorerActions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 110, AutoScroll = true };
        explorerActions.Controls.Add(MakeButton("Scene", () => SelectObject(_scene)));
        explorerActions.Controls.Add(MakeButton("Actor", AddActor));
        explorerActions.Controls.Add(MakeButton("Advanced", Advanced));
        left.Panel1.Controls.Add(explorerActions); left.Panel2.Controls.Add(right);
        InitializeDragPalette(left.Panel1);
        void TogglePanel(string key, Action<bool> toggle)
        {
            var check = new ToolStripMenuItem(T(key)) { Checked = true, CheckOnClick = true };
            check.CheckedChanged += (_, _) => toggle(check.Checked); views.DropDownItems.Add(check);
        }
        TogglePanel("ExplorerPanel", visible => left.Panel1Collapsed = !visible);
        TogglePanel("Inspector", visible => right.Panel2Collapsed = !visible);
        TogglePanel("TimelinePanel", visible => middle.Panel2Collapsed = !visible);
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.AddRange([creators, files, actors, tools, views]);
        middle.Panel1.Controls.Add(left);
        right.Panel1.Controls.Add(_viewport);
        var viewTools = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        var grid = new CheckBox { Text = T("Grid"), Checked = true, AutoSize = true };
        grid.CheckedChanged += (_, _) => { _viewport.ShowGrid = grid.Checked; _viewport.Invalidate(); };
        var camera = new CheckBox { Text = T("CameraView"), AutoSize = true };
        camera.CheckedChanged += (_, _) => { _viewport.CameraView = camera.Checked; RenderMap(); };
        viewTools.Controls.Add(MakeButton("SelectionMode", () => { SelectObject(_scene); RefreshTree(); _viewport.Invalidate(); _timeline.Invalidate(); }));
        viewTools.Controls.Add(grid); viewTools.Controls.Add(camera); viewTools.Controls.Add(MakeButton("Refresh", RenderMap));
        right.Panel1.Controls.Add(viewTools);
        right.Panel1.Controls.Add(new Label { Text = T("MapHint"), Dock = DockStyle.Bottom, Height = 40 });
        right.Panel2.Controls.Add(_properties); right.Panel2.Controls.Add(_points);
        right.Panel2.Controls.Add(_selectionTitle);
        var propertyActions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 75, AutoScroll = true };
        propertyActions.Controls.Add(MakeButton("Resources", ChooseResource)); propertyActions.Controls.Add(MakeButton("Duplicate", Duplicate));
        propertyActions.Controls.Add(MakeButton("ActorTarget", ChooseActorTarget));
        propertyActions.Controls.Add(MakeButton("EnvironmentSettings", () => {
            if (_timeline.Selected is { Track: CinematicTrack.Environment } clip) SelectObject(clip.EnvironmentSettings ??= new()); }));
        propertyActions.Controls.Add(MakeButton("Delete", Delete)); right.Panel2.Controls.Add(propertyActions);
        middle.Panel2.Controls.Add(_timeline);
        middle.Panel2.Controls.Add(new Label { Text = T("Timeline"), Dock = DockStyle.Top, Height = 26 });
        var commands = new ToolStrip { Dock = DockStyle.Bottom, GripStyle = ToolStripGripStyle.Hidden };
        var add = new ToolStripDropDownButton(T("AddAction"));
        var essential = new ToolStripMenuItem(T("EssentialActions"));
        var world = new ToolStripMenuItem(T("WorldActions"));
        var advanced = new ToolStripMenuItem(T("AdvancedActions"));
        foreach (var track in Enum.GetValues<CinematicTrack>())
        {
            var type = track;
            var category = track switch
            {
                CinematicTrack.Camera or CinematicTrack.Movement or CinematicTrack.Dialogue or CinematicTrack.Sound or CinematicTrack.Music => essential,
                CinematicTrack.Animation or CinematicTrack.Presentation or CinematicTrack.Actors or CinematicTrack.Environment or CinematicTrack.Choice => world,
                _ => advanced,
            };
            category.DropDownItems.Add(T(track.ToString()), null, (_, _) => AddClip(type));
        }
        add.DropDownItems.AddRange([essential, world, advanced]); commands.Items.Add(add);
        commands.Items.Add(new ToolStripButton(T("ImportAudio"), null, (_, _) => new FrmSoundImport().ShowDialog(this)));
        Controls.Add(middle); Controls.Add(commands); Controls.Add(_status); Controls.Add(toolbar);
        var guide = new Label { Text = T("QuickStart"), Dock = DockStyle.Top, Height = 36, Padding = new Padding(8, 6, 8, 0) };
        Controls.Add(guide); Controls.SetChildIndex(guide, 3); Controls.SetChildIndex(toolbar, 4);
        Intersect.Editor.Forms.Styling.EditorTheme.Apply(this);
        _explorer.AfterSelect += (_, e) =>
        {
            if (_updating) return;
            if (e.Node?.Tag is EventDescriptor evt && evt.Pages.FirstOrDefault()?.Cinematic is { } scene)
            { if (evt.Id != _eventId && CanDiscard()) LoadScene(Clone(scene), evt.Id); }
            else if (e.Node?.Tag is { } item)
            {
                if (item is CinematicClip clip) { _timeline.Selected = _viewport.Selected = clip; RefreshPoints(); }
                SelectObject(item); _timeline.Invalidate(); _viewport.Invalidate();
            }
        };
        _timeline.SelectionChanged += clip =>
        {
            _viewport.Selected = clip;
            if (clip != null) { _timeline.CursorMs = _viewport.CursorMs = clip.StartMs; RenderMap(); }
            SelectObject(clip ?? (object)_scene); RefreshPoints(); _viewport.Invalidate();
        };
        _timeline.Edited += CommitEdit;
        _timeline.MarkerSelected += marker => { _timeline.Selected = _viewport.Selected = null; SelectObject(marker); };
        _timeline.Scrubbed += time => { _playing = false; SetCursor(time); };
        _viewport.Edited += () => { CommitEdit(); RefreshPoints(); RenderMap(); };
        _viewport.PreviewChanged += () => { if (Environment.TickCount64 - _lastRender >= 33) RenderMap(); };
        _viewport.PointSelected += point => { if (point != null) { SelectObject(point); _points.SelectedItem = point; } };
        _viewport.ActorSelected += actor => { SelectObject(actor); RefreshTree(); _timeline.Invalidate(); };
        _properties.PropertyValueChanged += (_, _) => { CommitEdit(); RenderMap(); };
        _points.SelectedIndexChanged += (_, _) => { if (_points.SelectedItem is CinematicPoint p) SelectObject(p); };
        _points.KeyDown += (_, e) =>
        {
            if (_timeline.Selected is not { } clip || _points.SelectedIndex < 0) return;
            var i = _points.SelectedIndex;
            if (e.KeyCode == Keys.Delete) { clip.Points.RemoveAt(i); CommitEdit(); RefreshPoints(); }
            else if (e.Alt && e.KeyCode is Keys.Up or Keys.Down)
            {
                var next = Math.Clamp(i + (e.KeyCode == Keys.Up ? -1 : 1), 0, clip.Points.Count - 1);
                (clip.Points[i], clip.Points[next]) = (clip.Points[next], clip.Points[i]); CommitEdit(); RefreshPoints(); _points.SelectedIndex = next;
            }
            _viewport.Invalidate();
        };
        _timer.Tick += (_, _) => TickPreview(); _timer.Start();
        Shown += (_, _) => { if (initialScene == null) NewScene(); else LoadScene(initialScene, existingEventId); RenderMap();RequestVesselModels(false); };
        FormClosing += (_, e) => { if (!CanDiscard()) e.Cancel = true; };
        Disposed += (_, _) => { _timer.Dispose(); _viewport.MapImage?.Dispose(); };
    }

    private static Button MakeButton(string key, Action action)
    { var button = new Button { Text = T(key), AutoSize = true, Height = 30, FlatStyle = FlatStyle.Flat }; button.Click += (_, _) => action(); return button; }
    private static void ApplyColors(Control root)
    {
        root.BackColor = DrawingColor.FromArgb(22, 31, 40); root.ForeColor = DrawingColor.Gainsboro;
        foreach (Control child in root.Controls) ApplyColors(child);
    }
    private bool CanDiscard() => Snapshot() == _savedState || _savedState == "" && _scene.Clips.Count == 0 ||
        MessageBox.Show(this, T("Unsaved"), Text, MessageBoxButtons.OKCancel) == DialogResult.OK;
    private static CinematicScene Clone(CinematicScene scene) => JsonConvert.DeserializeObject<CinematicScene>(JsonConvert.SerializeObject(scene))!;
    private void NewScene()
    {
        if (Globals.CurrentMap is not { } map) { _status.Text = T("MapRequired"); return; }
        var scene = new CinematicScene { Name = T("New"), MapId = map.Id };
        scene.Actors.Add(new() { Name = T("Player"), StartX = Options.Instance.Map.MapWidth / 2, StartY = Options.Instance.Map.MapHeight / 2 });
        LoadScene(scene, Guid.Empty);
    }
    private void LoadScene(CinematicScene scene, Guid eventId)
    {
        _playing = false; _scene = scene; _eventId = eventId; _undo.Clear(); _redo.Clear(); _previewBarriers.Clear();
        _timeline.Scene = _viewport.Scene = scene; _timeline.Selected = _viewport.Selected = null;
        _lastState = _savedState = Snapshot(); SelectObject(scene); SetCursor(0); RefreshTree(); RenderMap();
    }
    private void SelectObject(object value)
    {
        _selection = value; _viewport.SelectedActor = value as CinematicActor ??
            (value is CinematicClip { Track: CinematicTrack.Actors } actorClip ? _scene.Actors.FirstOrDefault(a => a.TargetId == actorClip.ActorId) : null);
        if (value is CinematicActor or CinematicScene)
        { _timeline.Selected = _viewport.Selected = null; RefreshPoints(); }
        var route=value as CinematicClip ?? _timeline.Selected;
        var vessel=route?.Track == CinematicTrack.Movement && _scene.Actors.Any(a=>a.TargetId == route.ActorId && a.Kind == CinematicActorKind.TemporaryVessel);
        _properties.SelectedObject = new CinematicProperties(value,value is CinematicPoint && vessel,vessel,_scene);_properties.Refresh();
        _selectionTitle.Text = T("EditingSelection") + " " + (value switch
        {
            CinematicScene scene => scene.Name,
            CinematicActor actor => actor.Name,
            CinematicClip clip => clip.Name,
            CinematicMarker marker => marker.Name,
            _ => T("Inspector"),
        });
    }
    private void CommitEdit()
    {
        var state = Snapshot(); if (state != _lastState) { _undo.Push(_lastState); _redo.Clear(); _lastState = state; }
        _properties.Refresh(); _timeline.Invalidate(); _viewport.Invalidate(); RefreshTree();
        Text = T("Title") + " — " + _scene.Name + (state != _savedState ? " *" : "");
    }
    private void Restore(Stack<string> from, Stack<string> to)
    {
        if (from.Count == 0) return;
        to.Push(Snapshot()); _scene = JsonConvert.DeserializeObject<CinematicScene>(from.Pop())!; _lastState = Snapshot();
        _timeline.Scene = _viewport.Scene = _scene; _timeline.Selected = _viewport.Selected = null;
        SelectObject(_scene); RefreshTree(); RefreshPoints(); RenderMap(); _timeline.Invalidate();
    }
    private void RefreshTree()
    {
        _updating = true; _explorer.BeginUpdate(); _explorer.Nodes.Clear();
        var root = _explorer.Nodes.Add(string.IsNullOrWhiteSpace(_scene.Saga) ? T("Saga") : _scene.Saga);
        var chapter = root.Nodes.Add(string.IsNullOrWhiteSpace(_scene.Chapter) ? T("Chapter") : _scene.Chapter);
        var sequence = chapter.Nodes.Add(string.IsNullOrWhiteSpace(_scene.Sequence) ? T("Sequence") : _scene.Sequence);
        var current = sequence.Nodes.Add(_scene.Name); current.Tag = _scene;
        foreach (var group in _scene.Actors.GroupBy(a => a.GroupId))
        {
            var parent = group.Key == Guid.Empty ? current : current.Nodes.Add(group.First().GroupName);
            foreach (var actor in group) { var node = parent.Nodes.Add(actor.Name); node.Tag = actor; }
        }
        foreach (var clip in _scene.Clips.OrderBy(c => c.StartMs)) { var node = current.Nodes.Add($"{clip.StartMs / 1000f:0.##}s · {clip}"); node.Tag = clip; }
        foreach (var marker in _scene.Markers.OrderBy(m => m.TimeMs)) { var node = current.Nodes.Add($"{marker.TimeMs / 1000f:0.##}s · {marker.Name}"); node.Tag = marker; }
        if (Globals.CurrentMap is { } map)
            foreach (var evt in map.LocalEvents.Values.Where(e => e.Pages.FirstOrDefault()?.Cinematic != null && e.Id != _eventId))
            { var node = _explorer.Nodes.Add(evt.Name); node.Tag = evt; }
        _explorer.ExpandAll();
        TreeNode? FindSelection(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            { if (ReferenceEquals(node.Tag, _selection)) return node; if (FindSelection(node.Nodes) is { } found) return found; }
            return null;
        }
        _explorer.SelectedNode = FindSelection(_explorer.Nodes);
        _explorer.EndUpdate(); _updating = false;
    }
    private Resource? Pick(IEnumerable<Resource> resources)
    {
        using var form = new DarkForm { Text = T("Resources"), Width = 520, Height = 440, StartPosition = FormStartPosition.CenterParent };
        var list = new CinematicPicker<Resource> { Dock = DockStyle.Fill };
        var accept = new Button { Text = T("Accept"), AutoSize = true, Enabled = false, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = T("Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42 };
        actions.Controls.Add(accept); actions.Controls.Add(cancel);
        list.SelectionChanged += selected => accept.Enabled = selected != null;
        list.Accepted += () => { form.DialogResult = DialogResult.OK; form.Close(); };
        list.SetChoices(resources.OrderBy(r => r.Name));
        form.Controls.Add(list); form.Controls.Add(actions);
        form.AcceptButton = accept; form.CancelButton = cancel;
        Intersect.Editor.Forms.Styling.EditorTheme.Apply(form);
        return form.ShowDialog(this) == DialogResult.OK ? list.SelectedItem : null;
    }
    private void AddActor()
    {
        if (Globals.CurrentMap is not { } map || map.Id != _scene.MapId) return;
        var choices = map.LocalEvents.Values.Where(e => !e.Global && e.Pages.Count > 0 && e.Id != _eventId && _scene.Actors.All(a => a.TargetId != e.Id))
            .Select(e => new Resource(e.Id, e.Name)).ToList();
        if (_scene.Actors.All(a => a.TargetId != Guid.Empty)) choices.Insert(0, new(Guid.Empty, T("Player")));
        if (Pick(choices) is not { } picked) return;
        var evt = map.LocalEvents.GetValueOrDefault(picked.Id);
        var actor = new CinematicActor { Name = picked.Name, TargetId = picked.Id, StartX = evt?.SpawnX ?? 0, StartY = evt?.SpawnY ?? 0 };
        _scene.Actors.Add(actor); SelectObject(actor); CommitEdit();
    }
    private void ChooseTemplate()
    {
        if (Globals.CurrentMap is not { } map || !CanDiscard()) return;
        var kinds = Enum.GetValues<CinematicTemplateKind>();
        if (Pick(kinds.Select((kind, i) => new Resource(new Guid(i + 1, 0, 0, new byte[8]), T("Template" + kind)))) is not { } selected) return;
        var chosenKind = kinds.Single(k => T("Template" + k) == selected.Name);
        LoadScene(CinematicTemplates.Create(chosenKind, map.Id, Options.Instance.Map.MapWidth, Options.Instance.Map.MapHeight, T), Guid.Empty);
    }
    private void AddTemporaryActor()
    {
        if (_scene.Actors.Count >= 64) { _status.Text = T("scene-size"); return; }
        if (Pick(NPCDescriptor.Lookup.Values.OfType<NPCDescriptor>().Where(n => !string.IsNullOrWhiteSpace(n.Sprite))
                .Select(n => new Resource(n.Id, n.Name))) is not { } selected) return;
        var actor = new CinematicActor { Name = selected.Name, TargetId = Guid.NewGuid(), Kind = CinematicActorKind.TemporaryNpc,
            NPCId = selected.Id, StartX = Options.Instance.Map.MapWidth / 2, StartY = Options.Instance.Map.MapHeight / 2 };
        _scene.Actors.Add(actor); SelectObject(actor); CommitEdit(); RenderMap();
    }
    private sealed class FormationSettings
    {
        public CinematicFormationKind Formation { get; set; }
        public int Count { get; set; } = 8;
        public int Spacing { get; set; } = 1;
        public int StartX { get; set; }
        public int StartY { get; set; }
    }
    private void AddFormation()
    {
        if (Globals.CurrentMap is not { } map || map.Id != _scene.MapId) return;
        if (Pick(NPCDescriptor.Lookup.Values.OfType<NPCDescriptor>().Where(n => !string.IsNullOrWhiteSpace(n.Sprite))
                .Select(n => new Resource(n.Id, n.Name))) is not { } npc) return;
        var settings = new FormationSettings { StartX = Options.Instance.Map.MapWidth / 2, StartY = Options.Instance.Map.MapHeight / 2 };
        using var form = new DarkForm { Text = T("Formation"), Width = 520, Height = 430, StartPosition = FormStartPosition.CenterParent };
        form.Controls.Add(new PropertyGrid { Dock = DockStyle.Fill, SelectedObject = new CinematicProperties(settings), ToolbarVisible = false });
        form.Controls.Add(new Button { Text = T("Accept"), Dock = DockStyle.Bottom, Height = 36, DialogResult = DialogResult.OK });
        Intersect.Editor.Forms.Styling.EditorTheme.Apply(form);
        if (form.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var offsets = CinematicFormations.Offsets(settings.Formation, settings.Count, settings.Spacing);
            if (_scene.Actors.Count + offsets.Count > 64) throw new InvalidOperationException("scene-size");
            var group = Guid.NewGuid();
            var actors = offsets.Select((p, i) => new CinematicActor { Name = npc.Name + " " + (i + 1), TargetId = Guid.NewGuid(),
                Kind = CinematicActorKind.TemporaryNpc, NPCId = npc.Id, GroupId = group, GroupName = npc.Name,
                StartX = checked(settings.StartX + p.X), StartY = checked(settings.StartY + p.Y) }).ToList();
            if (actors.Any(a => a.StartX < 0 || a.StartY < 0 || a.StartX >= Options.Instance.Map.MapWidth || a.StartY >= Options.Instance.Map.MapHeight ||
                    map.Attributes[a.StartX, a.StartY] is MapBlockedAttribute)) throw new InvalidOperationException("formation-position");
            _scene.Actors.AddRange(actors); SelectObject(actors[0]); CommitEdit(); RenderMap();
        }
        catch (Exception error) when (error is InvalidOperationException or OverflowException)
        { MessageBox.Show(this, T(error is OverflowException ? "formation-position" : error.Message), Text); }
    }
    private void CopyGroupPath()
    {
        if (_timeline.Selected is not { } source) return;
        try
        {
            _scene.Clips.AddRange(CinematicFormations.CopyGroupPath(_scene, source, Options.Instance.Map.MapWidth, Options.Instance.Map.MapHeight));
            CommitEdit(); RenderMap();
        }
        catch (InvalidOperationException error) { MessageBox.Show(this, T(error.Message), Text); }
    }
    private void ChooseShotPreset()
    {
        if (_timeline.Selected is not { Track: CinematicTrack.Camera } clip) return;
        var kinds = Enum.GetValues<CinematicShotPreset>();
        if (Pick(kinds.Select((kind, i) => new Resource(new Guid(i + 1, 0, 0, new byte[8]), T(kind.ToString())))) is not { } selected) return;
        CinematicTemplates.ApplyShot(clip, kinds.Single(k => T(k.ToString()) == selected.Name));
        CommitEdit(); RenderMap();
    }
    private void AddClip(CinematicTrack track, int? startMs = null, CinematicActor? dropActor = null, CinematicPoint? position = null, CinematicLibraryItem? resource = null)
    {
        var selectedActor = dropActor ?? _selection as CinematicActor ??
            (_selection is CinematicClip selectedClip ? _scene.Actors.FirstOrDefault(a => a.TargetId == selectedClip.ActorId) : null) ?? _scene.Actors.FirstOrDefault();
        if (track == CinematicTrack.Naval && selectedActor?.Kind != CinematicActorKind.TemporaryVessel)
            selectedActor=_scene.Actors.FirstOrDefault(a=>a.Kind == CinematicActorKind.TemporaryVessel);
        if (track == CinematicTrack.Naval && selectedActor == null) { _status.Text=T("vessel-required");return; }
        var clip = new CinematicClip { Name = T(track.ToString()), Track = track, StartMs = Math.Clamp(startMs ?? _timeline.CursorMs, 0, Math.Max(0, _scene.DurationMs - 1000)),
            X = position?.X ?? Options.Instance.Map.MapWidth / 2, Y = position?.Y ?? Options.Instance.Map.MapHeight / 2, ActorId = selectedActor?.TargetId ?? Guid.Empty };
        if (resource != null)
        { clip.Name = resource.Name; clip.ResourceId = resource.ResourceId; clip.AudioFile = resource.AudioFile; clip.VoiceSound = resource.Voice; }
        if (track == CinematicTrack.Movement && selectedActor != null)
        {
            var previous = _scene.Clips.Where(c => c.Track == track && c.ActorId == clip.ActorId && c.StartMs <= clip.StartMs && c.Points.Count > 0).OrderBy(c => c.StartMs).LastOrDefault();
            clip.Points.Add(new() { X = previous?.Points[^1].X ?? selectedActor.StartX, Y = previous?.Points[^1].Y ?? selectedActor.StartY });
            if (position != null) clip.Points.Add(position);
        }
        _scene.Clips.Add(clip); _timeline.Selected = _viewport.Selected = clip; SelectObject(clip); CommitEdit(); RefreshPoints();
        _timeline.Reveal(clip);
        if (resource == null && track is (CinematicTrack.Animation or CinematicTrack.Sound or CinematicTrack.Music or CinematicTrack.Event or CinematicTrack.Gameplay or CinematicTrack.Condition or CinematicTrack.Quest or CinematicTrack.Projectile)) ChooseResource();
    }
    private void ChooseResource()
    {
        if (_selection is CinematicActor actor)
        {
            if (actor.Kind == CinematicActorKind.TemporaryVessel) { ChooseVesselModel(actor);return; }
            if (actor.Kind == CinematicActorKind.TemporaryNpc && Pick(NPCDescriptor.Lookup.Values.OfType<NPCDescriptor>()
                    .Where(n => !string.IsNullOrWhiteSpace(n.Sprite)).Select(n => new Resource(n.Id, n.Name))) is { } selected)
            { actor.NPCId = selected.Id; CommitEdit(); RenderMap(); }
            SelectObject(actor); return;
        }
        if (_timeline.Selected is not { } clip) return;
        if (clip.Track == CinematicTrack.Projectile) { ChooseProjectileResource(clip);return; }
        if (clip.Track == CinematicTrack.Environment)
        {
            if (Pick(GameContentManager.GetSmartSortedTextureNames(GameContentManager.TextureType.Fog).Select(f=>new Resource(Guid.Empty,f))) is { } fog)
            { (clip.EnvironmentSettings ??= new()).FogFile=fog.Name; CommitEdit(); SelectObject(clip.EnvironmentSettings); RenderMap(); }
            return;
        }
        if (ChooseWorkflowResource(clip)) return;
        if (clip.Track == CinematicTrack.Movement || clip.Track == CinematicTrack.Camera && clip.FollowActor)
        {
            if (Pick(_scene.Actors.Select(a => new Resource(a.TargetId, a.Name))) is { } chosen)
            { clip.ActorId = chosen.Id; CommitEdit(); }
            return;
        }
        IEnumerable<Resource> choices = clip.Track switch
        {
            CinematicTrack.Animation => AnimationDescriptor.Lookup.Values.OfType<AnimationDescriptor>().Select(a => new Resource(a.Id, a.Name)),
            CinematicTrack.Naval => AnimationDescriptor.Lookup.Values.OfType<AnimationDescriptor>().Select(a => new Resource(a.Id,a.Name)),
            CinematicTrack.Event or CinematicTrack.Choice or CinematicTrack.Gameplay => EventDescriptor.Lookup.Values.OfType<EventDescriptor>().Where(e => e.CommonEvent).Select(e => new Resource(e.Id, e.Name)),
            CinematicTrack.Music => GameContentManager.SmartSortedMusicNames.Select(n => new Resource(Guid.Empty, n)),
            CinematicTrack.Sound => GameContentManager.SmartSortedSoundNames.Select(n => new Resource(Guid.Empty, n))
                .Concat(GameContentManager.SmartSortedVoiceSoundNames.Select(n => new Resource(Guid.Empty, n, true))),
            _ => [],
        };
        if (Pick(choices) is not { } picked) return;
        if (clip.Track == CinematicTrack.Choice)
        {
            var fields = new[] { "ChoiceEvent1", "ChoiceEvent2", "ChoiceEvent3", "ChoiceEvent4" };
            if (Pick(fields.Select((f, i) => new Resource(new Guid(i + 1, 0, 0, new byte[8]), T(f)))) is { } branch)
            {
                var field = fields.First(f => T(f) == branch.Name);
                typeof(CinematicClip).GetProperty(field)!.SetValue(clip, picked.Id);
            }
        }
        else if (clip.Track is CinematicTrack.Sound or CinematicTrack.Music) { clip.AudioFile = picked.Name; clip.VoiceSound = picked.Voice; }
        else clip.ResourceId = picked.Id;
        CommitEdit();
    }
    private void ChooseActorTarget()
    {
        if (_timeline.Selected is not { } clip || clip.Track is not (CinematicTrack.Camera or CinematicTrack.Movement or CinematicTrack.Animation or CinematicTrack.Actors or CinematicTrack.Naval or CinematicTrack.Projectile)) return;
        if (Pick(_scene.Actors.Where(a => (clip.Track != CinematicTrack.Actors || a.Kind != CinematicActorKind.PersonalEvent) &&
                (clip.Track != CinematicTrack.Naval || a.Kind == CinematicActorKind.TemporaryVessel))
                .Select(a => new Resource(a.TargetId, a.Name))) is not { } selected) return;
        clip.ActorId = selected.Id; CommitEdit(); RenderMap();
    }
    private void Duplicate()
    {
        if (_selection is CinematicMarker marker)
        { var markerCopy = new CinematicMarker { Name = marker.Name, TimeMs = Math.Min(_scene.DurationMs, marker.TimeMs + 1000) };
            _scene.Markers.Add(markerCopy); SelectObject(markerCopy); CommitEdit(); return; }
        if (_timeline.Selected is not { } clip) return;
        var copy = JsonConvert.DeserializeObject<CinematicClip>(JsonConvert.SerializeObject(clip))!; copy.Id = Guid.NewGuid();
        copy.StartMs = Math.Min(clip.StartMs + clip.DurationMs, Math.Max(0, _scene.DurationMs - clip.DurationMs));
        _scene.Clips.Add(copy); _timeline.Selected = _viewport.Selected = copy; SelectObject(copy); CommitEdit(); RefreshPoints();
    }
    private void Delete()
    {
        if (_selection is CinematicMarker marker) _scene.Markers.Remove(marker);
        else if (_selection is CinematicActor actor)
        { _scene.Actors.Remove(actor); _scene.Clips.RemoveAll(c => c.Track == CinematicTrack.Movement && c.ActorId == actor.TargetId); }
        else if (_timeline.Selected is { } clip) _scene.Clips.Remove(clip);
        _timeline.Selected = _viewport.Selected = null; SelectObject(_scene); CommitEdit(); RefreshPoints();
    }
    private void RefreshPoints()
    {
        _points.Visible = _timeline.Selected is { Track: CinematicTrack.Movement };
        _points.Items.Clear(); if (_timeline.Selected is { Track: CinematicTrack.Movement } clip)
            foreach (var point in clip.Points) _points.Items.Add(point);
    }
    private bool SaveScene()
    {
        if (Globals.CurrentMap is not { } map || map.Id != _scene.MapId) { _status.Text = T("MapRequired"); return false; }
        try
        {
            ValidateVesselPaths();
            var page = CinematicCompiler.Compile(_scene, Options.Instance.Map.MapWidth, Options.Instance.Map.MapHeight,
                (x, y) => map.Attributes[x, y] is MapBlockedAttribute,
                id => map.LocalEvents.TryGetValue(id, out var actor) && !actor.Global && actor.Pages.Count > 0,
                id => AnimationDescriptor.Get(id) != null, id => EventDescriptor.Get(id)?.CommonEvent == true,
                (file, music) => music ? GameContentManager.MusicNames.Contains(file) :
                    GameContentManager.SoundNames.Contains(file) || GameContentManager.SmartSortedVoiceSoundNames.Contains(file),
                id => NPCDescriptor.Get(id) is { Sprite.Length: > 0 },
                WorkflowBooleanAvailable,WorkflowQuestAvailable,WorkflowTaskAvailable,
                file=>GameContentManager.GetSmartSortedTextureNames(GameContentManager.TextureType.Fog).Contains(file),
                key=>_vesselModelsReady && VesselModels.ContainsKey(key));
            if (_eventId == Guid.Empty) _eventId = Guid.NewGuid();
            var evt = map.LocalEvents.GetValueOrDefault(_eventId) ?? new EventDescriptor(_eventId, map.Id, 0, 0);
            if (evt.Pages.Count > 0)
            {
                // Preserve trigger, graphic, conditions and other native page metadata edited in the Event Editor.
                var metadata = evt.Pages[0]; metadata.Cinematic = page.Cinematic; metadata.CommandLists = page.CommandLists; page = metadata;
            }
            evt.Name = _scene.Name; evt.Global = false; evt.Pages = [page]; map.LocalEvents[_eventId] = evt;
            PacketSender.SendMap(map); _joustSceneSaved?.Invoke(_eventId); _savedState = _lastState = Snapshot(); _status.Text = T("Saved"); CommitEdit(); return true;
        }
        catch (InvalidOperationException error) { MessageBox.Show(this, T("Validation") + ": " + Explain(error.Message), Text); return false; }
    }
    private void Advanced()
    {
        if (!SaveScene() || Globals.CurrentMap is not { } map) return;
        using var editor = new FrmEvent(map) { MyEvent = map.LocalEvents[_eventId], MyMap = map };
        editor.InitEditor(false, false, false); editor.ShowDialog(this);
    }
    private void Import()
    {
        if (!CanDiscard()) return;
        using var dialog = new OpenFileDialog { Filter = "Cinematic JSON|*.cinematic.json|JSON|*.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > 2 * 1024 * 1024) throw new InvalidOperationException("scene-size");
            var scene = JsonConvert.DeserializeObject<CinematicScene>(File.ReadAllText(dialog.FileName), new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None, MaxDepth = 32 });
            if (scene == null || scene.Clips == null || scene.Actors == null || scene.Markers == null || scene.Markers.Count > 128 || scene.Markers.Any(m => m == null) || scene.Clips.Count > 512 || scene.Actors.Count > 64 || scene.Clips.Any(c => c == null || c.Points == null) || scene.Actors.Any(a => a == null))
                throw new InvalidOperationException("scene-size");
            LoadScene(scene, Guid.Empty);
        }
        catch (Exception error) when (error is IOException or JsonException or InvalidOperationException)
        { MessageBox.Show(this, T("Validation") + ": " + Explain(error.Message), Text); }
    }
    private void Export()
    {
        using var dialog = new SaveFileDialog { Filter = "Cinematic JSON|*.cinematic.json", FileName = "scene.cinematic.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { File.WriteAllText(dialog.FileName, JsonConvert.SerializeObject(_scene, Formatting.Indented)); }
        catch (IOException error) { MessageBox.Show(this, error.Message, Text); }
    }
    private void TestInGame()
    {
        if (!SaveScene()) return;
        _status.Text = T("TestHint");
        var path = Intersect.Editor.Core.Preferences.LoadPreference("ClientPath");
        if (File.Exists(path)) Process.Start(new ProcessStartInfo(path) { WorkingDirectory = Path.GetDirectoryName(path)! });
    }
    private void SetCursor(int time)
    {
        _timeline.CursorMs = _viewport.CursorMs = Math.Clamp(time, 0, Math.Max(0, _scene.DurationMs));
        _timeline.Invalidate(); _viewport.Invalidate(); RenderMap();
    }
    private void TickPreview()
    {
        if (Globals.CurrentMap?.Id != _scene.MapId)
        {
            _playing = false; _viewport.MapImage?.Dispose(); _viewport.MapImage = null;
            _viewport.Invalidate(); _status.Text = T("MapRequired"); return;
        }
        if (!_playing) return;
        var now = Environment.TickCount64; var next = Math.Min(_scene.DurationMs, _timeline.CursorMs + (int)Math.Clamp(now - _lastTick, 0, 250)); _lastTick = now;
        var barrier = _scene.Clips.Where(c => c.Track is CinematicTrack.Dialogue or CinematicTrack.Choice or CinematicTrack.Event or CinematicTrack.Gameplay or CinematicTrack.Condition or CinematicTrack.Quest &&
            c.StartMs >= _timeline.CursorMs && c.StartMs <= next && !_previewBarriers.Contains(c.Id)).OrderBy(c => c.StartMs).FirstOrDefault();
        if (barrier != null) { next = barrier.StartMs; _previewBarriers.Add(barrier.Id); _playing = false; SelectObject(barrier); }
        _timeline.CursorMs = _viewport.CursorMs = next; _timeline.Invalidate(); _viewport.Invalidate();
        if (now - _lastRender >= 200) RenderMap();
        if (next >= _scene.DurationMs) _playing = false;
        _status.Text = $"{next / 1000f:0.00}s / {_scene.DurationMs / 1000f:0.00}s · " + T("PreviewStatus");
    }
    private void RenderMap()
    {
        if (Globals.CurrentMap?.Id != _scene.MapId || Render.GetGraphicsDevice() == null)
        { _viewport.MapImage?.Dispose(); _viewport.MapImage = null; _viewport.Invalidate(); _status.Text = T("MapRequired"); return; }
        var image = Render.RenderCinematicMap(_scene, _timeline.CursorMs, _viewport.CameraView, out var perspective);
        _viewport.PerspectiveRendered = perspective;
        if (_viewport.CameraView && !perspective && CinematicPreview.Lens(_scene,_timeline.CursorMs).Projection == CinematicProjection.Perspective2_5D)
            _status.Text = T("PerspectiveFallback");
        var previous = _viewport.MapImage; _viewport.MapImage = image; previous?.Dispose();
        _lastRender = Environment.TickCount64; _viewport.Invalidate();
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F5) { TestInGame(); return true; }
        if (keyData == (Keys.Control | Keys.S)) { SaveScene(); return true; }
        if (keyData == (Keys.Control | Keys.Z)) { Restore(_undo, _redo); return true; }
        if (keyData == (Keys.Control | Keys.Y)) { Restore(_redo, _undo); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
