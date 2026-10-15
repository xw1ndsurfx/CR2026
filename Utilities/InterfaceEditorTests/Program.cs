using System.Reflection;
using Intersect;
using Intersect.Editor.Forms.Editors.Events;
using Intersect.Editor.Forms.DockingElements;
using Intersect.Editor.Maps;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Maps;
using MessagePack;
using Intersect.Network.Packets.Server;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        try { Run(); } catch (Exception exception) { System.Console.Error.WriteLine(exception); Environment.Exit(1); }
    }
    private static void Run()
    {
        typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, new Options());
        var paddedBounds = GraphicBounds.Read(128, 128, (x, y) => x >= 48 && x < 80 && y >= 30 && y < 78);
        var anchored = EventGraphicPlacement.VisibleTopLeft(320, 640, paddedBounds, 32, 32);
        Check(anchored.X + paddedBounds.X + paddedBounds.Width / 2f == 336 &&
            anchored.Y + paddedBounds.Y + paddedBounds.Height == 672, "Editor graphics must sit visibly on the E tile despite transparent padding");
        var empty = GraphicBounds.Read(32, 32, (_, _) => false);
        Check(empty == new GraphicBounds(0, 0, 32, 32), "Empty graphic fallback must keep a finite tile anchor");
        using var editor = new FrmEvent(new MapDescriptor(Guid.NewGuid()));
        editor.CurrentPage = new EventPage { WorldMapOffsetX = 48, WorldMapOffsetY = -16 };
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(FrmEvent).GetMethod("LoadWorldMapOffsets", flags)!.Invoke(editor, null);
        var x = (NumericUpDown)typeof(FrmEvent).GetField("_worldMapX", flags)!.GetValue(editor)!;
        var y = (NumericUpDown)typeof(FrmEvent).GetField("_worldMapY", flags)!.GetValue(editor)!;
        Check(x.Value == 48 && y.Value == -16, "Page offsets must load");
        x.Value = -64; y.Value = 32;
        Check(editor.CurrentPage.WorldMapOffsetX == -64 && editor.CurrentPage.WorldMapOffsetY == 32, "Page offsets must save");
        editor.Size = new Size(1500, 950); editor.PerformLayout();
        var split = editor.Controls.OfType<SplitContainer>().Single();
        Check(split.Width > 1400 && split.Height > 700 && split.Panel2.Width > 800, "Event command area must grow with the window");
        var save = (Control)typeof(FrmEvent).GetField("btnSave", flags)!.GetValue(editor)!;
        Check(save.Parent!.Bottom <= editor.ClientSize.Height && save.Parent.Height >= save.Bottom, "Save controls remain accessible");
        editor.ShowInTaskbar = false; editor.Opacity = 0; editor.StartPosition = FormStartPosition.Manual;
        editor.Location = new System.Drawing.Point(-30000, -30000); editor.Show(); Application.DoEvents();
        using (var bitmap = new Bitmap(editor.Width, editor.Height))
        {
            editor.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
            if (Environment.GetEnvironmentVariable("CR2026_EDITOR_PREVIEW") is { Length: > 0 } preview) bitmap.Save(preview);
        }
        var toggle = typeof(FrmEvent).GetMethod("ToggleEventFullscreen", flags)!;
        toggle.Invoke(editor, null); Check(editor.FormBorderStyle == FormBorderStyle.None, "Full screen mode");
        toggle.Invoke(editor, null); Check(editor.FormBorderStyle == FormBorderStyle.Sizable, "Restore resizable window");
        var root = new TreeNode("Effects"); root.Nodes.Add(new TreeNode("Play animation") { Tag = 42 });
        root.Nodes.Add(new TreeNode("Play sound") { Tag = 43 });
        var filtered = (TreeNode)typeof(FrmEvent).GetMethod("FilterCommands", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [root, "animation"])!;
        Check(filtered.Nodes.Count == 1 && (int)filtered.Nodes[0].Tag! == 42 && root.Nodes.Count == 2, "Command search preserves command identity and source tree");

        foreach (var zoom in new[] { .25f, .5f, 1f, 2f, 4f })
        {
            var view = new Rectangle(125, -80, 1000, 700); var cursor = new System.Drawing.Point(333, 221);
            var next = MapEditorViewport.ZoomAt(zoom, view, cursor, new Size(1000, 700), 120);
            Check(Math.Abs((cursor.X / zoom - view.X) - (cursor.X / next.Zoom - next.View.X)) <= .5f &&
                Math.Abs((cursor.Y / zoom - view.Y) - (cursor.Y / next.Zoom - next.View.Y)) <= .5f,
                "Map Editor wheel zoom must preserve the world point under the cursor");
            Check(next.Zoom is >= .25f and <= 4f, "Map zoom bounds");
            var moved = MapEditorViewport.PanFrom(view, new System.Drawing.Point(80, 60), new System.Drawing.Point(180, 110), zoom);
            Check(Math.Abs((moved.X - view.X) * zoom - 100) <= zoom / 2 &&
                Math.Abs((moved.Y - view.Y) * zoom - 50) <= zoom / 2, "Map dragging must follow the pointer at every zoom");
        }
        using (var mapEditor = new FrmMapEditor())
        {
            var mapType = typeof(FrmMapEditor);
            Intersect.Editor.Core.Graphics.MapEditorZoom = 2;
            Intersect.Editor.Core.Graphics.CurrentView = new Rectangle(100, 80, 500, 350);
            mapType.GetMethod("picMap_MouseDown", flags)!.Invoke(mapEditor,
                [mapEditor.picMap, new MouseEventArgs(MouseButtons.Middle, 1, -100, -80, 0)]);
            mapType.GetMethod("picMap_MouseMove", flags)!.Invoke(mapEditor,
                [mapEditor.picMap, new MouseEventArgs(MouseButtons.Middle, 0, 0, -20, 0)]);
            Check(Intersect.Editor.Core.Graphics.CurrentView.Location == new System.Drawing.Point(150, 110),
                "Middle drag starts outside map and moves without accessing map/edit state");
            mapEditor.picMap_MouseUp(mapEditor.picMap, new MouseEventArgs(MouseButtons.Middle, 1, 0, -20, 0));
            mapType.GetField("_spaceHeld", flags)!.SetValue(mapEditor, true);
            mapType.GetMethod("picMap_MouseDown", flags)!.Invoke(mapEditor,
                [mapEditor.picMap, new MouseEventArgs(MouseButtons.Left, 1, 40, 40, 0)]);
            mapType.GetMethod("picMap_MouseMove", flags)!.Invoke(mapEditor,
                [mapEditor.picMap, new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0)]);
            Check(Intersect.Editor.Core.Graphics.CurrentView.Location == new System.Drawing.Point(130, 90), "Space-left drag moves the view");
            mapEditor.picMap_MouseUp(mapEditor.picMap, new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
            Check(Intersect.Editor.General.Globals.MouseButton == -1 && mapEditor.MapUndoStates.Count == 0,
                "Navigation must not draw or create undo entries");
            Check((MouseButtons)mapType.GetField("_panButton", flags)!.GetValue(mapEditor)! == MouseButtons.None,
                "Release ends pan capture");
            mapType.GetMethod("picMap_MouseDown", flags)!.Invoke(mapEditor,
                [mapEditor.picMap, new MouseEventArgs(MouseButtons.Left, 1, 40, 40, 0)]);
            mapEditor.picMap.Capture = false;
            mapEditor.picMap_MouseUp(mapEditor.picMap, new MouseEventArgs(MouseButtons.Left, 1, 40, 40, 0));
            Check(Intersect.Editor.General.Globals.MouseButton == -1 && mapEditor.MapUndoStates.Count == 0,
                "Lost capture must not turn a pan release into a painting action");
            Check((bool)typeof(Control).GetMethod("GetStyle", flags)!.Invoke(mapEditor.picMap, [ControlStyles.Selectable])!,
                "Map canvas must accept keyboard focus for Space navigation");
        }
        var marker = new WorldMapEventMarker(Guid.NewGuid(), Guid.NewGuid(), 5, 8, Guid.NewGuid(), -64, 32);
        var copy = MessagePackSerializer.Deserialize<WorldMapEventMarker>(MessagePackSerializer.Serialize(marker));
        Check(copy.OffsetX == -64 && copy.OffsetY == 32, "Marker offsets must survive the network packet");
        var page = Newtonsoft.Json.JsonConvert.DeserializeObject<EventPage>(Newtonsoft.Json.JsonConvert.SerializeObject(editor.CurrentPage))!;
        Check(page.WorldMapOffsetX == -64 && page.WorldMapOffsetY == 32, "Marker offsets must survive event page persistence");
        System.Console.WriteLine("PASS editor refresh: responsive Event Editor, full screen, command search, per-page offsets, cursor-anchored wheel zoom, middle/Space map navigation, capture cancellation and packet/persistence round-trips");
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
