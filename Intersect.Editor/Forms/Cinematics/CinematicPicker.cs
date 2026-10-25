using System.Globalization;

namespace Intersect.Editor.Forms.Cinematics;

// Filtering keeps object identity, including resources with identical names.
internal sealed class CinematicPicker<T> : UserControl where T : class
{
    private readonly TextBox _search = new() { Dock = DockStyle.Top };
    private readonly ListBox _list = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly Label _empty = new() { Dock = DockStyle.Bottom, Height = 44 };
    private T[] _choices = [];
    private T? _remembered;
    internal T? SelectedItem => _list.SelectedItem as T;
    internal event Action<T?>? SelectionChanged;
    internal event Action? Accepted;
    internal Func<T, DataObject>? DragData;
    private System.Drawing.Point? _dragOrigin;
    private readonly Dictionary<T, Bitmap?> _thumbnails = [];

    internal CinematicPicker(Func<T, Bitmap?>? thumbnail = null, string? glyph = null)
    {
        if (thumbnail != null || glyph != null)
        {
            _list.DrawMode = DrawMode.OwnerDrawFixed; _list.ItemHeight = 68;
            _list.DrawItem += (_, e) =>
            {
                if (e.Index < 0 || _list.Items[e.Index] is not T item) return;
                var selected = (e.State & DrawItemState.Selected) != 0;
                using var fill = new SolidBrush(selected ? Styling.EditorTheme.Selection : Styling.EditorTheme.Input);
                e.Graphics.FillRectangle(fill, e.Bounds);
                if (!_thumbnails.TryGetValue(item, out var image)) _thumbnails[item] = image = thumbnail?.Invoke(item);
                var box = new Rectangle(e.Bounds.X + 8, e.Bounds.Y + 6, 52, 52);
                if (image != null)
                {
                    var state = e.Graphics.Save();
                    e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    var scale = Math.Min(box.Width / (float)image.Width, box.Height / (float)image.Height);
                    var width = Math.Max(1, (int)(image.Width * scale)); var height = Math.Max(1, (int)(image.Height * scale));
                    e.Graphics.DrawImage(image, box.X + (box.Width - width) / 2, box.Y + (box.Height - height) / 2, width, height);
                    e.Graphics.Restore(state);
                }
                else TextRenderer.DrawText(e.Graphics, glyph ?? "◇", _list.Font, box, Styling.EditorTheme.Accent,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(e.Graphics, item.ToString(), _list.Font,
                    new Rectangle(e.Bounds.X + 68, e.Bounds.Y + 10, Math.Max(1, e.Bounds.Width - 76), 24), Styling.EditorTheme.Text,
                    TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
                TextRenderer.DrawText(e.Graphics, CinematicText.T("DragToTimeline"), _list.Font,
                    new Rectangle(e.Bounds.X + 68, e.Bounds.Y + 35, Math.Max(1, e.Bounds.Width - 76), 22), Styling.EditorTheme.Muted,
                    TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
                e.DrawFocusRectangle();
            };
        }
        _search.PlaceholderText = _search.AccessibleName = CinematicText.T("SearchChoices");
        _list.AccessibleName = CinematicText.T("Resources");
        _empty.Text = CinematicText.T("NoMatchingChoices");
        Controls.Add(_list); Controls.Add(_empty); Controls.Add(_search);
        _search.TextChanged += (_, _) => Filter();
        _search.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Down && _list.Items.Count > 0)
            { _list.Focus(); if (_list.SelectedIndex < 0) _list.SelectedIndex = 0; e.SuppressKeyPress = true; }
        };
        _list.SelectedIndexChanged += (_, _) =>
        {
            if (SelectedItem is { } selected) _remembered = selected;
            SelectionChanged?.Invoke(SelectedItem);
        };
        _list.DoubleClick += (_, _) => { if (SelectedItem != null) Accepted?.Invoke(); };
        _list.MouseDown += (_, e) => _dragOrigin = e.Button == MouseButtons.Left ? e.Location : null;
        _list.MouseUp += (_, _) => _dragOrigin = null;
        _list.MouseMove += (_, e) =>
        {
            if (e.Button != MouseButtons.Left || _dragOrigin is not { } origin || DragData == null || SelectedItem is not { } selected) return;
            var threshold = new Rectangle(origin.X - SystemInformation.DragSize.Width / 2,
                origin.Y - SystemInformation.DragSize.Height / 2, SystemInformation.DragSize.Width, SystemInformation.DragSize.Height);
            if (threshold.Contains(e.Location)) return;
            _dragOrigin = null;
            _list.DoDragDrop(DragData(selected), DragDropEffects.Copy);
        };
        _list.KeyDown += (_, e) =>
        { if (e.KeyCode == Keys.Enter && SelectedItem != null) { Accepted?.Invoke(); e.SuppressKeyPress = true; } };
    }

    internal void SetChoices(IEnumerable<T> choices, T? selected = null)
    { _choices = choices.ToArray(); _remembered = selected; Filter(); }

    private void Filter()
    {
        var selected = _remembered;
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var item in _choices.Where(item => CultureInfo.CurrentCulture.CompareInfo.IndexOf(
                     item.ToString() ?? "", _search.Text.Trim(), CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0))
            _list.Items.Add(item);
        if (selected != null && _list.Items.Contains(selected)) _list.SelectedItem = selected;
        _empty.Visible = _list.Items.Count == 0;
        _list.EndUpdate();
        SelectionChanged?.Invoke(SelectedItem);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { foreach (var bitmap in _thumbnails.Values) bitmap?.Dispose(); _thumbnails.Clear(); }
        base.Dispose(disposing);
    }
}
