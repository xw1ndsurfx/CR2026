using System.Globalization;
using Intersect.Editor.Networking;
using Intersect.Framework.Core.Tutorials;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Network.Packets.Tutorials;
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
        Action("Move up", () => Move(-1)); Action("Move down", () => Move(1));
        Action("Restore default", () => { if (MessageBox.Show(this, T("Replace this draft with the default guide?"), Text, MessageBoxButtons.YesNo) != DialogResult.Yes) return; var wasEnabled = _config.Enabled; var wasAuto = _config.AutoStart; _config = TutorialConfiguration.CreateDefault(); _config.Enabled = wasEnabled; _config.AutoStart = wasAuto; _selected = null; Select(_config.Steps[0]); _dirty = true; });
        var enabled = new CheckBox { Text = T("Enabled"), Checked = _config.Enabled, AutoSize = true };
        enabled.CheckedChanged += (_, _) => { _config.Enabled = enabled.Checked; _dirty = true; }; toolbar.Controls.Add(enabled);
        var auto = new CheckBox { Text = T("Start on first login"), Checked = _config.AutoStart, AutoSize = true };
        auto.CheckedChanged += (_, _) => { _config.AutoStart = auto.Checked; _dirty = true; }; toolbar.Controls.Add(auto);
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var settings = new TabPage(T("Objective")); settings.Controls.Add(_properties); tabs.TabPages.Add(settings);
        var languages = new[] { "en", "fr", "es" };
        for (var i = 0; i < 3; i++)
        {
            var tab = new TabPage(languages[i].ToUpperInvariant()); var title = _titles[i] = new TextBox { Dock = DockStyle.Top };
            var text = _texts[i] = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical };
            tab.Controls.Add(text); tab.Controls.Add(title); tabs.TabPages.Add(tab);
            title.TextChanged += (_, _) => { if (!_loading) _dirty = true; }; text.TextChanged += (_, _) => { if (!_loading) _dirty = true; };
        }
        var scenes = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 60 };
        scenes.Controls.Add(new Label { Text = T("Cinematic common event"), AutoSize = true }); scenes.Controls.Add(_scene);
        Controls.Add(tabs); Controls.Add(_steps); Controls.Add(scenes); Controls.Add(toolbar);
        _steps.DisplayMember = "Title";
        _steps.SelectedIndexChanged += (_, _) => { if (_loading) return; Flush(); LoadStep(_steps.SelectedItem as TutorialStep); };
        _properties.PropertyValueChanged += (_, _) => _dirty = true;
        _scene.SelectedIndexChanged += (_, _) => { if (!_loading && _selected != null) { _selected.CinematicEventId = (_scene.SelectedItem as SceneChoice)?.Id ?? Guid.Empty; _dirty = true; } };
        FormClosing += (_, e) => { if (!_save.Enabled) { e.Cancel = true; return; } if (_dirty && MessageBox.Show(this, T("Discard unsaved changes?"), Text, MessageBoxButtons.YesNo) != DialogResult.Yes) e.Cancel = true; };
        _saveTimeout.Tick += (_, _) => { _saveTimeout.Stop(); SetPending(false); MessageBox.Show(this, T("Save timed out. Reopen the editor before retrying.")); };
        FormClosed += (_, _) => _saveTimeout.Dispose();
        Select(_config.Steps[0]);
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
        _scene.DataSource = choices; _scene.SelectedItem = choices.FirstOrDefault(c => c.Id == step?.CinematicEventId) ?? choices[0]; _loading = false;
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
