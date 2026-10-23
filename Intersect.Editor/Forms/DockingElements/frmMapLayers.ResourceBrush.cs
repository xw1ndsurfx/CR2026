using Intersect.Editor.General;
using Intersect.Editor.Maps;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;
using Intersect.Framework.Core.GameObjects.Resources;
namespace Intersect.Editor.Forms.DockingElements;
public partial class FrmMapLayers
{
    private readonly CheckBox _resourceBrush = new() { Text = "Resource brush", AutoSize = true };
    private readonly NumericUpDown _resourceRadius = new() { Minimum = 0, Maximum = 8, Value = 2, Width = 52 };
    private readonly NumericUpDown _resourceDensity = new() { Minimum = 1, Maximum = 100, Value = 25, Width = 52 };
    private readonly HashSet<(Guid Map, int X, int Y)> _paintedResources = [];
    private FlowLayoutPanel? _resourceBrushRow;
    private void InitializeResourceBrush()
    {
        pnlAttributes.AutoScroll=true;
        var location = new System.Drawing.Point(8, Options.Instance.Map.ZDimensionVisible ? 116 : 70);
        if (_resourceBrushRow != null)
        {
            _resourceBrushRow.Location = location;
            grpResource.Height = _resourceBrushRow.Bottom + 8;
            return;
        }
        var row = new FlowLayoutPanel { Location = location, Size = new Size(235, 102), BackColor = grpResource.BackColor, ForeColor = grpResource.ForeColor };
        row.Controls.Add(_resourceBrush); row.SetFlowBreak(_resourceBrush, true);
        row.Controls.AddRange([new Label { Text = "Radius (tiles)", AutoSize = true }, _resourceRadius]); row.SetFlowBreak(_resourceRadius, true);
        row.Controls.AddRange([new Label { Text = "Density (%)", AutoSize = true }, _resourceDensity]);
        grpResource.Controls.Add(row); grpResource.Height = row.Bottom + 8;
        _resourceBrushRow = row;
        new ToolTip(components).SetToolTip(_resourceBrush, "Paint scattered resources by clicking or dragging. Existing attributes and event tiles are preserved. Undo restores the stroke.");
    }
    public void ActivateResourceBrush()
    {
        InitializeResourceBrush();
        btnAttributeHeader_Click(this,EventArgs.Empty);
        var types=Enum.GetValues<MapAttributeType>().Where(type=>type!=MapAttributeType.Walkable).ToArray();
        cmbAttributeType.SelectedIndex=Array.IndexOf(types,MapAttributeType.Resource);
        _resourceBrush.Checked=true;
        Globals.CurrentTool=Intersect.Editor.General.EditingTool.Brush;
        pnlAttributes.ScrollControlIntoView(_resourceBrushRow!);
    }
    public void BeginResourceStroke() => _paintedResources.Clear();
    public void PaintAttribute(MapInstance map, int x, int y)
    {
        if (!_resourceBrush.Checked || CreateAttribute() is not MapResourceAttribute resource) { PlaceAttribute(map, x, y); return; }
        if (resource.ResourceId == Guid.Empty || ResourceDescriptor.Get(resource.ResourceId) == null) return;
        foreach (var cell in ResourceBrush.Cells(x, y, (int)_resourceRadius.Value,
            Options.Instance.Map.MapWidth, Options.Instance.Map.MapHeight))
        {
            if (!_paintedResources.Add((map.Id, cell.X, cell.Y))) continue;
            if ((cell.X != x || cell.Y != y) && Random.Shared.Next(100) >= _resourceDensity.Value) continue;
            if (map.Attributes[cell.X, cell.Y] is { Type: not MapAttributeType.Walkable } || map.FindEventAt(cell.X, cell.Y) != null) continue;
            PlaceAttribute(map, cell.X, cell.Y, CreateResourceAttribute());
        }
    }
}
internal static class ResourceBrush
{
    internal static IEnumerable<(int X, int Y)> Cells(int x, int y, int radius, int width, int height)
    {
        radius = Math.Clamp(radius, 0, 8);
        for (var dy = -radius; dy <= radius; dy++) for (var dx = -radius; dx <= radius; dx++)
        {
            if (dx * dx + dy * dy > radius * radius || x + dx < 0 || y + dy < 0 || x + dx >= width || y + dy >= height) continue;
            yield return (x + dx, y + dy);
        }
    }
}
