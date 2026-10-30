using Intersect.Editor.Forms.Styling;
using Intersect.Framework.Core.QualityOfLife;

namespace Intersect.Editor.Forms.Editors;

// Selection is a detached draft. Only the creator's Apply action changes the object.
internal sealed class AutoCreatorAssetPicker : UserControl
{
    private sealed record Choice(string Asset)
    {
        public override string ToString() => Asset.Length == 0 ? T("None") : Asset;
    }

    private readonly ComboBox _choices = new() { Name = "AssetChoices", DropDownStyle = ComboBoxStyle.DropDownList, DropDownWidth = 400 };
    private readonly PictureBox _image = new() { Name = "AssetPreview", SizeMode = PictureBoxSizeMode.Zoom, BackColor = System.Drawing.Color.FromArgb(30, 30, 33) };
    private readonly Label _status = new() { AutoSize = false };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 120 };
    private readonly string _folder;
    private EditorObjectPreview? _preview;
    internal string SelectedAsset => (_choices.SelectedItem as Choice)?.Asset ?? "";
    private static string T(string text) => QualityOfLifeText.Get(text, System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    internal AutoCreatorAssetPicker(string caption, string folder, IEnumerable<string> assets, string? current)
    {
        if (string.IsNullOrWhiteSpace(current) || string.Equals(current, "None", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(current, Intersect.Editor.Localization.Strings.General.None.ToString(), StringComparison.OrdinalIgnoreCase)) current = "";
        _folder = folder; Height = 100; BackColor = System.Drawing.Color.FromArgb(45, 45, 48); ForeColor = System.Drawing.Color.Gainsboro;
        var label = new Label { Text = T(caption), AutoSize = false, Location = new(0, 4), Size = new(210, 23) };
        _choices.SetBounds(0, 30, 310, 26); _image.SetBounds(330, 2, 86, 86); _status.SetBounds(0, 60, 310, 36);
        Controls.AddRange([label, _choices, _image, _status]);
        _choices.Items.Add(new Choice(""));
        foreach (var asset in assets.Append(current ?? "").Where(a => !string.IsNullOrWhiteSpace(a)).Distinct(StringComparer.OrdinalIgnoreCase))
            _choices.Items.Add(new Choice(asset));
        _choices.SelectedItem = _choices.Items.Cast<Choice>().First(c => string.Equals(c.Asset, current ?? "", StringComparison.OrdinalIgnoreCase));
        _choices.SelectedIndexChanged += (_, _) => RefreshPreview();
        _timer.Tick += (_, _) => RenderFrame();
        SizeChanged += (_, _) => {
            var width = Math.Max(120, Width - 102);
            label.Width = width; _choices.Width = width; _status.Width = width;
            _image.Left = Width - 90;
        };
        RefreshPreview();
    }

    internal static AutoCreatorAssetPicker Insert(Form dialog, string name, string caption, string folder, IEnumerable<string> assets, string? current)
    {
        const int offset = 110;
        foreach (Control control in dialog.Controls) control.Top += offset;
        var picker = new AutoCreatorAssetPicker(caption, folder, assets, current) { Name = name };
        picker.SetBounds(20, 10, Math.Max(280, dialog.ClientSize.Width - 60), 100);
        picker.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        dialog.Controls.Add(picker);
        dialog.AutoScrollMinSize = new Size(Math.Max(dialog.AutoScrollMinSize.Width, picker.Right + 20),
            dialog.Controls.Cast<Control>().Max(c => c.Bottom) + 24);
        return picker;
    }

    internal void ApplyTo(ComboBox target)
    {
        var index = SelectedAsset.Length == 0 ? 0 : target.FindStringExact(SelectedAsset);
        // The extra current entry may refer to a missing resource. Keep that reference
        // without forcing the legacy editor to load a non-existent image from disk.
        if (index < 0 || index >= target.Items.Count) return;
        target.SelectedIndex = index;
    }

    private void RefreshPreview()
    {
        _timer.Stop(); _preview?.Dispose();
        _preview = _folder == "entities" ? EditorObjectPreview.EntityFile(SelectedAsset) : EditorObjectPreview.IconFile(SelectedAsset, _folder);
        _status.Text = SelectedAsset.Length == 0 ? "" : _preview.HasImage ? T("Preview") : T("Preview unavailable");
        RenderFrame();
        if (_preview.Animated) _timer.Start();
    }

    private void RenderFrame()
    {
        var previous = _image.Image; _image.Image = _preview?.Render(); previous?.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _timer.Dispose(); _preview?.Dispose(); _image.Image?.Dispose(); _image.Image = null; }
        base.Dispose(disposing);
    }
}
