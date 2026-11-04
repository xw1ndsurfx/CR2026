using System.Media;
using Intersect.Framework.Core.Exportation;
using DrawingColor = System.Drawing.Color;

namespace Intersect.Editor.Forms.Editors;

/// <summary>
/// Embedded resource browser for the same Exportation Editor window. Only
/// approved fixed game asset slots may be replaced; never copy arbitrary paths
/// from server packets or player data.
/// </summary>
internal sealed class ExportationAssetsPanel : UserControl
{
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly ListBox _graphics = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly ListBox _sounds = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly PictureBox _picture = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom,
        BackColor = DrawingColor.FromArgb(25, 32, 40) };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 56,
        Padding = new Padding(10), ForeColor = DrawingColor.Gainsboro };
    private SoundPlayer? _soundPlayer;

    private static string T(string key) => ExportationText.Get(key,
        System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    private static readonly string[] Graphics =
    [
        "background.png", "sprites/planches.png", "sprites/lingots_fer.png",
        "sprites/potion_rouge.png", "sprites/tissu.png", "sprites/pommes.png",
        "sprites/caisse_scellee.png", "sprites/ancre.png", "sprites/bateau.png",
        "sprites/marchand.png", "sprites/destinataire.png", "sprites/sceau_royal.png",
        "sprites/parchemin_quete.png",
    ];

    private static readonly string[] Sounds =
    [
        "cargo_lost.wav", "cargo_seal.wav", "delivery_success.wav",
        "item_glass.wav", "item_metal.wav", "item_remove.wav",
        "item_wood.wav", "order_complete.wav", "ui_close.wav", "ui_open.wav",
    ];

    private static string Root()
    {
        var roots = new[]
        {
            Path.Combine(Environment.CurrentDirectory, "resources"),
            Path.Combine(AppContext.BaseDirectory, "resources"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "resources")),
        };
        return roots.FirstOrDefault(path =>
            Directory.Exists(Path.Combine(path, "gui", "exportation")) ||
            Directory.Exists(Path.Combine(path, "sounds", "exportation")))
               ?? roots[1];
    }

    public ExportationAssetsPanel()
    {
        BackColor = DrawingColor.FromArgb(24, 31, 42);
        ForeColor = DrawingColor.Gainsboro;
        var graphicsPage = new TabPage(T("Graphics")) { BackColor = BackColor };
        var soundsPage = new TabPage(T("Sounds")) { BackColor = BackColor };
        _tabs.TabPages.Add(graphicsPage);
        _tabs.TabPages.Add(soundsPage);

        var layout = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 270,
            Panel1MinSize = 150, Panel2MinSize = 240 };
        layout.Panel1.Controls.Add(_graphics);
        layout.Panel2.Controls.Add(_picture);
        var commands = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(8) };
        var replaceGraphic = new Button { Text = T("Import / replace"), AutoSize = true };
        replaceGraphic.Click += (_, _) => ImportGraphic();
        var refreshGraphics = new Button { Text = T("Refresh"), AutoSize = true };
        refreshGraphics.Click += (_, _) => PreviewGraphic();
        commands.Controls.Add(replaceGraphic); commands.Controls.Add(refreshGraphics);
        graphicsPage.Controls.Add(layout); graphicsPage.Controls.Add(commands);

        var soundLayout = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 320,
            Panel1MinSize = 160, Panel2MinSize = 160 };
        soundLayout.Panel1.Controls.Add(_sounds);
        var right = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true,
            FlowDirection = FlowDirection.TopDown, Padding = new Padding(12) };
        var play = new Button { Text = T("Play sound"), AutoSize = true };
        play.Click += (_, _) => PlaySound();
        var stop = new Button { Text = T("Stop"), AutoSize = true };
        stop.Click += (_, _) => _soundPlayer?.Stop();
        var import = new Button { Text = T("Import / replace"), AutoSize = true };
        import.Click += (_, _) => ImportSound();
        right.Controls.Add(play); right.Controls.Add(stop); right.Controls.Add(import);
        soundLayout.Panel2.Controls.Add(right);
        soundsPage.Controls.Add(soundLayout);

        _graphics.BackColor = _sounds.BackColor = DrawingColor.FromArgb(27, 36, 48);
        _graphics.ForeColor = _sounds.ForeColor = DrawingColor.WhiteSmoke;
        Controls.Add(_tabs); Controls.Add(_status);
        foreach (var item in Graphics) _graphics.Items.Add(item);
        foreach (var item in Sounds) _sounds.Items.Add(item);
        _graphics.SelectedIndexChanged += (_, _) => PreviewGraphic();
        _sounds.SelectedIndexChanged += (_, _) => ShowSoundPath();
        _graphics.SelectedIndex = 0; _sounds.SelectedIndex = 0;
        Disposed += (_, _) =>
        {
            _soundPlayer?.Stop();
            _picture.Image?.Dispose();
            _picture.Image = null;
        };
    }

    private string? SelectedGraphic()
    {
        var selection = _graphics.SelectedItem as string;
        return selection is not null && Graphics.Contains(selection, StringComparer.Ordinal)
            ? Path.Combine(Root(), "gui", "exportation", selection.Replace('/', Path.DirectorySeparatorChar))
            : null;
    }

    private string? SelectedSound()
    {
        var selection = _sounds.SelectedItem as string;
        return selection is not null && Sounds.Contains(selection, StringComparer.Ordinal)
            ? Path.Combine(Root(), "sounds", "exportation", selection)
            : null;
    }

    private void PreviewGraphic()
    {
        _picture.Image?.Dispose();
        _picture.Image = null;
        var path = SelectedGraphic();
        if (path == null || !File.Exists(path))
        {
            _status.Text = T("Asset not installed. Install the approved GFX/SFX pack first.");
            return;
        }
        try
        {
            using var original = Image.FromFile(path);
            _picture.Image = new Bitmap(original);
            _status.Text = Path.GetFileName(path) + " · " + original.Width + " × " + original.Height;
        }
        catch (Exception error) when (error is IOException or ArgumentException or UnauthorizedAccessException)
        {
            _status.Text = T("Unable to preview this graphic.");
        }
    }

    private void ShowSoundPath()
    {
        var file = SelectedSound();
        _status.Text = file is null || !File.Exists(file)
            ? T("Sound missing from resources.")
            : Path.GetFileName(file) + " · WAV";
    }

    private void PlaySound()
    {
        var path = SelectedSound();
        if (path == null || !File.Exists(path))
        {
            _status.Text = T("Sound missing from resources."); return;
        }
        try
        {
            _soundPlayer?.Stop();
            _soundPlayer = new SoundPlayer(path);
            _soundPlayer.Load();
            _soundPlayer.Play();
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or
            System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        {
            _status.Text = T("This WAV could not be played.");
        }
    }

    private void ImportGraphic()
    {
        var path = SelectedGraphic();
        if (path == null) return;
        using var picker = new OpenFileDialog { Title = T("Choose a PNG resource"),
            Filter = "PNG (*.png)|*.png", CheckFileExists = true };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            using var img = Image.FromFile(picker.FileName);
            var isBackground = Path.GetFileName(path) == "background.png";
            var size = isBackground ? new Size(1672, 941) : new Size(362, 362);
            if (img.Width != size.Width || img.Height != size.Height)
            {
                MessageBox.Show(this, T("Invalid image dimensions. Expected ") +
                    size.Width + " × " + size.Height, T("Graphics"), MessageBoxButtons.OK,
                    MessageBoxIcon.Warning); return;
            }
            Replace(picker.FileName, path);
            PreviewGraphic();
        }
        catch (Exception error) when (error is IOException or ArgumentException or UnauthorizedAccessException)
        {
            _status.Text = T("Unable to import this image.");
        }
    }

    private void ImportSound()
    {
        var path = SelectedSound();
        if (path == null) return;
        using var picker = new OpenFileDialog { Title = T("Choose a WAV resource"),
            Filter = "WAV (*.wav)|*.wav", CheckFileExists = true };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            using var probe = new SoundPlayer(picker.FileName);
            probe.Load();
            Replace(picker.FileName, path);
            ShowSoundPath();
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or
            System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        {
            _status.Text = T("Unable to import this WAV.");
        }
    }

    private void Replace(string source, string target)
    {
        var input = new FileInfo(source);
        if (input.Length > 16 * 1024 * 1024)
            throw new IOException("Resource exceeds 16 MiB.");
        if (Path.GetFullPath(source).Equals(Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
            return;
        if (File.Exists(target) && MessageBox.Show(this,
            T("Replace the existing resource?"), T("Graphics and sounds"),
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(source, target, overwrite: true);
        _status.Text = T("Resource installed locally. Rebuild and commit the binary file to distribute it.");
    }
}
