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

    internal CinematicPicker()
    {
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
}
