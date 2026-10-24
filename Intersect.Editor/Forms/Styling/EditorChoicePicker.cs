using System.Media;
using Intersect.Framework.Core.QualityOfLife;

namespace Intersect.Editor.Forms.Styling;

internal sealed class EditorChoicePicker : Form
{
    private readonly ComboBox _source;
    private readonly EditorChoice[] _entries;
    private readonly IReadOnlyCollection<object> _recent;
    private readonly string? _directory;
    private readonly TextBox _search = new() { Dock = DockStyle.Fill };
    private readonly LogiklikComboBox _type = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
    private readonly LogiklikComboBox _sort = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly LogiklikCheckBox _recentOnly = new() { AutoSize = false, Width = 155, Height = 26 };
    private readonly DataGridView _list = new()
    {
        Dock = DockStyle.Fill, VirtualMode = true, ReadOnly = true, AllowUserToAddRows = false,
        AllowUserToDeleteRows = false, AllowUserToResizeRows = false, RowHeadersVisible = false,
        MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, AutoGenerateColumns = false,
    };
    private readonly PreviewCanvas _image = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = EditorTheme.Background };
    private readonly Label _details = new() { Dock = DockStyle.Top, Height = 76, Padding = new Padding(8), AutoEllipsis = true };
    private readonly Label _previewStatus = new() { Dock = DockStyle.Bottom, Height = 62, Padding = new Padding(8), AutoEllipsis = true };
    private readonly Label _count = new() { Dock = DockStyle.Bottom, Height = 28, Padding = new Padding(4) };
    private readonly LogiklikButton _play = new() { Width = 96, Height = 30 };
    private readonly LogiklikButton _stop = new() { Width = 96, Height = 30 };
    private readonly LogiklikButton _select = new() { Width = 112, Height = 34 };
    private readonly System.Windows.Forms.Timer _searchDelay = new() { Interval = 120 };
    private readonly System.Windows.Forms.Timer _previewDelay = new() { Interval = 120 };
    private readonly System.Windows.Forms.Timer _animation = new() { Interval = 50 };
    private EditorObjectPreview? _objectPreview;
    private EditorChoice[] _visible = [];
    private bool _filtering;
    private string? _previewFile;
    private SoundPlayer? _sound;

    internal EditorChoice? SelectedChoice { get; private set; }
    internal int VisibleCount => _visible.Length;
    private static string T(string source) => QualityOfLifeText.Get(source, System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    internal EditorChoicePicker(ComboBox source, EditorChoice[] entries, string? directory, IReadOnlyCollection<object> recent)
    {
        _source = source; _entries = entries; _directory = directory; _recent = recent;
        Text = T("Choose an entry"); Name = nameof(EditorChoicePicker);
        StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(840, 550); MinimumSize = new Size(650, 430);
        MinimizeBox = false; ShowInTaskbar = false;
        var top = new TableLayoutPanel { Dock = DockStyle.Top, Height = 92, Padding = new Padding(16, 12, 16, 8), ColumnCount = 2, RowCount = 2 };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        top.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); top.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _search.Name = "ChoiceSearch"; _search.PlaceholderText = T("Search by name or several words...");
        var clear = new LogiklikButton { Text = T("Clear"), Dock = DockStyle.Fill };
        clear.Click += (_, _) => { _search.Clear(); _search.Focus(); };
        top.Controls.Add(_search, 0, 0); top.Controls.Add(clear, 1, 0);
        var filters = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 6, 0, 0) };
        _type.Name = "ChoiceTypeFilter"; _sort.Name = "ChoiceSort";
        SearchableChoices.SetEnabled(_type, false); SearchableChoices.SetEnabled(_sort, false);
        _type.Items.Add(T("All file types"));
        _type.Items.AddRange(entries.Select(entry => entry.Extension).Where(extension => extension.Length > 0)
            .Distinct().Order(StringComparer.OrdinalIgnoreCase).Cast<object>().ToArray());
        _type.SelectedIndex = 0;
        _type.Visible = directory != null || entries.Any(entry => EditorResourcePreview.IsImage(entry.Extension) || entry.Extension is ".wav" or ".ogg" or ".mp3" or ".flac" or ".opus");
        _sort.Items.AddRange([T("Original order"), T("Name: A to Z"), T("Name: Z to A")]); _sort.SelectedIndex = 0;
        _recentOnly.Text = T("Recent choices");
        filters.Controls.AddRange([_type, _sort, _recentOnly]); top.Controls.Add(filters, 0, 1); top.SetColumnSpan(filters, 2);
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16, 0, 16, 0), ColumnCount = 2, RowCount = 1 };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 246));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var browser = new Panel { Dock = DockStyle.Fill };
        _list.Columns.Add(new DataGridViewTextBoxColumn { Name = "EntryName", HeaderText = T("Name"), FillWeight = 85, SortMode = DataGridViewColumnSortMode.NotSortable });
        _list.Columns.Add(new DataGridViewTextBoxColumn { Name = "EntryType", HeaderText = T("Type"), FillWeight = 15, SortMode = DataGridViewColumnSortMode.NotSortable });
        _list.RowTemplate.Height = 28;
        browser.Controls.Add(_list); browser.Controls.Add(_count); body.Controls.Add(browser, 0, 0);
        var preview = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 0, 0) };
        var audio = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, WrapContents = false };
        _play.Text = T("Listen"); _stop.Text = T("Stop"); _play.Enabled = false; _stop.Enabled = false;
        audio.Controls.AddRange([_play, _stop]);
        preview.Controls.Add(_image); preview.Controls.Add(_details); preview.Controls.Add(_previewStatus); preview.Controls.Add(audio);
        body.Controls.Add(preview, 1, 0);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 62, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(16, 12, 16, 12) };
        var cancel = new LogiklikButton { Text = T("Cancel"), DialogResult = DialogResult.Cancel, Width = 112, Height = 34 };
        _select.Text = T("Select"); _select.Name = "ChoiceSelect"; footer.Controls.Add(cancel); footer.Controls.Add(_select);
        Controls.Add(body); Controls.Add(top); Controls.Add(footer);
        AcceptButton = _select; CancelButton = cancel;
        _search.TextChanged += (_, _) => { _searchDelay.Stop(); _searchDelay.Start(); };
        _searchDelay.Tick += (_, _) => { _searchDelay.Stop(); ApplyFilter(); };
        _type.SelectedIndexChanged += (_, _) => ApplyFilter(); _sort.SelectedIndexChanged += (_, _) => ApplyFilter();
        _recentOnly.CheckedChanged += (_, _) => ApplyFilter();
        _list.CellValueNeeded += (_, args) =>
        {
            if (args.RowIndex >= 0 && args.RowIndex < _visible.Length)
                args.Value = args.ColumnIndex == 0 ? _visible[args.RowIndex].Text : _visible[args.RowIndex].Extension;
        };
        _list.SelectionChanged += (_, _) =>
        {
            if (_filtering) return;
            StopAudio(); _previewDelay.Stop(); _previewDelay.Start();
        };
        _previewDelay.Tick += (_, _) => { _previewDelay.Stop(); UpdatePreview(); };
        _select.Click += (_, _) => Choose();
        _list.CellDoubleClick += (_, args) => { if (args.RowIndex >= 0) Choose(); };
        _list.KeyDown += (_, args) =>
        {
            if (args.KeyCode == Keys.Enter) { Choose(); args.Handled = args.SuppressKeyPress = true; }
        };
        _search.KeyDown += (_, args) =>
        {
            if (args.KeyCode == Keys.Down && _visible.Length > 0) { _list.Focus(); args.Handled = args.SuppressKeyPress = true; }
        };
        _animation.Tick += (_, _) => { var old = _image.Image; _image.Image = _objectPreview?.Render(); old?.Dispose(); };
        _play.Click += (_, _) => PlayAudio(); _stop.Click += (_, _) => StopAudio();
        Shown += (_, _) => { ApplyFilter(); _search.Focus(); };
        EditorTheme.Apply(this);
        _list.ColumnHeadersDefaultCellStyle.SelectionBackColor = EditorTheme.Surface;
        _list.ColumnHeadersDefaultCellStyle.SelectionForeColor = _list.ColumnHeadersDefaultCellStyle.ForeColor;
        ApplyFilter();
    }

    internal void SetFilter(string query, string? extension = null, int sort = 0, bool recentOnly = false)
    {
        _search.Text = query; _searchDelay.Stop(); _sort.SelectedIndex = sort;
        _type.SelectedIndex = extension == null ? 0 : _type.FindStringExact(extension);
        _recentOnly.Checked = recentOnly; ApplyFilter();
    }

    private void ApplyFilter()
    {
        _searchDelay.Stop(); _previewDelay.Stop(); StopAudio();
        var previous = _list.CurrentCell?.RowIndex is int row && row >= 0 && row < _visible.Length ? _visible[row].Index : _source.SelectedIndex;
        _filtering = true;
        try
        {
            _visible = SearchableChoices.Filter(_entries, _search.Text, _type.SelectedIndex > 0 ? _type.Text : null, _sort.SelectedIndex, _recentOnly.Checked, _recent);
            _list.CurrentCell = null; _list.RowCount = _visible.Length;
            if (_visible.Length > 0)
            {
                var index = Array.FindIndex(_visible, entry => entry.Index == previous);
                _list.CurrentCell = _list[0, Math.Max(0, index)];
            }
            _select.Enabled = _visible.Length > 0;
            _count.Text = _visible.Length == 0 ? T("No matching entries.")
                : string.Format(T("{0} of {1} entries"), _visible.Length, _entries.Length);
            _list.Invalidate();
        }
        finally { _filtering = false; }
        _previewDelay.Start();
    }

    internal void Choose()
    {
        if (_list.CurrentCell?.RowIndex is not int row || row < 0 || row >= _visible.Length) return;
        SelectedChoice = _visible[row]; DialogResult = DialogResult.OK;
    }

    private void UpdatePreview()
    {
        _animation.Stop(); _objectPreview?.Dispose(); _objectPreview = null;
        StopAudio(); _image.Image?.Dispose(); _image.Image = null; _previewFile = null; _play.Enabled = false;
        if (_list.CurrentCell?.RowIndex is not int row || row < 0 || row >= _visible.Length)
        { _details.Text = string.Empty; _previewStatus.Text = T("Select an entry to preview it."); return; }
        var choice = _visible[row]; _details.Text = choice.Text;
        if (choice.Descriptor is { } descriptor)
        {
            _objectPreview = EditorObjectPreview.Create(descriptor);
            _image.Image = _objectPreview.Render();
            _previewStatus.Text = _image.Image == null ? T("This entry has no visual resource configured.") : descriptor.Name;
            if (_objectPreview.Animated) _animation.Start();
            return;
        }
        if (_directory != null && Path.GetFileName(_directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) == "entities")
        {
            _objectPreview = EditorObjectPreview.EntityFile(choice.Text);
            _image.Image = _objectPreview.Render();
            _previewStatus.Text = _image.Image == null ? T("The selected resource is unavailable.") : choice.Text;
            if (_objectPreview.Animated) _animation.Start();
            return;
        }
        _previewFile = EditorResourcePreview.ResolveFile(choice.Text, _directory);
        _previewStatus.Text = T("Preview unavailable. You can still select this entry.");
        if (_previewFile == null) return;
        if (EditorResourcePreview.IsImage(choice.Extension))
        {
            _image.Image = EditorResourcePreview.LoadImage(_previewFile);
            if (_image.Image != null) _previewStatus.Text = $"{_image.Image.Width} × {_image.Image.Height}";
        }
        else if (choice.Extension == ".wav")
        { _play.Enabled = true; _previewStatus.Text = T("Listen before selecting this sound."); }
    }

    private void PlayAudio()
    {
        StopAudio(); if (_previewFile == null) return;
        var player = new SoundPlayer(_previewFile); _sound = player; _stop.Enabled = true;
        player.LoadCompleted += (_, args) =>
        {
            if (IsDisposed || !ReferenceEquals(_sound, player)) return;
            try
            {
                if (args.Error != null) throw new InvalidOperationException("Audio load failed.", args.Error);
                player.Play();
            }
            catch (Exception error) when (error is IOException or InvalidOperationException or TimeoutException)
            { _previewStatus.Text = T("The selected resource is unavailable."); StopAudio(); }
        };
        try { player.LoadAsync(); }
        catch (Exception error) when (error is IOException or InvalidOperationException or TimeoutException)
        { _previewStatus.Text = T("The selected resource is unavailable."); StopAudio(); }
    }

    private void StopAudio()
    {
        var player = _sound; _sound = null;
        if (player != null) { player.Stop(); player.Dispose(); }
        _stop.Enabled = false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animation.Dispose(); _objectPreview?.Dispose(); _objectPreview = null;
            _searchDelay.Dispose(); _previewDelay.Dispose(); StopAudio();
            _image.Image?.Dispose(); _image.Image = null;
        }
        base.Dispose(disposing);
    }

    private sealed class PreviewCanvas : PictureBox
    {
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            if (Image == null) return;
            using var light = new SolidBrush(System.Drawing.Color.FromArgb(235, 239, 243));
            using var dark = new SolidBrush(System.Drawing.Color.FromArgb(208, 214, 220));
            e.Graphics.FillRectangle(light, ClientRectangle);
            for (var y = 0; y < Height; y += 12)
                for (var x = 0; x < Width; x += 12)
                    if ((x / 12 + y / 12) % 2 == 0) e.Graphics.FillRectangle(dark, x, y, 12, 12);
        }
    }
}
