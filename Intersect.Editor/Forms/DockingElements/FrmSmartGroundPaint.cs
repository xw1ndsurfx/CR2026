using Intersect.Editor.Forms.Styling;
using Intersect.Editor.Maps;
using DrawingColor = System.Drawing.Color;

namespace Intersect.Editor.Forms.DockingElements;

internal sealed class FrmSmartGroundPaint : Form
{
    private readonly SmartGroundPaint _paint;
    private readonly PictureBox _canvas = new() { Dock = DockStyle.Fill, Cursor = Cursors.Cross };
    private readonly CheckBox _grid = new() { Checked = true, AutoSize = true };
    private readonly Button _color = new() { AutoSize = true };
    private DrawingColor _ink = DrawingColor.SaddleBrown;
    private SmartCell? _last;
    private Rectangle _imageBounds;
    internal Bitmap Result => new(_paint.Image);
    internal FrmSmartGroundPaint(Bitmap source)
    {
        _paint = new(source);
        Text = FrmSmartMapBuilder.T("EditGround"); StartPosition = FormStartPosition.CenterParent;
        ClientSize = new(640, 540); MinimumSize = new(520, 460); ShowInTaskbar = false;
        var tools = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 210, Padding = new(8), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        var help = new Label { Text = FrmSmartMapBuilder.T("GroundPaintHelp"), AutoSize = true, MaximumSize = new(190, 0), Margin = new(3, 10, 3, 14) };
        tools.Controls.Add(help);
        _color.Text = FrmSmartMapBuilder.T("GroundColor"); tools.Controls.Add(_color);
        void Add(string key, Action action)
        {
            var button = new Button { Text = FrmSmartMapBuilder.T(key), AutoSize = true, MaximumSize = new(190, 0), Margin = new(3, 5, 3, 5) };
            button.Click += (_, _) => { action(); _canvas.Invalidate(); }; tools.Controls.Add(button);
        }
        Add("GroundTint", () => _paint.Colorize(_ink, false));
        Add("GroundSolid", () => _paint.Colorize(_ink, true));
        Add("GroundUndo", () => _paint.Undo()); Add("GroundReset", () => _paint.Reset());
        _grid.Text = FrmSmartMapBuilder.T("GroundGrid"); tools.Controls.Add(_grid);
        _grid.CheckedChanged += (_, _) => _canvas.Invalidate();
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new(6) };
        var accept = new Button { Text = FrmSmartMapBuilder.T("GroundAccept"), AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = FrmSmartMapBuilder.T("GroundCancel"), AutoSize = true, DialogResult = DialogResult.Cancel };
        footer.Controls.AddRange(new Control[] { accept, cancel }); CancelButton = cancel;
        // Enter is deliberately not an accept shortcut while painting.
        Controls.Add(_canvas); Controls.Add(tools); Controls.Add(footer);
        _color.Click += (_, _) =>
        {
            using var dialog = new ColorDialog { Color = _ink, FullOpen = true };
            if (dialog.ShowDialog(this) == DialogResult.OK) { _ink = dialog.Color; _color.ForeColor = _ink; }
        };
        _canvas.Paint += (_, args) =>
        {
            int scale = Math.Max(1, Math.Min((_canvas.Width - 20) / _paint.Image.Width, (_canvas.Height - 20) / _paint.Image.Height));
            var size = new Size(_paint.Image.Width * scale, _paint.Image.Height * scale);
            _imageBounds = new((_canvas.Width - size.Width) / 2, (_canvas.Height - size.Height) / 2, size.Width, size.Height);
            args.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            args.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            args.Graphics.DrawImage(_paint.Image, _imageBounds);
            if (_grid.Checked && scale >= 6)
            {
                using var pen = new Pen(DrawingColor.FromArgb(65, DrawingColor.Black));
                for (var x = 0; x <= _paint.Image.Width; x++) args.Graphics.DrawLine(pen, _imageBounds.Left + x * scale, _imageBounds.Top, _imageBounds.Left + x * scale, _imageBounds.Bottom);
                for (var y = 0; y <= _paint.Image.Height; y++) args.Graphics.DrawLine(pen, _imageBounds.Left, _imageBounds.Top + y * scale, _imageBounds.Right, _imageBounds.Top + y * scale);
            }
        };
        SmartCell Cell(MouseEventArgs e) => new((e.X - _imageBounds.X) * _paint.Image.Width / Math.Max(1, _imageBounds.Width), (e.Y - _imageBounds.Y) * _paint.Image.Height / Math.Max(1, _imageBounds.Height));
        _canvas.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left || !_imageBounds.Contains(e.Location)) return;
            _paint.Checkpoint(); _last = Cell(e); _canvas.Capture = true; _paint.Stroke(_last.Value, _last.Value, _ink); _canvas.Invalidate();
        };
        _canvas.MouseMove += (_, e) =>
        {
            if (_last is not { } last) return;
            var cell = Cell(e); _paint.Stroke(last, cell, _ink); _last = cell; _canvas.Invalidate();
        };
        _canvas.MouseUp += (_, _) => { _last = null; _canvas.Capture = false; };
        _canvas.MouseCaptureChanged += (_, _) => { if (!_canvas.Capture) _last = null; };
        EditorTheme.Apply(this);
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.Z)) { _paint.Undo(); _canvas.Invalidate(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
    protected override void Dispose(bool disposing) { if (disposing) _paint.Dispose(); base.Dispose(disposing); }
}
