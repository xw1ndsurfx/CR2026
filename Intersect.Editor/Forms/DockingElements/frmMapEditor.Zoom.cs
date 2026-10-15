using Intersect.Editor.Core;
using Intersect.Editor.General;
using Intersect.Editor.Maps;
using Graphics = Intersect.Editor.Core.Graphics;

namespace Intersect.Editor.Forms.DockingElements;

public partial class FrmMapEditor
{
    private void MapContainerMouseWheel(object? sender, MouseEventArgs e)
    {
        if (e is HandledMouseEventArgs { Handled: true }) return;
        var cursor = picMap.PointToClient(pnlMapContainer.PointToScreen(e.Location));
        if (!picMap.ClientRectangle.Contains(cursor)) return;
        MapEditorMouseWheel(picMap, new MouseEventArgs(e.Button, e.Clicks, cursor.X, cursor.Y, e.Delta));
        if (e is HandledMouseEventArgs handled) handled.Handled = true;
    }

    private static MouseEventArgs LogicalMapMouse(MouseEventArgs e) => new(
        e.Button, e.Clicks, (int)Math.Floor(e.X / Graphics.MapEditorZoom),
        (int)Math.Floor(e.Y / Graphics.MapEditorZoom), e.Delta);

    private void ResetMapViewport()
    {
        var width = (int)Math.Ceiling(picMap.Width / Graphics.MapEditorZoom);
        var height = (int)Math.Ceiling(picMap.Height / Graphics.MapEditorZoom);
        Graphics.CurrentView = new Rectangle(
            (width - Options.Instance.Map.MapWidth * Options.Instance.Map.TileWidth) / 2,
            (height - Options.Instance.Map.MapHeight * Options.Instance.Map.TileHeight) / 2, width, height);
    }

    private void MapEditorMouseWheel(object? sender, MouseEventArgs e)
    {
        if (!Options.IsLoaded || e.Delta == 0 || Globals.EditingLight != null) return;
        lock (Graphics.GraphicsLock)
        {
            var next = MapEditorViewport.ZoomAt(Graphics.MapEditorZoom, Graphics.CurrentView, e.Location, picMap.Size, e.Delta);
            if (next.Zoom == Graphics.MapEditorZoom) return;
            Graphics.EndSpriteBatch();
            Graphics.MapEditorZoom = next.Zoom;
            Graphics.CurrentView = next.View;
            Globals.MouseX = (int)(e.X / next.Zoom); Globals.MouseY = (int)(e.Y / next.Zoom);
            Graphics.TilePreviewUpdated = true;
        }
        if (e is HandledMouseEventArgs handled) handled.Handled = true;
        picMap_MouseMove(sender!, new MouseEventArgs(MouseButtons.None, 0, e.X, e.Y, 0));
        Main.DrawFrame();
    }
}
