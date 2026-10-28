using Intersect.Editor.General;
using Intersect.Editor.Maps;
using WeifenLuo.WinFormsUI.Docking;

namespace Intersect.Editor.Forms.DockingElements;

public partial class FrmMapEditor
{
    private FrmSmartMapBuilder? _smartBuilder;
    private MapInstance? _smartMap;
    private SmartPreset? _smartPreset;
    private SmartShape _smartShape;
    private SmartCell _smartStart;
    private SmartCell _smartEnd;
    private readonly HashSet<SmartCell> _smartPath = [];
    private List<SmartEdit>? _smartEdits;
    private bool _smartPreserve;
    private bool _smartErase;
    private int _smartSeed;
    internal bool SmartBuilderActive => _smartBuilder?.EnabledForMap == true;

    public void ShowSmartMapBuilder()
    {
        if (_smartBuilder is not { IsDisposed: false })
        {
            _smartBuilder = new FrmSmartMapBuilder();
            _smartBuilder.VisibleChanged += (_, _) => { if (!_smartBuilder.Visible) CancelSmartStroke(); };
            Disposed += (_, _) => _smartBuilder.Dispose();
        }
        if (DockPanel == null) return;
        _smartBuilder.Show(DockPanel, DockState.DockRight); _smartBuilder.Activate();
    }
    private SmartCell SmartMouseCell(MouseEventArgs e) => new(
        (int)Math.Floor((e.X - Core.Graphics.CurrentView.Left) / (double)Options.Instance.Map.TileWidth),
        (int)Math.Floor((e.Y - Core.Graphics.CurrentView.Top) / (double)Options.Instance.Map.TileHeight));
    private static bool SmartInBounds(SmartCell cell) => cell.X >= 0 && cell.Y >= 0 && cell.X < Options.Instance.Map.MapWidth && cell.Y < Options.Instance.Map.MapHeight;
    private bool BeginSmartStroke(MouseEventArgs e)
    {
        if (!SmartBuilderActive || e.Button is not (MouseButtons.Left or MouseButtons.Right)) return false;
        CancelSmartStroke();
        var cell = SmartMouseCell(e);
        if (Globals.EditingLight != null || Globals.Dragging || Globals.CurrentMap == null || !SmartInBounds(cell) || _smartBuilder!.Settings(true) is not { } preset) return true;
        _smartMap = Globals.CurrentMap; _smartPreset = preset; _smartShape = _smartBuilder.Shape;
        _smartStart = _smartEnd = cell; _smartErase = e.Button == MouseButtons.Right; _smartPreserve = _smartBuilder.Preserve;
        _smartSeed = Random.Shared.Next(); _smartEdits = null;
        Globals.CurTileX = cell.X; Globals.CurTileY = cell.Y; Globals.MouseButton = -1;
        _smartPath.UnionWith(SmartBrush.Area(cell, cell, SmartShape.Brush, preset.Radius, preset.Round));
        picMap.Capture = true; Core.Graphics.TilePreviewUpdated = true;
        return true;
    }
    private bool MoveSmartStroke()
    {
        if (_smartMap == null) return SmartBuilderActive;
        if (_smartMap != Globals.CurrentMap || !SmartBuilderActive) { CancelSmartStroke(); return true; }
        var cell = new SmartCell(Globals.CurTileX, Globals.CurTileY);
        if (cell != _smartEnd)
        {
            if (_smartShape == SmartShape.Brush) _smartPath.UnionWith(SmartBrush.Area(_smartEnd, cell, SmartShape.Brush, _smartPreset!.Radius, _smartPreset.Round));
            _smartEnd = cell; _smartEdits = null; Core.Graphics.TilePreviewUpdated = true;
        }
        return true;
    }
    private IEnumerable<SmartCell> SmartCells(MapInstance map, SmartPreset preset, SmartShape shape, SmartCell start, SmartCell end)
    {
        if (shape is SmartShape.Fill or SmartShape.Replace) return SmartBrush.Flood(map.Layers[preset.PaintLayer], start, shape == SmartShape.Replace);
        if (shape == SmartShape.Stamp) return new[] { end };
        if (_smartMap != null && shape == SmartShape.Brush) return _smartPath;
        return SmartBrush.Area(start, end, shape, preset.Radius, preset.Round);
    }
    private List<SmartEdit> SmartPlan(MapInstance map, SmartPreset preset, SmartShape shape, SmartCell start, SmartCell end, bool preserve, bool erase, int seed) =>
        shape == SmartShape.ConnectWater ? SmartBrush.ConnectWater(map, preset, start, erase)
        : SmartBrush.Plan(map, preset, SmartCells(map, preset, shape, start, end), preserve, erase, seed, shape == SmartShape.Stamp);

    internal void PreviewSmartBuilder(MapInstance preview)
    {
        if (_dockLandingSelected != null || _dockLandingConsumeRelease) return;
        if (_smartMap != null && _smartMap == Globals.CurrentMap && _smartPreset != null)
        {
            _smartEdits ??= SmartPlan(_smartMap, _smartPreset, _smartShape, _smartStart, _smartEnd, _smartPreserve, _smartErase, _smartSeed);
            SmartBrush.Apply(preview, _smartEdits, false);
        }
        else if (SmartBuilderActive && _smartBuilder!.Settings() is { } preset && Globals.CurrentMap is { } map && map.Layers.ContainsKey(preset.Layer))
        {
            var cell = new SmartCell(Globals.CurTileX, Globals.CurTileY);
            SmartBrush.Apply(preview, SmartPlan(map, preset, _smartBuilder.Shape, cell, cell, _smartBuilder.Preserve, false, 0), false);
        }
    }
    private bool EndSmartStroke(MouseEventArgs e)
    {
        if (_smartMap == null) return SmartBuilderActive && e.Button is MouseButtons.Left or MouseButtons.Right;
        if (e.Button != (_smartErase ? MouseButtons.Right : MouseButtons.Left)) return true;
        var map = _smartMap;
        if (map != Globals.CurrentMap || _smartPreset == null || !SmartBuilderActive) { CancelSmartStroke(); return true; }
        var cell = SmartMouseCell(LogicalMapMouse(e));
        if (SmartInBounds(cell))
        {
            Globals.CurTileX = cell.X; Globals.CurTileY = cell.Y; MoveSmartStroke();
        }
        var edits = _smartEdits ?? SmartPlan(map, _smartPreset, _smartShape, _smartStart, _smartEnd, _smartPreserve, _smartErase, _smartSeed);
        if (edits.Count > 0)
        {
            // Take a fresh pre-action snapshot, never a stale snapshot from a different tool.
            var before = map.SaveInternal();
            SmartBrush.Apply(map, edits, true);
            MapUndoStates.Add(before); MapRedoStates.Clear(); CurrentMapState = map.SaveInternal(); mMapChanged = false;
            _smartBuilder!.RecordUse();
        }
        _smartBuilder!.Report(edits.Count); CancelSmartStroke();
        return true;
    }
    internal void CancelSmartStroke()
    {
        _smartMap = null; _smartPreset = null; _smartEdits = null; _smartPath.Clear();
        picMap.Capture = false; Core.Graphics.TilePreviewUpdated = true;
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_dockLandingSelected != null && keyData == Keys.Escape) { CancelDockLandingSelection(); return true; }
        if (_smartMap != null && keyData == Keys.Escape) { CancelSmartStroke(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
