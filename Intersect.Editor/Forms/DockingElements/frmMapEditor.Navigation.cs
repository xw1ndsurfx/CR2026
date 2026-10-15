using Intersect.Editor.General;
using Intersect.Editor.Maps;
using Graphics = Intersect.Editor.Core.Graphics;

namespace Intersect.Editor.Forms.DockingElements;

public partial class FrmMapEditor
{
    // PictureBox normally refuses keyboard focus, so Space cannot reach KeyPreview.
    private sealed class MapEditorCanvas : PictureBox
    {
        public MapEditorCanvas() => SetStyle(ControlStyles.Selectable, true);
        protected override bool IsInputKey(Keys keyData) =>
            (keyData & Keys.KeyCode) == Keys.Space || base.IsInputKey(keyData);
    }

    private bool _spaceHeld;
    private MouseButtons _panButton;
    private MouseButtons _panReleaseButton;
    private System.Drawing.Point _panStart;
    private Rectangle _panStartView;
    private Cursor? _panPreviousCursor;

    private void InitializeMapNavigation()
    {
        var hint = new ToolTip(components ??= new System.ComponentModel.Container());
        hint.SetToolTip(picMap, "Zoom: mouse wheel | Move: hold middle mouse, or Space + left mouse");
        picMap.MouseCaptureChanged += (_, _) => { if (!picMap.Capture) EndMapPan(); };
        Deactivate += (_, _) => { _spaceHeld = false; EndMapPan(); };
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space && picMap.ContainsFocus && Globals.MouseButton < 0)
        {
            _spaceHeld = true;
            e.Handled = e.SuppressKeyPress = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space && _spaceHeld)
        {
            _spaceHeld = false;
            e.Handled = e.SuppressKeyPress = true;
        }
        base.OnKeyUp(e);
    }

    private bool BeginMapPan(MouseEventArgs e)
    {
        if (_panButton != MouseButtons.None) return true;
        _panReleaseButton = MouseButtons.None;
        if (!Options.IsLoaded || Globals.EditingLight != null || Globals.MouseButton >= 0) return false;
        if (e.Button != MouseButtons.Middle && !(e.Button == MouseButtons.Left && _spaceHeld)) return false;
        _panButton = e.Button;
        _panReleaseButton = e.Button;
        _panStart = e.Location;
        _panStartView = Graphics.CurrentView;
        _panPreviousCursor = Cursor;
        Cursor = Cursors.Hand;
        picMap.Focus();
        picMap.Capture = true;
        tooltipMapAttribute.Hide();
        return true;
    }

    private bool MoveMapPan(System.Drawing.Point cursor)
    {
        if (_panButton == MouseButtons.None) return false;
        lock (Graphics.GraphicsLock)
        {
            Graphics.CurrentView = MapEditorViewport.PanFrom(_panStartView, _panStart, cursor, Graphics.MapEditorZoom);
            Graphics.TilePreviewUpdated = true;
        }
        Intersect.Editor.Core.Main.DrawFrame();
        return true;
    }

    private void EndMapPan()
    {
        if (_panButton == MouseButtons.None) return;
        _panButton = MouseButtons.None;
        Cursor = _panPreviousCursor ?? Cursors.Default;
        _panPreviousCursor = null;
        picMap.Capture = false;
    }
}
