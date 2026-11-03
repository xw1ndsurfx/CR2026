using Intersect.Editor.General;
using Intersect.Editor.Maps;

namespace Intersect.Editor.Forms.DockingElements;

public partial class FrmMapEditor
{
    private MapInstance? _dockLandingSelectionMap;
    private Func<int, int, bool>? _dockLandingSelected;
    private Action? _dockLandingSelectionCancelled;
    private bool _dockLandingConsumeRelease;
    private bool _dockLandingJustSelected;
    internal bool ChoosingDockLanding => _dockLandingSelected != null;

    internal void BeginDockLandingSelection(Func<int, int, bool> selected, Action cancelled)
    {
        CancelDockLandingSelection(); CancelSmartStroke();
        _dockLandingSelectionMap = Globals.CurrentMap;
        _dockLandingSelected = selected; _dockLandingSelectionCancelled = cancelled;
        Globals.MouseButton = -1;
    }

    internal void CancelDockLandingSelection()
    {
        var cancelled = _dockLandingSelectionCancelled;
        _dockLandingSelectionMap = null; _dockLandingSelected = null; _dockLandingSelectionCancelled = null;
        _dockLandingJustSelected = false;
        cancelled?.Invoke();
    }

    private bool PickDockLanding(MouseEventArgs e)
    {
        if (_dockLandingSelected == null)
        {
            var consumeDoubleClick = _dockLandingJustSelected && e.Clicks > 1;
            _dockLandingJustSelected = false;
            if (consumeDoubleClick) _dockLandingConsumeRelease = true;
            return consumeDoubleClick;
        }
        _dockLandingConsumeRelease = true;
        if (_dockLandingSelectionMap != Globals.CurrentMap || e.Button == MouseButtons.Right)
        { CancelDockLandingSelection(); return true; }
        if (e.Button != MouseButtons.Left) return true;
        var x = (int)Math.Floor((e.X - Core.Graphics.CurrentView.Left) / (double)Options.Instance.Map.TileWidth);
        var y = (int)Math.Floor((e.Y - Core.Graphics.CurrentView.Top) / (double)Options.Instance.Map.TileHeight);
        if (x < 0 || y < 0 || x >= Options.Instance.Map.MapWidth || y >= Options.Instance.Map.MapHeight) return true;
        var selected = _dockLandingSelected;
        if (!selected(x, y)) return true;
        _dockLandingSelected = null; _dockLandingSelectionCancelled = null; _dockLandingSelectionMap = null;
        _dockLandingJustSelected = true;
        return true;
    }
}
