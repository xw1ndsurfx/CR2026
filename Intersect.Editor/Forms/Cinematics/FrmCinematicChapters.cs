using DarkUI.Forms;
using Intersect.Editor.Core;
using Intersect.Editor.Forms.Editors.Events;
using Intersect.Editor.General;
using Intersect.Editor.Networking;
using Intersect.Enums;
using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Variables;
using Intersect.GameObjects;
using Intersect.Network.Packets.Server;
using Newtonsoft.Json;

namespace Intersect.Editor.Forms.Cinematics;

internal sealed class FrmCinematicChapters : DarkForm
{
    private CinematicChapter _chapter = new();
    private Guid _eventId;
    private Guid _automaticCompletionId;
    private Guid _ownedCompletionId;
    private Guid _ownedNewEventId;
    private bool _newChapter;
    private readonly HashSet<(GameObjectType Type, Guid Id)> _requestedCreates = [];
    private string _saved = "";
    private readonly TreeView _library = new() { Dock = DockStyle.Fill, HideSelection = false };
    private readonly ListBox _scenes = new() { Dock = DockStyle.Fill };
    private readonly PropertyGrid _properties = new() { Dock = DockStyle.Fill, ToolbarVisible = false, HelpVisible = true };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 58, Padding = new(10, 6, 10, 6) };
    private readonly Label _guide = new() { Dock = DockStyle.Top, Height = 54, Padding = new(10, 8, 10, 4) };
    private readonly FlowLayoutPanel _tools = new() { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new(6) };
    private readonly CheckBox _advanced = new() { AutoSize = true, Margin = new(12, 9, 4, 4), Name = "ChapterAdvancedOptions" };
    private readonly System.Windows.Forms.Timer _saveTimer = new() { Interval = 250 };
    private CinematicChapterSaveSession? _saveSession;
    private long _saveStarted;
    private bool _openAdvancedWhenSaved;
    private bool _refreshingLibrary;
    private object? _selection;
    private static string T(string key) => CinematicText.T(key);
    private string Snapshot() => JsonConvert.SerializeObject(_chapter);
    private sealed record Choice(Guid Id, string Name) { public override string ToString() => Name; }

    internal FrmCinematicChapters(EventDescriptor? existing = null)
    {
        Text = T("ChapterDirector"); Width = 1100; Height = 760; MinimumSize = new(800, 550);
        StartPosition = FormStartPosition.CenterParent; Icon = Program.Icon;
        void Button(string key, Action action)
        { var button = new Button { Name = key, Text = T(key), AutoSize = true }; button.Click += (_, _) => action(); _tools.Controls.Add(button); }
        Button("NewChapter", () => { if (CanDiscard()) LoadChapter(null); }); Button("SaveChapter", () => Save());
        Button("ChapterProperties", () => Select(_chapter)); Button("AddChapterScene", AddScene);
        Button("Up", () => Move(-1)); Button("Down", () => Move(1)); Button("Delete", Delete);
        _advanced.Text = T("ChapterAdvancedOptions"); _tools.Controls.Add(_advanced);
        var native = new Button { Name = "ChapterNativeEditor", Text = T("ChapterNativeEditor"), AutoSize = true, Visible = false };
        native.Click += (_, _) => Save(openAdvanced: true); _tools.Controls.Add(native);
        _advanced.CheckedChanged += (_, _) => { native.Visible = _advanced.Checked; Select(_selection ?? _chapter); UpdateGuide(); };
        var left = new SplitContainer { Dock = DockStyle.Fill, Size = new(1050, 650), SplitterDistance = 230 };
        var right = new SplitContainer { Dock = DockStyle.Fill, Size = new(800, 650), SplitterDistance = 340 };
        left.Panel1.Controls.Add(_library); left.Panel2.Controls.Add(right);
        right.Panel1.Controls.Add(_scenes); right.Panel2.Controls.Add(_properties);
        Controls.Add(left); Controls.Add(_guide); Controls.Add(_status); Controls.Add(_tools);
        Intersect.Editor.Forms.Styling.EditorTheme.Apply(this);
        _scenes.SelectedIndexChanged += (_, _) => { if (_scenes.SelectedItem is CinematicChapterScene entry) Select(entry); };
        _properties.PropertyValueChanged += (_, _) => { RefreshScenes(); Select(_selection ?? _chapter); UpdateGuide(); };
        _library.AfterSelect += (_, e) => { if (!_refreshingLibrary && e.Node?.Tag is EventDescriptor evt && evt.Id != _eventId && CanDiscard()) LoadChapter(evt); };
        FormClosing += (_, e) =>
        {
            if (_saveSession?.Pending == true) { _status.Text = T("ChapterSavePending"); e.Cancel = true; }
            else if (!CanDiscard()) e.Cancel = true;
        };
        _saveTimer.Tick += (_, _) =>
        {
            if (!Networking.Network.Connected) _saveSession?.Fail("ChapterSaveDisconnected");
            else if (Environment.TickCount64 - _saveStarted >= 15000) _saveSession?.Fail("ChapterSaveTimeout");
        };
        PacketHandler.GameObjectReceived += ReceiveObject;
        Disposed += (_, _) => { PacketHandler.GameObjectReceived -= ReceiveObject; _saveTimer.Dispose(); };
        LoadChapter(existing);
    }

    private void Select(object value)
    { _selection = value; _properties.SelectedObject = new CinematicChapterProperties(value, _chapter, _eventId, _advanced.Checked); }
    private bool CanDiscard() => _saved == Snapshot() || MessageBox.Show(this, T("Unsaved"), Text, MessageBoxButtons.OKCancel) == DialogResult.OK;
    private void LoadChapter(EventDescriptor? evt)
    {
        _saveSession = null; _openAdvancedWhenSaved = false; _saveTimer.Stop(); _requestedCreates.Clear();
        _eventId = evt?.Id ?? Guid.Empty;
        _newChapter = evt == null; _ownedNewEventId = Guid.Empty;
        _chapter = evt?.Pages.FirstOrDefault()?.CinematicChapter is { } source
            ? JsonConvert.DeserializeObject<CinematicChapter>(JsonConvert.SerializeObject(source))!
            : new() { Name = T("NewChapter") };
        _automaticCompletionId = PlayerVariableDescriptor.Get(_chapter.CompletionVariableId)?.TextId == CompletionTextId(_eventId)
            ? _chapter.CompletionVariableId : Guid.Empty;
        _ownedCompletionId = _automaticCompletionId;
        _saved = Snapshot(); Select(_chapter); RefreshScenes(); RefreshLibrary(); UpdateGuide();
        _status.Text = T("ChapterGettingStarted");
    }
    private void UpdateGuide()
    {
        _guide.Text = _chapter.UnlockVariableId != Guid.Empty && _chapter.UnlockVariableId == _chapter.CompletionVariableId
            ? T("ChapterTrackingConflict") : _advanced.Checked ? T("ChapterAdvancedHint") : T("ChapterProgressHint");
    }
    private void RefreshScenes()
    {
        var selection = _selection;
        _scenes.Items.Clear(); foreach (var entry in _chapter.Scenes) _scenes.Items.Add(entry);
        if (selection is CinematicChapterScene scene) _scenes.SelectedItem = scene;
    }
    private void RefreshLibrary()
    {
        _refreshingLibrary = true; _library.BeginUpdate();
        try
        {
            _library.Nodes.Clear();
            foreach (var project in EventDescriptor.Lookup.Values.OfType<EventDescriptor>()
                         .Where(e => e.CommonEvent && e.Pages.FirstOrDefault()?.CinematicChapter != null)
                         .GroupBy(e => e.Pages[0].CinematicChapter!.Project).OrderBy(g => g.Key))
            {
                var root = _library.Nodes.Add(string.IsNullOrWhiteSpace(project.Key) ? T("Project") : project.Key);
                foreach (var saga in project.GroupBy(e => e.Pages[0].CinematicChapter!.Saga).OrderBy(g => g.Key))
                {
                    var node = root.Nodes.Add(string.IsNullOrWhiteSpace(saga.Key) ? T("Saga") : saga.Key);
                    foreach (var evt in saga.OrderBy(e => e.Name))
                    {
                        var leaf = new TreeNode(evt.Name) { Tag = evt }; node.Nodes.Add(leaf);
                        if (evt.Id == _eventId) _library.SelectedNode = leaf;
                    }
                }
            }
            _library.ExpandAll();
        }
        finally { _library.EndUpdate(); _refreshingLibrary = false; }
    }
    private Choice? Pick(IEnumerable<Choice> choices)
    {
        using var form = new DarkForm { Text = T("AddChapterScene"), Width = 560, Height = 450, StartPosition = FormStartPosition.CenterParent };
        var list = new ListBox { Dock = DockStyle.Fill }; foreach (var choice in choices.OrderBy(c => c.Name)) list.Items.Add(choice);
        var accept = new Button { Text = T("Accept"), Dock = DockStyle.Bottom, Height = 36, DialogResult = DialogResult.OK, Enabled = false };
        list.SelectedIndexChanged += (_, _) => accept.Enabled = list.SelectedItem != null;
        list.DoubleClick += (_, _) => { if (list.SelectedItem != null) { form.DialogResult = DialogResult.OK; form.Close(); } };
        form.Controls.Add(list); form.Controls.Add(accept); Intersect.Editor.Forms.Styling.EditorTheme.Apply(form);
        return form.ShowDialog(this) == DialogResult.OK ? list.SelectedItem as Choice : null;
    }
    private void AddScene()
    {
        var available = (Globals.CurrentMap?.LocalEvents.Values.AsEnumerable() ?? [])
            .Concat(EventDescriptor.Lookup.Values.OfType<EventDescriptor>())
            .Where(e => !e.Global && !e.CommonEvent && e.Pages.Count == 1 && e.Pages[0].Cinematic != null)
            .GroupBy(e => e.Id).Select(g => g.First()).ToArray();
        if (available.Length == 0) { _status.Text = T("ChapterNoSavedScenes"); return; }
        if (Pick(available.Select(e => new Choice(e.Id, e.Name))) is not { } selected) return;
        var entry = new CinematicChapterScene { Name = selected.Name, MapId = available.Single(e => e.Id == selected.Id).MapId, SceneEventId = selected.Id };
        _chapter.Scenes.Add(entry); Select(entry); RefreshScenes(); _status.Text = T("ChapterScenesReady");
    }
    private void Move(int delta)
    {
        if (_scenes.SelectedItem is not CinematicChapterScene entry) return;
        var i = _chapter.Scenes.IndexOf(entry); var target = i + delta;
        if (target < 0 || target >= _chapter.Scenes.Count) return;
        (_chapter.Scenes[i], _chapter.Scenes[target]) = (_chapter.Scenes[target], _chapter.Scenes[i]); RefreshScenes();
    }
    private void Delete()
    { if (_scenes.SelectedItem is CinematicChapterScene entry) { _chapter.Scenes.Remove(entry); Select(_chapter); RefreshScenes(); } }
    private static bool ValidScene(Guid mapId, Guid id)
    {
        var evt = Globals.CurrentMap?.Id == mapId ? Globals.CurrentMap.LocalEvents.GetValueOrDefault(id) : EventDescriptor.Get(id);
        return evt != null && evt.MapId == mapId && !evt.Global && !evt.CommonEvent && evt.Pages.Count == 1 && evt.Pages[0].Cinematic?.MapId == mapId;
    }
    private static string CompletionTextId(Guid eventId) => "cinematic_chapter_" + eventId.ToString("N") + "_complete";
    internal CinematicChapterSaveSession PrepareSaveSession(Action<GameObjectType, Guid> create, Action<Intersect.Models.IDatabaseObject> save)
    {
        var draft = JsonConvert.DeserializeObject<CinematicChapter>(Snapshot())!;
        if (string.IsNullOrWhiteSpace(draft.Name)) throw new InvalidOperationException("ChapterNameRequired");
        var eventId = _eventId == Guid.Empty ? Guid.NewGuid() : _eventId;
        var existingEvent = EventDescriptor.Get(eventId);
        if (_newChapter && existingEvent != null && _ownedNewEventId != eventId)
            throw new InvalidOperationException("ChapterObjectConflict");
        if (!_newChapter && (existingEvent == null || !existingEvent.CommonEvent || existingEvent.Global ||
                             existingEvent.Pages.Count != 1 || existingEvent.Pages[0].CinematicChapter == null))
            throw new InvalidOperationException("ChapterEventInvalid");
        var automaticId = _automaticCompletionId;
        if (draft.CompletionVariableId == Guid.Empty) draft.CompletionVariableId = automaticId = Guid.NewGuid();
        if (draft.UnlockVariableId != Guid.Empty && draft.UnlockVariableId == draft.CompletionVariableId)
            throw new InvalidOperationException("ChapterTrackingConflict");
        var existingVariable = PlayerVariableDescriptor.Get(draft.CompletionVariableId);
        if (draft.CompletionVariableId == automaticId && existingVariable != null && _ownedCompletionId != automaticId)
            throw new InvalidOperationException("ChapterObjectConflict");
        var createVariable = draft.CompletionVariableId == automaticId && existingVariable == null;
        var page = CinematicChapterCompiler.Compile(draft,
            id => createVariable && id == automaticId || PlayerVariableDescriptor.Get(id)?.DataType == VariableDataType.Boolean,
            ValidScene, id => id != eventId && EventDescriptor.Get(id)?.CommonEvent == true, id => QuestDescriptor.Get(id) != null);
        PlayerVariableDescriptor? variable = null;
        if (draft.CompletionVariableId == automaticId)
        {
            variable = new PlayerVariableDescriptor(automaticId);
            if (existingVariable != null) variable.Load(existingVariable.JsonData);
            variable.Name = string.Format(T("ChapterCompletionName"), draft.Name);
            variable.DataType = VariableDataType.Boolean;
            variable.TextId = CompletionTextId(eventId);
            variable.Folder = T("ChapterProgressFolder");
        }
        _chapter.CompletionVariableId = draft.CompletionVariableId;
        _automaticCompletionId = automaticId; _eventId = eventId;
        return new(eventId, page, existingEvent, variable, (type, id) =>
        { _requestedCreates.Add((type, id)); create(type, id); }, save, existingVariable != null);
    }
    private void Save(bool openAdvanced = false)
    {
        if (_saveSession?.Pending == true) return;
        if (!Networking.Network.Connected) { _status.Text = T("ChapterSaveDisconnected"); return; }
        try
        {
            _openAdvancedWhenSaved = openAdvanced;
            _saveSession = PrepareSaveSession(PacketSender.SendCreateObject, PacketSender.SendSaveObject);
            _saveSession.Changed += SaveChanged;
            _saveStarted = Environment.TickCount64; _saveTimer.Start();
            _saveSession.Start();
        }
        catch (InvalidOperationException error)
        {
            var key = error.Message switch
            {
                "chapter-completion" => "ChapterCompletionInvalid", "chapter-unlock" => "ChapterUnlockInvalid",
                "chapter-scenes" => "ChapterScenesInvalid", "event-unavailable" => "ChapterEventInvalid",
                "quest-unavailable" => "ChapterQuestInvalid", _ => error.Message,
            };
            _status.Text = T(key);
            if (key is "ChapterTrackingConflict" or "ChapterCompletionInvalid" or "ChapterUnlockInvalid") _advanced.Checked = true;
        }
    }
    private void ReceiveObject(GameObjectPacket packet)
    {
        if (IsDisposed || Disposing || !IsHandleCreated) return;
        if (InvokeRequired)
        {
            try { BeginInvoke((Action)(() => ReceiveObject(packet))); }
            catch (InvalidOperationException) { }
            return;
        }
        if (packet.CreationSucceeded && !packet.Deleted && string.IsNullOrEmpty(packet.CreationError) &&
            _requestedCreates.Contains((packet.Type, packet.Id)))
        {
            if (packet.Type == GameObjectType.PlayerVariable && packet.Id == _automaticCompletionId) _ownedCompletionId = packet.Id;
            if (packet.Type == GameObjectType.Event && packet.Id == _eventId) _ownedNewEventId = packet.Id;
        }
        _saveSession?.Receive(packet);
    }
    private void SaveChanged()
    {
        if (_saveSession == null) return;
        if (_saveSession.VariableCreated) _ownedCompletionId = _saveSession.CompletionId;
        if (_saveSession.EventCreated) _ownedNewEventId = _saveSession.EventId;
        var pending = _saveSession.Pending;
        _tools.Enabled = _library.Enabled = _scenes.Enabled = _properties.Enabled = !pending;
        _status.Text = T(_saveSession.StatusKey);
        if (pending) return;
        _saveTimer.Stop();
        if (_saveSession.Succeeded)
        {
            _newChapter = false;
            _saved = Snapshot(); RefreshLibrary(); Select(_selection ?? _chapter); UpdateGuide();
            if (_openAdvancedWhenSaved)
            {
                _openAdvancedWhenSaved = false;
                using var editor = new FrmEvent(null) { MyEvent = EventDescriptor.Get(_eventId) };
                editor.InitEditor(false, false, false); editor.ShowDialog(this);
            }
        }
    }
}
