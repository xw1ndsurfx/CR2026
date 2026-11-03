using System.Drawing.Imaging;
using Intersect.Editor.Forms.Styling;
using Intersect.Framework.Core.QualityOfLife;

namespace Intersect.Editor.Forms.Editors;

public sealed partial class FrmAchievementConfiguration
{
    private readonly DarkComboBox _lockedIcon = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly PictureBox _unlockedPreview = new() { Width = 72, Height = 72, SizeMode = PictureBoxSizeMode.Zoom, BackColor = EditorTheme.Input };
    private readonly PictureBox _lockedPreview = new() { Width = 72, Height = 72, SizeMode = PictureBoxSizeMode.Zoom, BackColor = EditorTheme.Input };
    private bool _lockedPreviewFallback;
    private static string IconText(string source) => QualityOfLifeText.Get(source,
        System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    private static string SelectedIconName(ComboBox selector) => selector.SelectedIndex <= 0 ? string.Empty : selector.Text.Trim();

    private void InitializeIconSelectors()
    {
        var directory = Path.Combine("resources", "achievements");
        SearchableChoices.SetResourceDirectory(_icon, directory);
        SearchableChoices.SetResourceDirectory(_lockedIcon, directory);
        _icon.Name = "AchievementUnlockedIcon"; _lockedIcon.Name = "AchievementLockedIcon";
        _icon.SelectedIndexChanged += (_, _) => UpdateIconPreviews();
        _lockedIcon.SelectedIndexChanged += (_, _) => UpdateIconPreviews();
        _lockedPreview.Paint += (_, args) =>
        {
            if (!_lockedPreviewFallback) return;
            using var dark = new SolidBrush(EditorTheme.Background);
            using var light = new SolidBrush(EditorTheme.Muted);
            var x = _lockedPreview.Width - 22; var y = _lockedPreview.Height - 22;
            args.Graphics.FillRectangle(dark, x, y, 22, 22);
            args.Graphics.FillRectangle(light, x + 7, y + 3, 8, 3);
            args.Graphics.FillRectangle(light, x + 5, y + 5, 3, 8);
            args.Graphics.FillRectangle(light, x + 14, y + 5, 3, 8);
            args.Graphics.FillRectangle(light, x + 4, y + 11, 14, 9);
            args.Graphics.FillRectangle(dark, x + 10, y + 13, 3, 5);
        };
    }

    private Control BuildIconPreviews()
    {
        var previews = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        previews.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        previews.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        previews.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        previews.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        foreach (var (text, image) in new[] { ("Unlocked", _unlockedPreview), ("Locked", _lockedPreview) })
        {
            var column = new FlowLayoutPanel { Width = 100, Height = 100, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            column.Controls.Add(new Label { Text = IconText(text), AutoSize = true }); column.Controls.Add(image); previews.Controls.Add(column);
        }
        previews.Controls.Add(new Label { Text = IconText("Without a locked image, the normal icon is dimmed and marked with a lock."),
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(10, 3, 3, 3) });
        return previews;
    }

    private void UpdateIconPreviews()
    {
        Bitmap? Load(ComboBox box) => box.SelectedIndex > 0 && EditorResourcePreview.ResolveFile(box.Text, Path.Combine("resources", "achievements")) is { } file
            ? EditorResourcePreview.LoadImage(file) : null;
        _unlockedPreview.Image?.Dispose(); _lockedPreview.Image?.Dispose();
        _unlockedPreview.Image = Load(_icon); _lockedPreview.Image = Load(_lockedIcon);
        _lockedPreviewFallback = _lockedPreview.Image == null;
        if (_lockedPreviewFallback && Load(_icon) is { } source)
        {
            using (source)
            {
                var dimmed = new Bitmap(source.Width, source.Height);
                using var graphics = System.Drawing.Graphics.FromImage(dimmed);
                using var attributes = new ImageAttributes();
                attributes.SetColorMatrix(new ColorMatrix { Matrix00 = .57f, Matrix11 = .57f, Matrix22 = .57f });
                graphics.DrawImage(source, new Rectangle(0, 0, source.Width, source.Height), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
                _lockedPreview.Image = dimmed;
            }
        }
        _lockedPreview.Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _unlockedPreview.Image?.Dispose(); _unlockedPreview.Image = null;
            _lockedPreview.Image?.Dispose(); _lockedPreview.Image = null;
        }
        base.Dispose(disposing);
    }
}
