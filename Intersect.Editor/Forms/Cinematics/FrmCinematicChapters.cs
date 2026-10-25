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
using Newtonsoft.Json;

namespace Intersect.Editor.Forms.Cinematics;

internal sealed class FrmCinematicChapters : DarkForm
{
    private CinematicChapter _chapter = new();
    private Guid _eventId;
    private string _saved = "";
    private readonly TreeView _library = new() { Dock = DockStyle.Fill, HideSelection = false };
    private readonly ListBox _scenes = new() { Dock = DockStyle.Fill };
    private readonly PropertyGrid _properties = new() { Dock = DockStyle.Fill, ToolbarVisible = false };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 45 };
    private object? _selection;
    private static string T(string key) => CinematicText.T(key);
    private string Snapshot() => JsonConvert.SerializeObject(_chapter);
    private sealed record Choice(Guid Id, string Name) { public override string ToString() => Name; }

    internal FrmCinematicChapters(EventDescriptor? existing = null)
    {
        Text = T("ChapterDirector"); Width = 1100; Height = 760; MinimumSize = new(800, 550);
        StartPosition = FormStartPosition.CenterParent; Icon = Program.Icon;
        var tools = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        void Button(string key, Action action)
        { var button = new Button { Text = T(key), AutoSize = true }; button.Click += (_, _) => action(); tools.Controls.Add(button); }
        Button("NewChapter", () => { if (CanDiscard()) LoadChapter(null); }); Button("SaveChapter", Save);
        Button("ChapterProperties", () => Select(_chapter)); Button("AddChapterScene", AddScene);
        Button("Up", () => Move(-1)); Button("Down", () => Move(1)); Button("Delete", Delete);
        Button("SelectUnlock", () => SelectVariable(false)); Button("SelectCompletion", () => SelectVariable(true));
        Button("SelectQuest", SelectQuest); Button("SelectCommonEvent", SelectCommonEvent); Button("Advanced", Advanced);
        var left = new SplitContainer { Dock = DockStyle.Fill, Size = new(1050, 650), SplitterDistance = 250 };
        var right = new SplitContainer { Dock = DockStyle.Fill, Size = new(800, 650), SplitterDistance = 340 };
        left.Panel1.Controls.Add(_library); left.Panel2.Controls.Add(right);
        right.Panel1.Controls.Add(_scenes); right.Panel2.Controls.Add(_properties);
        Controls.Add(left); Controls.Add(_status); Controls.Add(tools);
        Intersect.Editor.Forms.Styling.EditorTheme.Apply(this);
        _scenes.SelectedIndexChanged += (_, _) => { if (_scenes.SelectedItem is CinematicChapterScene entry) Select(entry); };
        _properties.PropertyValueChanged += (_, _) => { RefreshScenes(); RefreshLibrary(); };
        _library.AfterSelect += (_, e) => { if (e.Node?.Tag is EventDescriptor evt && evt.Id != _eventId && CanDiscard()) LoadChapter(evt); };
        FormClosing += (_, e) => { if (!CanDiscard()) e.Cancel = true; };
        LoadChapter(existing);
    }

    private void Select(object value) { _selection = value; _properties.SelectedObject = new CinematicProperties(value); }
    private bool CanDiscard() => _saved == Snapshot() || MessageBox.Show(this, T("Unsaved"), Text, MessageBoxButtons.OKCancel) == DialogResult.OK;
    private void LoadChapter(EventDescriptor? evt)
    {
        _eventId = evt?.Id ?? Guid.Empty;
        _chapter = evt?.Pages.FirstOrDefault()?.CinematicChapter is { } source
            ? JsonConvert.DeserializeObject<CinematicChapter>(JsonConvert.SerializeObject(source))!
            : new() { Name = T("NewChapter") };
        _saved = Snapshot(); Select(_chapter); RefreshScenes(); RefreshLibrary(); _status.Text = T("ChapterHint");
    }
    private void RefreshScenes()
    {
        var selection = _selection;
        _scenes.Items.Clear(); foreach (var entry in _chapter.Scenes) _scenes.Items.Add(entry);
        if (selection is CinematicChapterScene scene) _scenes.SelectedItem = scene;
    }
    private void RefreshLibrary()
    {
        _library.BeginUpdate(); _library.Nodes.Clear();
        foreach (var project in EventDescriptor.Lookup.Values.OfType<EventDescriptor>()
                     .Where(e => e.CommonEvent && e.Pages.FirstOrDefault()?.CinematicChapter != null)
                     .GroupBy(e => e.Pages[0].CinematicChapter!.Project).OrderBy(g => g.Key))
        {
            var root = _library.Nodes.Add(string.IsNullOrWhiteSpace(project.Key) ? T("Project") : project.Key);
            foreach (var saga in project.GroupBy(e => e.Pages[0].CinematicChapter!.Saga).OrderBy(g => g.Key))
            {
                var node = root.Nodes.Add(string.IsNullOrWhiteSpace(saga.Key) ? T("Saga") : saga.Key);
                foreach (var evt in saga.OrderBy(e => e.Name)) node.Nodes.Add(new TreeNode(evt.Name) { Tag = evt });
            }
        }
        _library.ExpandAll(); _library.EndUpdate();
    }
    private Choice? Pick(IEnumerable<Choice> choices)
    {
        using var form = new DarkForm { Text = T("Resources"), Width = 560, Height = 450, StartPosition = FormStartPosition.CenterParent };
        var list = new ListBox { Dock = DockStyle.Fill }; foreach (var choice in choices.OrderBy(c => c.Name)) list.Items.Add(choice);
        var accept = new Button { Text = T("Accept"), Dock = DockStyle.Bottom, Height = 36, DialogResult = DialogResult.OK };
        list.DoubleClick += (_, _) => { if (list.SelectedItem != null) { form.DialogResult = DialogResult.OK; form.Close(); } };
        form.Controls.Add(list); form.Controls.Add(accept); Intersect.Editor.Forms.Styling.EditorTheme.Apply(form);
        return form.ShowDialog(this) == DialogResult.OK ? list.SelectedItem as Choice : null;
    }
    private void AddScene()
    {
        if (Globals.CurrentMap is not { } map) return;
        var available = map.LocalEvents.Values.Concat(EventDescriptor.Lookup.Values.OfType<EventDescriptor>())
            .Where(e => !e.Global && !e.CommonEvent && e.Pages.Count == 1 && e.Pages[0].Cinematic != null)
            .GroupBy(e => e.Id).Select(g => g.First()).ToArray();
        if (Pick(available
                .Select(e => new Choice(e.Id, e.Name))) is not { } selected) return;
        var entry = new CinematicChapterScene { Name = selected.Name, MapId = available.Single(e => e.Id == selected.Id).MapId, SceneEventId = selected.Id };
        _chapter.Scenes.Add(entry); Select(entry); RefreshScenes();
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
    private void SelectVariable(bool completion)
    {
        if (Pick(PlayerVariableDescriptor.Lookup.Values.OfType<PlayerVariableDescriptor>().Where(v => v.DataType == VariableDataType.Boolean)
                .Select(v => new Choice(v.Id, v.Name))) is not { } selected) return;
        if (completion) _chapter.CompletionVariableId = selected.Id; else _chapter.UnlockVariableId = selected.Id;
        Select(_chapter);
    }
    private void SelectQuest()
    {
        if (Pick(QuestDescriptor.Lookup.Values.OfType<QuestDescriptor>().Select(q => new Choice(q.Id, q.Name))) is not { } selected) return;
        _chapter.StartQuestId = selected.Id; Select(_chapter);
    }
    private void SelectCommonEvent()
    {
        if (Pick(EventDescriptor.Lookup.Values.OfType<EventDescriptor>().Where(e => e.CommonEvent && e.Id != _eventId)
                .Select(e => new Choice(e.Id, e.Name))) is not { } selected) return;
        if (_selection is CinematicChapterScene entry)
        {
            if (Pick([new(Guid.Empty, T("BeforeEventId")), new(new Guid(1, 0, 0, new byte[8]), T("AfterEventId"))]) is not { } phase) return;
            if (phase.Id == Guid.Empty) entry.BeforeEventId = selected.Id; else entry.AfterEventId = selected.Id;
            Select(entry);
        }
        else { _chapter.CompletionEventId = selected.Id; Select(_chapter); }
    }
    private static bool ValidScene(Guid mapId, Guid id)
    {
        var evt = Globals.CurrentMap?.Id == mapId ? Globals.CurrentMap.LocalEvents.GetValueOrDefault(id) : EventDescriptor.Get(id);
        return evt != null && evt.MapId == mapId && !evt.Global && !evt.CommonEvent && evt.Pages.Count == 1 && evt.Pages[0].Cinematic?.MapId == mapId;
    }
    private void Save()
    {
        try
        {
            var page = CinematicChapterCompiler.Compile(_chapter,
                id => PlayerVariableDescriptor.Get(id)?.DataType == VariableDataType.Boolean, ValidScene,
                id => id != _eventId && EventDescriptor.Get(id)?.CommonEvent == true, id => QuestDescriptor.Get(id) != null);
            if (_eventId == Guid.Empty) _eventId = Guid.NewGuid();
            var evt = EventDescriptor.Get(_eventId) ?? new EventDescriptor(_eventId, true);
            if (evt.Pages.Count > 0)
            { var metadata = evt.Pages[0]; metadata.CinematicChapter = page.CinematicChapter; metadata.Cinematic = null; metadata.CommandLists = page.CommandLists; page = metadata; }
            evt.Name = _chapter.Name; evt.CommonEvent = true; evt.Global = false; evt.Pages = [page];
            EventDescriptor.Lookup.Set(evt.Id, evt); PacketSender.SendSaveObject(evt);
            _saved = Snapshot(); RefreshLibrary(); _status.Text = T("ChapterSent");
        }
        catch (InvalidOperationException error) { MessageBox.Show(this, T(error.Message), Text); }
    }
    private void Advanced()
    {
        Save(); if (_eventId == Guid.Empty || _saved != Snapshot()) return;
        using var editor = new FrmEvent(null) { MyEvent = EventDescriptor.Get(_eventId) };
        editor.InitEditor(false, false, false); editor.ShowDialog(this);
    }
}
