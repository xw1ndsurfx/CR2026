using Intersect.Editor.Networking;
using Intersect.Framework.Core.Exportation;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;

namespace Intersect.Editor.Forms.Editors;

/// <summary>Server-validated route JSON editor. Rejected saves retain the local draft.</summary>
public sealed class FrmExportationRoutes : Form
{
    private readonly TextBox _json = new()
    {
        Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both,
        AcceptsTab = true, WordWrap = false, Font = new Font(FontFamily.GenericMonospace, 11),
    };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 48,
        AutoEllipsis = true, Padding = new Padding(10) };
    private readonly Button _apply = new() { AutoSize = true };
    private readonly System.Windows.Forms.Timer _timeout = new() { Interval = 15000 };
    private bool _pending, _dirty, _loading;
    private static string T(string key) => ExportationText.Get(key,
        System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    public FrmExportationRoutes(string json)
    {
        Text = T("Exportation route editor");
        StartPosition = FormStartPosition.CenterParent;
        Size = new(1050, 780); MinimumSize = new(760, 520);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
        _apply.Text = T("Apply to server");
        _apply.Click += (_, _) => Save();
        var reload = new Button { Text = T("Reload from server"), AutoSize = true };
        reload.Click += (_, _) =>
        {
            if (_dirty && MessageBox.Show(this, T("Discard the local draft?"), Text,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _dirty = false;
            Network.SendPacket(new ExportationRequestPacket { Action = ExportationAction.GetConfiguration });
        };
        buttons.Controls.Add(_apply); buttons.Controls.Add(reload);
        Controls.Add(_json); Controls.Add(buttons); Controls.Add(_status);
        _json.TextChanged += (_, _) =>
        {
            if (_loading) return;
            _dirty = true; _status.Text = T("Unsaved route changes.");
        };
        _timeout.Tick += (_, _) =>
        {
            _timeout.Stop();
            if (!_pending) return;
            _pending = false; _apply.Enabled = true;
            _status.Text = T("Server did not confirm the save. Your draft is preserved.");
        };
        FormClosing += (_, e) =>
        {
            if (_dirty && MessageBox.Show(this, T("Discard the local draft?"), Text,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) e.Cancel = true;
        };
        Disposed += (_, _) => { _timeout.Stop(); _timeout.Dispose(); };
        Load(json);
    }

    private void Load(string json)
    {
        _loading = true;
        try { _json.Text = json; _dirty = false; }
        finally { _loading = false; }
        _status.Text = T("Configure source and recipient NPCs, maps, goods, quest and rewards.");
    }

    private void Save()
    {
        if (_pending) return;
        try { _ = ExportationConfiguration.FromJson(_json.Text); }
        catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException or ArgumentException)
        {
            _status.Text = T("Invalid route JSON. Nothing was saved."); return;
        }
        _pending = true; _apply.Enabled = false; _timeout.Start();
        Network.SendPacket(new ExportationRequestPacket
        {
            Action = ExportationAction.SaveConfiguration,
            ConfigurationJson = _json.Text,
        });
        _status.Text = T("Waiting for the server to confirm.");
    }

    internal void Response(ExportationStatePacket packet)
    {
        if (IsDisposed) return;
        _timeout.Stop(); _pending = false; _apply.Enabled = true;
        if (packet.Saved && packet.ConfigurationJson.Length > 0)
        {
            Load(packet.ConfigurationJson);
            _status.Text = T("Exportation routes saved.");
        }
        else if (packet.ConfigurationJson.Length > 0 && !_dirty) Load(packet.ConfigurationJson);
        else if (!string.IsNullOrWhiteSpace(packet.Message)) _status.Text = packet.Message;
    }
}
