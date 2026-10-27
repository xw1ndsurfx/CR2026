using Intersect.Editor.Forms.Styling;
using Intersect.Editor.General;
using Intersect.Editor.Maps;
using Intersect.Framework.Core.GameObjects.Mapping.Tilesets;
using Intersect.Framework.Core.GameObjects.Maps;

namespace Intersect.Editor.Forms.DockingElements;

internal sealed partial class FrmSmartMapBuilder
{
    private (SmartTile Start, SmartTile Reference, SmartTile Appearance, string Name)? _pendingConnection;

    private void CreateWaterConnection()
    {
        if (_working == null || CurrentTile() is not { } selected) { _status.Text = T("WaterConnectionRequired"); return; }
        var reference = SmartTile.From(GroundShoreVariant.ConnectionTile(_working.Tile.Native));
        if (reference.Autotile is not (1 or 3 or 6 or 7) || selected.Autotile != reference.Autotile)
        { _status.Text = T("WaterConnectionRequired"); return; }
        Bitmap? Load(SmartTile tile) => TilesetDescriptor.Get(tile.TilesetId) is { } descriptor
            ? EditorResourcePreview.LoadImage(EditorResourcePreview.ResolveFile(descriptor.Name, "resources/tilesets"), int.MaxValue) : null;
        using var source = Load(reference); using var appearance = Load(selected);
        if (source == null || appearance == null) { _status.Text = T("InvalidResource"); return; }
        using var image = SmartWaterConnection.Create(source, reference, appearance, selected);
        using var bytes = new MemoryStream(); image.Save(bytes, System.Drawing.Imaging.ImageFormat.Png);
        var name = GroundShoreVariant.FileName(reference.Native, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes.ToArray()))[..20].ToLowerInvariant());
        var target = Path.Combine("resources", "tilesets", name);
        if (!File.Exists(target))
        {
            var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllBytes(temporary, bytes.ToArray()); File.Move(temporary, target); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        _pendingConnection = (_working.Tile, reference, selected, name);
        if (!ResolveWaterConnection())
        { Intersect.Editor.Networking.PacketSender.SendNewTilesets(new[] { name }); _status.Text = T("SoftWaterRegistering"); }
    }

    private bool ResolveWaterConnection()
    {
        if (_pendingConnection is not { } pending) return false;
        if (_working?.Tile != pending.Start) { _pendingConnection = null; return false; }
        var descriptor = TilesetDescriptor.Lookup.Values.OfType<TilesetDescriptor>().FirstOrDefault(t => t.Name == pending.Name);
        if (descriptor == null) return false;
        _working = new SmartPreset { Name = _working.Name, Tile = pending.Reference with { TilesetId = descriptor.Id },
            Layer = Options.Instance.Map.Layers.All[0], ConnectedWaterSource = pending.Appearance };
        _pendingConnection = null; _pendingGround = null; _pendingWater = null; _selected = null;
        _softWater.Checked = false; _groundShore.Checked = false; _category.SelectedIndex = 0;
        _destination.SelectedItem = _working.Layer; _shape.SelectedIndex = (int)SmartShape.ConnectWater;
        _enabled.Checked = true; Globals.CurrentTool = EditingTool.Brush;
        RefreshRules(); LoadSample(); InvalidatePreview(); _status.Text = T("WaterConnectionReady");
        return true;
    }
}
