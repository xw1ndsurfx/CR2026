using Intersect.Editor.Networking;
using Intersect.Localization;
using Intersect.Network.Packets.Editor;
using Intersect.Network.Packets.Server;
using Newtonsoft.Json;

namespace Intersect.Editor.Forms;
public sealed class FrmTranslations : Form
{
    private static FrmTranslations? _open;
    private readonly CheckBox _enabled = new() { Text = "Automatically translate saved content", AutoSize = true };
    private readonly TextBox _endpoint = new() { Width = 520, PlaceholderText = "https://your-service/translate" };
    private readonly TextBox _key = new() { Width = 300, UseSystemPasswordChar = true };
    private readonly ComboBox _source = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly DataGridView _texts = new() { Dock = DockStyle.Fill, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 44 };
    private bool _dirty;
    public FrmTranslations()
    {
        Text = "Game translations - English / FranÃƒÂ§ais / EspaÃƒÂ±ol";
        Size = new Size(1100, 760); MinimumSize = new Size(800, 580); StartPosition = FormStartPosition.CenterParent;
        _open = this; FormClosed += (_, _) => { if (_open == this) _open = null; };
        _source.Items.AddRange(new object[] { "en", "fr", "es", "auto" }); _source.SelectedIndex = 0;
        var settings = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 155, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        settings.Controls.Add(_enabled);
        var endpoint = new FlowLayoutPanel { AutoSize = true }; endpoint.Controls.Add(new Label { Text = "LibreTranslate endpoint", Width = 150 }); endpoint.Controls.Add(_endpoint); settings.Controls.Add(endpoint);
        var key = new FlowLayoutPanel { AutoSize = true }; key.Controls.Add(new Label { Text = "API key (server only)", Width = 150 }); key.Controls.Add(_key); settings.Controls.Add(key);
        var source = new FlowLayoutPanel { AutoSize = true }; source.Controls.Add(new Label { Text = "Original content language", Width = 150 }); source.Controls.Add(_source); settings.Controls.Add(source);
        var commands = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42 };
        AddButton(commands, "Save service settings", () => Send("save"));
        AddButton(commands, "Translate existing content", () => Send("translate-all"));
        AddButton(commands, "Save corrections", () => Send("save-texts"));
        AddButton(commands, "Refresh", () => Send("get"));
        foreach (var name in new[] { "Source", "English", "FranÃƒÂ§ais", "EspaÃƒÂ±ol" }) _texts.Columns.Add(name, name);
        _texts.Columns[0].ReadOnly = true; _texts.CellValueChanged += (_, _) => _dirty = true;
        Controls.Add(_texts); Controls.Add(settings); Controls.Add(commands); Controls.Add(_status);
        Shown += (_, _) => Send("get");
    }
    private static void AddButton(FlowLayoutPanel panel, string text, Action click)
    { var button = new Button { Text = text, AutoSize = true, Height = 30 }; button.Click += (_, _) => click(); panel.Controls.Add(button); }
    private void Send(string action)
    {
        try
        {
            _texts.EndEdit();
            var settings = new TranslationServiceSettings { Enabled = _enabled.Checked, Endpoint = _endpoint.Text.Trim(), ApiKey = _key.Text, SourceLanguage = (string)_source.SelectedItem! };
            if (action == "save") settings.Validate();
            if (action != "save-texts" && _dirty && MessageBox.Show(this, "Discard unsaved translation edits?", "Translations", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            var catalog = new GameTextCatalog();
            if (action == "save-texts") foreach (DataGridViewRow row in _texts.Rows)
            {
                var original = Convert.ToString(row.Cells[0].Value)!; var versions = new Dictionary<string, string>();
                for (var i = 0; i < 3; i++)
                {
                    var text = Convert.ToString(row.Cells[i + 1].Value) ?? "";
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    if (!GameTextTokens.Compatible(original, text)) throw new InvalidDataException("A translation changed a variable or placeholder.");
                    versions[GameTextCatalog.Languages[i]] = text;
                }
                catalog.Entries[original] = versions;
            }
            Intersect.Editor.Networking.Network.SendPacket(new TranslationSettingsRequestPacket { Action = action, SettingsJson = JsonConvert.SerializeObject(settings), CatalogJson = catalog.ToJson() });
            _status.Text = "Waiting for the server...";
        }
        catch (Exception exception) { MessageBox.Show(this, exception.Message, "Translations"); }
    }
    public static void Receive(TranslationSettingsPacket packet)
    {
        var form = _open; if (form == null || form.IsDisposed) return;
        if (form.InvokeRequired) { form.BeginInvoke(() => Receive(packet)); return; }
        var settings = JsonConvert.DeserializeObject<TranslationServiceSettings>(packet.SettingsJson) ?? new();
        form._enabled.Checked = settings.Enabled; form._endpoint.Text = settings.Endpoint; form._key.Text = settings.ApiKey; form._source.SelectedItem = settings.SourceLanguage;
        form._texts.Rows.Clear();
        foreach (var (source, versions) in GameTextCatalog.FromJson(packet.CatalogJson).Entries.OrderBy(entry => entry.Key))
            form._texts.Rows.Add(source, versions.GetValueOrDefault("en", ""), versions.GetValueOrDefault("fr", ""), versions.GetValueOrDefault("es", ""));
        form._dirty = false; form._status.Text = packet.Status;
    }
}
