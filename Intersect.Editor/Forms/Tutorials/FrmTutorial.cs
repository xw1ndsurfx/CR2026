using System.Globalization;
using Intersect.Editor.Networking;
using Intersect.Framework.Core.Tutorials;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.GameObjects.Maps.MapList;
using Intersect.Network.Packets.Tutorials;
using Intersect.Editor.General;
using Newtonsoft.Json;
namespace Intersect.Editor.Forms.Tutorials;
internal sealed class FrmTutorial : Form
{
    private TutorialConfiguration _config;
    private string _revision;
    private readonly ListBox _steps = new() { Dock = DockStyle.Left, Width = 250 };
    private readonly PropertyGrid _properties = new() { Dock = DockStyle.Fill };
    private readonly TextBox[] _titles = new TextBox[3], _texts = new TextBox[3];
    private readonly ComboBox _scene = new() { Width = 350, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Name" };
    private readonly Button _save;
    private readonly RoyalGuidePreview _preview = new();
    private readonly ComboBox _highlightTarget = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 258 };
    private readonly ComboBox _highlightStyle = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 258 };
    private readonly ComboBox _worldMap = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 258 };
    private readonly ComboBox _worldNpc = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 258 };
    private readonly NumericUpDown _worldX = new() { Minimum = 0, Maximum = 255, Width = 90 };
    private readonly NumericUpDown _worldY = new() { Minimum = 0, Maximum = 255, Width = 90 };
    private readonly NumericUpDown _worldRadius = new() { Minimum = 0, Maximum = 6, Width = 90 };
    private sealed record TargetChoice(Guid Id, string Name)
    {
        public override string ToString() => Name;
    }
    private TutorialStep? _selected;
    private bool _loading, _dirty;
    private readonly System.Windows.Forms.Timer _saveTimeout = new() { Interval = 15000 };
    internal static string T(string source) => TutorialText.Get(source, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    internal FrmTutorial(TutorialEditorPacket packet)
    {
        _config = JsonConvert.DeserializeObject<TutorialConfiguration>(packet.Json)!; _revision = packet.Revision;
        Text = T("Tutorial editor"); Width = 1150; Height = 780;
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 75, AutoSize = false };
        Button Action(string text, Action run) { var b = new Button { Text = T(text), AutoSize = true }; b.Click += (_, _) => run(); toolbar.Controls.Add(b); return b; }
        _save = Action("Save", Save);
        Action("Add step", () => { Flush(); var step = new TutorialStep { Title = T("New step"), Text = T("New step") }; _config.Steps.Add(step); Select(step); _dirty = true; });
        Action("Delete step", () => { if (_selected == null || _config.Steps.Count <= 1) return; _config.Steps.Remove(_selected); _selected = null; Select(_config.Steps[0]); _dirty = true; });
        Action("Add combat lessons", () =>
        {
            Flush();
            var added = AdvancedCombatTutorialLessons.AppendTo(_config);
            if (added == 0)
            {
                MessageBox.Show(this, T("Combat lessons already added."));
                return;
            }
            Select(_config.Steps.First(step => step.Objective == TutorialObjective.CombatDodge));
            _dirty = true;
        });
        Action("Move up", () => Move(-1)); Action("Move down", () => Move(1));
        Action("Restore default", () => { if (MessageBox.Show(this, T("Replace this draft with the default guide?"), Text, MessageBoxButtons.YesNo) != DialogResult.Yes) return; var wasEnabled = _config.Enabled; var wasAuto = _config.AutoStart; _config = TutorialConfiguration.CreateDefault(); _config.Enabled = wasEnabled; _config.AutoStart = wasAuto; _selected = null; Select(_config.Steps[0]); _dirty = true; });
        var enabled = new CheckBox { Text = T("Enabled"), Checked = _config.Enabled, AutoSize = true };
        enabled.CheckedChanged += (_, _) => { _config.Enabled = enabled.Checked; _dirty = true; }; toolbar.Controls.Add(enabled);
        var auto = new CheckBox { Text = T("Start on first login"), Checked = _config.AutoStart, AutoSize = true };
        auto.CheckedChanged += (_, _) => { _config.AutoStart = auto.Checked; _dirty = true; }; toolbar.Controls.Add(auto);
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var settings = new TabPage(T("Objective")); settings.Controls.Add(_properties); tabs.TabPages.Add(settings);
        tabs.TabPages.Add(BuildVisualGuideTab());

        var languages = new[] { "en", "fr", "es" };
        for (var i = 0; i < 3; i++)
        {
            var tab = new TabPage(languages[i].ToUpperInvariant()); var title = _titles[i] = new TextBox { Dock = DockStyle.Top };
            var text = _texts[i] = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical };
            tab.Controls.Add(text); tab.Controls.Add(title); tabs.TabPages.Add(tab);
            var isEnglishTitle = i == 0;
            title.TextChanged += (_, _) =>
            {
                if (_loading) return;
                _dirty = true;
                if (isEnglishTitle) _preview.ShowStep(_selected, title.Text);
            };
            text.TextChanged += (_, _) => { if (!_loading) _dirty = true; };
        }
        var scenes = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 60 };
        scenes.Controls.Add(new Label { Text = T("Cinematic common event"), AutoSize = true }); scenes.Controls.Add(_scene);
        Controls.Add(tabs); Controls.Add(_steps); Controls.Add(scenes); Controls.Add(toolbar);
        _steps.DisplayMember = "Title";
        _steps.SelectedIndexChanged += (_, _) => { if (_loading) return; Flush(); LoadStep(_steps.SelectedItem as TutorialStep); };
        _properties.PropertyValueChanged += (_, _) => { _dirty = true; RefreshWorldControls(); };
        _scene.SelectedIndexChanged += (_, _) => { if (!_loading && _selected != null) { _selected.CinematicEventId = (_scene.SelectedItem as SceneChoice)?.Id ?? Guid.Empty; _dirty = true; } };
        FormClosing += (_, e) => { if (!_save.Enabled) { e.Cancel = true; return; } if (_dirty && MessageBox.Show(this, T("Discard unsaved changes?"), Text, MessageBoxButtons.YesNo) != DialogResult.Yes) e.Cancel = true; };
        _saveTimeout.Tick += (_, _) => { _saveTimeout.Stop(); SetPending(false); MessageBox.Show(this, T("Save timed out. Reopen the editor before retrying.")); };
        FormClosed += (_, _) => _saveTimeout.Dispose();
        Select(_config.Steps[0]);
    }
    private TabPage BuildVisualGuideTab()
    {
        var tab = new TabPage(T("Visual guide"));
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 292));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var controls = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(12),
        };

        Label Caption(string title) => new()
        {
            Text = T(title), Width = 258, Height = 28,
            Padding = new Padding(0, 7, 0, 0),
        };
        controls.Controls.Add(Caption("Highlight target"));
        foreach (var value in Enum.GetValues<TutorialHighlightTarget>())
            _highlightTarget.Items.Add(value);
        controls.Controls.Add(_highlightTarget);

        controls.Controls.Add(Caption("Highlight shape"));
        foreach (var value in Enum.GetValues<TutorialHighlightStyle>())
            _highlightStyle.Items.Add(value);
        controls.Controls.Add(_highlightStyle);

        controls.Controls.Add(Caption("Target map"));
        _worldMap.Items.Add(new TargetChoice(Guid.Empty, T("None")));
        foreach (var map in MapList.OrderedMaps.Where(map => map.MapId != Guid.Empty).DistinctBy(map => map.MapId))
            _worldMap.Items.Add(new TargetChoice(map.MapId, map.Name));
        controls.Controls.Add(_worldMap);
        var openMap = new Button { Text = T("Use open map"), Width = 258, Height = 30 };
        openMap.Click += (_, _) =>
        {
            if (Globals.CurrentMap == null) return;
            SelectChoice(_worldMap, Globals.CurrentMap.Id);
        };
        controls.Controls.Add(openMap);

        controls.Controls.Add(Caption("Target NPC"));
        _worldNpc.Items.Add(new TargetChoice(Guid.Empty, T("None")));
        foreach (var npc in NPCDescriptor.Lookup.Values.OfType<NPCDescriptor>().OrderBy(npc => npc.Name))
            _worldNpc.Items.Add(new TargetChoice(npc.Id, npc.Name));
        controls.Controls.Add(_worldNpc);

        controls.Controls.Add(Caption("Target tile X and Y"));
        var coords = new FlowLayoutPanel { Width = 258, Height = 30 };
        coords.Controls.AddRange([new Label { Text = "X", Width = 18, TextAlign = System.Drawing.ContentAlignment.MiddleCenter }, _worldX,
            new Label { Text = "Y", Width = 18, TextAlign = ContentAlignment.MiddleCenter }, _worldY]);
        controls.Controls.Add(coords);
        controls.Controls.Add(Caption("Arrival radius"));
        controls.Controls.Add(_worldRadius);

        controls.Controls.Add(new Label
        {
            Text = T("Preview is illustrative. Select a real map, NPC and coordinates."),
            Width = 258, Height = 72, AutoSize = false,
            ForeColor = System.Drawing.Color.DimGray,
        });

        _highlightTarget.SelectedIndexChanged += (_, _) =>
            ChangeWorld(step => step.HighlightTarget = (TutorialHighlightTarget)_highlightTarget.SelectedItem!);
        _highlightStyle.SelectedIndexChanged += (_, _) =>
            ChangeWorld(step => step.HighlightStyle = (TutorialHighlightStyle)_highlightStyle.SelectedItem!);
        _worldMap.SelectedIndexChanged += (_, _) =>
            ChangeWorld(step => step.MapId = (_worldMap.SelectedItem as TargetChoice)?.Id ?? Guid.Empty);
        _worldNpc.SelectedIndexChanged += (_, _) =>
            ChangeWorld(step => step.TargetNpcId = (_worldNpc.SelectedItem as TargetChoice)?.Id ?? Guid.Empty);
        _worldX.ValueChanged += (_, _) => ChangeWorld(step => step.TargetX = (int)_worldX.Value);
        _worldY.ValueChanged += (_, _) => ChangeWorld(step => step.TargetY = (int)_worldY.Value);
        _worldRadius.ValueChanged += (_, _) => ChangeWorld(step => step.TargetRadius = (int)_worldRadius.Value);

        layout.Controls.Add(controls, 0, 0);
        layout.Controls.Add(_preview, 1, 0);
        tab.Controls.Add(layout);
        return tab;
    }

    private void ChangeWorld(Action<TutorialStep> change)
    {
        if (_loading || _selected == null) return;
        change(_selected);
        _dirty = true;
        _properties.Refresh();
        _preview.ShowStep(_selected, _titles[0].Text);
    }

    private void RefreshWorldControls()
    {
        var oldLoading = _loading;
        _loading = true;
        try
        {
            var step = _selected;
            _highlightTarget.SelectedItem = step?.HighlightTarget ?? TutorialHighlightTarget.None;
            _highlightStyle.SelectedItem = step?.HighlightStyle ?? TutorialHighlightStyle.Outline;
            SelectChoice(_worldMap, step?.MapId ?? Guid.Empty);
            SelectChoice(_worldNpc, step?.TargetNpcId ?? Guid.Empty);
            _worldX.Value = Math.Clamp(step?.TargetX ?? 0, 0, 255);
            _worldY.Value = Math.Clamp(step?.TargetY ?? 0, 0, 255);
            _worldRadius.Value = Math.Clamp(step?.TargetRadius ?? 1, 0, 6);
            _preview.ShowStep(step, _titles[0].Text);
        }
        finally { _loading = oldLoading; }
    }

    private static void SelectChoice(ComboBox combo, Guid id)
    {
        var choice = combo.Items.Cast<TargetChoice>().FirstOrDefault(item => item.Id == id);
        if (choice == null)
        {
            choice = new TargetChoice(id, id.ToString());
            combo.Items.Add(choice); // Preserve stale values until manually fixed.
        }
        combo.SelectedItem = choice;
    }

    private sealed record SceneChoice(Guid Id, string Name);
    private void Move(int delta)
    { if (_selected == null) return; Flush(); var index = _config.Steps.IndexOf(_selected); var target = index + delta; if (target < 0 || target >= _config.Steps.Count) return; _config.Steps.RemoveAt(index); _config.Steps.Insert(target, _selected); Select(_selected); _dirty = true; }
    private void Select(TutorialStep step)
    { _loading = true; _steps.DataSource = null; _steps.DataSource = _config.Steps.ToArray(); _steps.SelectedItem = step; _loading = false; LoadStep(step); }
    private void LoadStep(TutorialStep? step)
    {
        _selected = step; _loading = true; _properties.SelectedObject = step;
        if (step != null) for (var i = 0; i < 3; i++) { var lang = new[] { "en", "fr", "es" }[i]; _titles[i].Text = _config.Translations.Get(step.Title, lang); _texts[i].Text = _config.Translations.Get(step.Text, lang); }
        var choices = new List<SceneChoice> { new(Guid.Empty, T("None")) };
        choices.AddRange(EventDescriptor.Lookup.Values.OfType<EventDescriptor>().Where(e => e.CommonEvent).Select(e => new SceneChoice(e.Id, e.Name)));
        _scene.DataSource = choices; _scene.SelectedItem = choices.FirstOrDefault(c => c.Id == step?.CinematicEventId) ?? choices[0];
        RefreshWorldControls();
        _loading = false;
    }
    private void Flush()
    {
        if (_selected == null) return;
        _selected.Title = _titles[0].Text; _selected.Text = _texts[0].Text;
        _config.Translations.Entries[Intersect.Localization.GameTextCatalog.NormalizeText(_selected.Title)] = new() { ["en"] = _titles[0].Text, ["fr"] = _titles[1].Text, ["es"] = _titles[2].Text };
        _config.Translations.Entries[Intersect.Localization.GameTextCatalog.NormalizeText(_selected.Text)] = new() { ["en"] = _texts[0].Text, ["fr"] = _texts[1].Text, ["es"] = _texts[2].Text };
    }
    private void Save()
    {
        Flush(); if (!_config.IsValid) { MessageBox.Show(this, T("Check steps and all three translations.")); return; }
        SetPending(true); _saveTimeout.Start();
        Intersect.Editor.Networking.Network.SendPacket(new TutorialEditorPacket { Save = true, Revision = _revision, Json = JsonConvert.SerializeObject(_config) });
    }
    private void SetPending(bool pending)
    { foreach (Control control in Controls) control.Enabled = !pending; _save.Enabled = !pending; }
    internal void Receive(TutorialEditorPacket packet)
    {
        _saveTimeout.Stop(); SetPending(false);
        if (packet.Error.Length > 0) { MessageBox.Show(this, packet.Error); return; }
        _revision = packet.Revision; if (packet.Saved) { _dirty = false; MessageBox.Show(this, T("Saved")); }
    }
}
