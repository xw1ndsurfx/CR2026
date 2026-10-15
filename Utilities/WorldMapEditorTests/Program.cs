using System.Reflection;
using Intersect.Editor.Forms.Editors;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.WorldMaps;
using Intersect.Network.Packets.Editor;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var first = new AnimationDescriptor(Guid.NewGuid()) { Name = "Potion shop" };
        var second = new AnimationDescriptor(Guid.NewGuid()) { Name = "Dungeon" };
        AnimationDescriptor.Lookup[first.Id] = first; AnimationDescriptor.Lookup[second.Id] = second;
        var legend = new WorldMapLegend { Entries = [new() { AnimationId = first.Id, Label = "Buy potions" }] };
        using var editor = new FrmWorldMapLegend(legend);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var grid = (DataGridView)typeof(FrmWorldMapLegend).GetField("_entries", flags)!.GetValue(editor)!;
        using (var preview = new System.Drawing.Bitmap(editor.Width, editor.Height))
            editor.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, preview.Width, preview.Height));
        Check(grid.Rows[0].Cells[0].FormattedValue?.ToString() == first.Name, "Animation dropdown must bind and display the animation name");
        Check(grid.Rows[0].Cells[0].Value is Guid id && id == first.Id, "Existing animation selection");
        grid.Rows.Add(second.Id, "Dangerous dungeon");
        grid.CurrentCell = grid.Rows[1].Cells[0];
        typeof(FrmWorldMapLegend).GetMethod("MoveRow", flags)!.Invoke(editor, [-1]);
        Check((Guid)grid.Rows[0].Cells[0].Value! == second.Id, "Reorder must preserve animation and label");
        typeof(FrmWorldMapLegend).GetMethod("Save", flags)!.Invoke(editor, null);
        var packet = Intersect.Editor.Networking.Network.Sent as SaveWorldMapLegendPacket
            ?? throw new InvalidOperationException("Editor did not send the save packet");
        var saved = WorldMapLegend.FromJson(packet.LegendJson);
        Check(saved.Entries.Count == 2 && saved.Entries[0].AnimationId == second.Id && saved.Entries[0].Label == "Dangerous dungeon",
            "Save must preserve selected animations, labels and order");
        Check(legend.Entries.Count == 1, "Unsaved editor rows must not mutate the input configuration");
        grid.Rows.RemoveAt(0);
        typeof(FrmWorldMapLegend).GetMethod("Save", flags)!.Invoke(editor, null);
        Check(WorldMapLegend.FromJson(((SaveWorldMapLegendPacket)Intersect.Editor.Networking.Network.Sent!).LegendJson).Entries.Count == 1,
            "Removed legend row persisted");
        editor.MarkSaved();
        using var empty = new FrmWorldMapLegend(new WorldMapLegend());
        typeof(FrmWorldMapLegend).GetMethod("Save", flags)!.Invoke(empty, null);
        Check(WorldMapLegend.FromJson(((SaveWorldMapLegendPacket)Intersect.Editor.Networking.Network.Sent!).LegendJson).Entries.Count == 0,
            "An empty legend must be savable");
        System.Console.WriteLine("PASS World Map Editor: animation selection, labels, reorder, removal, save packet, empty legend and isolated draft");
    }

    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("World Map Editor: " + message); }
}

namespace Intersect.Editor.Networking
{
    internal static class Network
    {
        internal static object? Sent;
        internal static void SendPacket(object packet) => Sent = packet;
    }
}
