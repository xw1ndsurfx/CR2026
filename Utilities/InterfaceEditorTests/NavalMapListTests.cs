using System.Reflection;
using Intersect.Editor.Forms.Controls;
using Intersect.Editor.Forms.DockingElements;
using Intersect.Framework.Core.GameObjects.Maps.MapList;

internal static class NavalMapListTests
{
    internal static void Run()
    {
        using var view = new FrmMapList();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var menu = (ToolStripMenuItem)typeof(FrmMapList).GetField("_prepareNaval", flags)!.GetValue(view)!;
        var tree = view.Controls.OfType<MapTreeList>().Single().list;
        var opening = typeof(FrmMapList).GetMethod("contextMenuStrip1_Opening", flags)!;
        void Open() => opening.Invoke(view, [view, new System.ComponentModel.CancelEventArgs()]);
        Open();
        if (menu.Available) throw new Exception("Naval setup must be hidden without a selected map");
        var folder = tree.Nodes.Add("Ocean"); folder.Tag = new MapListFolder { Name = "Ocean" };
        tree.SelectedNode = folder; Open();
        if (menu.Available) throw new Exception("A folder cannot be configured as a ship map");
        var map = folder.Nodes.Add("Test sea"); map.Tag = new MapListMap { MapId = Guid.NewGuid(), Name = "Test sea" };
        tree.SelectedNode = map; Open();
        if (!menu.Available || string.IsNullOrWhiteSpace(menu.Text)) throw new Exception("Saved maps must expose the translated naval setup command");
        using var preview = new Bitmap(view.Width, view.Height);
        view.DrawToBitmap(preview, new Rectangle(0, 0, view.Width, view.Height));
        System.Console.WriteLine("PASS naval map list: translated setup command, map/folder visibility and actual WinForms rendering");
    }
}
