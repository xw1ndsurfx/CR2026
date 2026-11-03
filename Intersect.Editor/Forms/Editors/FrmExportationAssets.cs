using System.Globalization;
using System.Media;
using Intersect.Framework.Core.Exportation;

namespace Intersect.Editor.Forms.Editors;

/// <summary>
/// Preview-only asset inspector. It never modifies live server configuration.
/// This makes it possible to verify shipped art and WAVs before server wiring.
/// </summary>
public sealed class FrmExportationAssets : Form
{
    private readonly ListBox _files = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly PictureBox _image = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom,
        BackColor = Color.FromArgb(31, 30, 24) };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 48,
        Padding = new Padding(12), AutoEllipsis = true };
    private readonly Button _play = new() { AutoSize = true };
    private SoundPlayer? _player;

    private static string Lang => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
    private static string T(string source) => ExportationText.Get(source, Lang);

    private static readonly string[] ImageFiles =
    [
        "background.png", "sprites/planches.png", "sprites/lingots_fer.png",
        "sprites/potion_rouge.png", "sprites/tissu.png", "sprites/pommes.png",
        "sprites/caisse_scellee.png", "sprites/ancre.png", "sprites/bateau.png",
        "sprites/marchand.png", "sprites/destinataire.png",
        "sprites/sceau_royal.png", "sprites/parchemin_quete.png",
    ];

    private static readonly string[] SoundFiles =
    [
        "cargo_lost.wav", "cargo_seal.wav", "delivery_success.wav",
        "item_glass.wav", "item_metal.wav", "item_remove.wav",
        "item_wood.wav", "order_complete.wav", "ui_close.wav", "ui_open.wav",
    ];

    private static string? Root()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "resources"),
            Path.Combine(Environment.CurrentDirectory, "resources"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "resources")),
        };
        return candidates.FirstOrDefault(path =>
            File.Exists(Path.Combine(path, "gui", "exportation", "background.png")));
    }

    public FrmExportationAssets()
    {
        Text = T("Exportation graphics and sounds");
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1120, 760);
        MinimumSize = new Size(720, 500);
        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 245 };
        split.Panel1.Controls.Add(_files);
        split.Panel2.Controls.Add(_image);
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true,
            Padding = new Padding(10) };
        _play.Text = T("Play sound");
        _play.Click += (_, _) => PlaySelected();
        toolbar.Controls.Add(_play);
        var refresh = new Button { Text = T("Refresh assets"), AutoSize = true };
        refresh.Click += (_, _) => LoadFiles();
        toolbar.Controls.Add(refresh);
        var description = new Label { Text = T("Preview only: route editing and live quest integration are not connected yet."),
            AutoSize = true, Padding = new Padding(12, 8, 0, 0) };
        toolbar.Controls.Add(description);
        Controls.Add(split);
        Controls.Add(toolbar);
        Controls.Add(_status);
        _files.SelectedIndexChanged += (_, _) => PreviewSelected();
        FormClosed += (_, _) =>
        {
            _player?.Stop();
            _image.Image?.Dispose();
            _image.Image = null;
        };
        LoadFiles();
    }

    private void LoadFiles()
    {
        _files.BeginUpdate();
        var selected = _files.SelectedItem as string;
        _files.Items.Clear();
        foreach (var file in ImageFiles) _files.Items.Add("gui/exportation/" + file);
        foreach (var file in SoundFiles) _files.Items.Add("sounds/exportation/" + file);
        _files.SelectedItem = selected;
        if (_files.SelectedIndex < 0 && _files.Items.Count > 0) _files.SelectedIndex = 0;
        _files.EndUpdate();
        PreviewSelected();
    }

    private void PreviewSelected()
    {
        _player?.Stop();
        _image.Image?.Dispose();
        _image.Image = null;
        _play.Enabled = false;
        if (_files.SelectedItem is not string relative) return;
        var root = Root();
        if (root == null)
        {
            _status.Text = T("Exportation assets are missing. Install the approved ZIP and rebuild the editor.");
            return;
        }
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            _status.Text = string.Format(CultureInfo.CurrentCulture, T("Missing asset: {0}"), relative);
            return;
        }
        try
        {
            if (relative.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                // Clone the file image so the editor does not lock the asset.
                using var original = Image.FromFile(path);
                _image.Image = new Bitmap(original);
                _status.Text = $"{relative} — {original.Width} × {original.Height}";
            }
            else
            {
                _play.Enabled = true;
                _status.Text = relative;
            }
        }
        catch (Exception error) when (error is IOException or ArgumentException or UnauthorizedAccessException)
        {
            _status.Text = string.Format(CultureInfo.CurrentCulture, T("Cannot preview: {0}"), relative);
        }
    }

    private void PlaySelected()
    {
        if (_files.SelectedItem is not string relative ||
            !relative.StartsWith("sounds/exportation/", StringComparison.Ordinal) ||
            !SoundFiles.Contains(Path.GetFileName(relative), StringComparer.Ordinal))
            return;
        var root = Root();
        if (root is null) return;
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path)) return;
        try
        {
            _player?.Stop();
            _player = new SoundPlayer(path);
            _player.Play();
        }
        catch (Exception error) when (error is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            _status.Text = string.Format(CultureInfo.CurrentCulture, T("Cannot preview: {0}"), relative);
        }
    }
}
