using System.Globalization;
using System.Reflection;
using Intersect;
using Intersect.Editor.Forms.DockingElements;
using Intersect.Editor.Forms.Styling;
using Intersect.Editor.General;
using Intersect.Editor.Maps;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;

internal static class DockLandingPickerTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    internal static void Run()
    {
        var previousMap = Globals.CurrentMap; var previousEditor = Globals.MapEditorWindow; var culture = CultureInfo.CurrentUICulture;
        var view = Intersect.Editor.Core.Graphics.CurrentView;
        try
        {
            foreach (var language in new[] { "en", "fr", "es" })
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
                using var layers = new FrmMapLayers();
                var map = new MapInstance(Guid.NewGuid());
                foreach (var layer in Options.Instance.Map.Layers.All) map.Layers[layer] = new Tile[Options.Instance.Map.MapWidth, Options.Instance.Map.MapHeight];
                map.Attributes[1, 1] = new MapDockAttribute();
                map.InitAutotiles(); Globals.CurrentMap = map;
                using var editor = new FrmMapEditor(); Globals.MapEditorWindow = editor;
                typeof(FrmMapLayers).GetMethod("ShowDockLandingEditor", Hidden)!.Invoke(layers, null);
                var group = (GroupBox)layers.Controls.Find("DockLandingSettings", true).Single();
                var custom = (CheckBox)group.Controls.Find("DockCustomLanding", true).Single();
                var x = (NumericUpDown)group.Controls.Find("DockLandingX", true).Single();
                var y = (NumericUpDown)group.Controls.Find("DockLandingY", true).Single();
                var choose = (Button)group.Controls.Find("ChooseDockLanding", true).Single();
                using var host = new Form { ClientSize = new Size(280, 365), ShowInTaskbar = false };
                // Display the actual attribute group without initializing the networked map dock.
                group.Parent = host; group.Location = new(8, 8); group.Visible = true;
                EditorTheme.Apply(host); host.Show(); Application.DoEvents();
                choose.PerformClick();
                Intersect.Editor.Core.Graphics.CurrentView = new(37, 51, 1000, 1000);
                var before = map.SaveInternal();
                bool Pick(MouseButtons button, int px, int py) => (bool)typeof(FrmMapEditor).GetMethod("PickDockLanding", Hidden)!.Invoke(editor, [new MouseEventArgs(button, 1, px, py, 0)])!;
                Pick(MouseButtons.Left, 37 + Options.Instance.Map.TileWidth + 2, 51 + Options.Instance.Map.TileHeight + 2);
                editor.picMap_MouseUp(editor, new(MouseButtons.Left, 1, 0, 0, 0));
                if (!editor.ChoosingDockLanding || custom.Checked || !before.Matches(map.SaveInternal())) throw new Exception("A solid Q cannot be the arrival; keep picking without painting");
                if (!Pick(MouseButtons.Left, 37 + 4 * Options.Instance.Map.TileWidth + 2, 51 + 6 * Options.Instance.Map.TileHeight + 2)) throw new Exception("Dock picker must consume the map click");
                editor.picMap_MouseUp(editor, new(MouseButtons.Left, 1, 0, 0, 0));
                if (!(bool)typeof(FrmMapEditor).GetMethod("PickDockLanding", Hidden)!.Invoke(editor, [new MouseEventArgs(MouseButtons.Left, 2, 50, 60, 0)])!)
                    throw new Exception("The second click of a double-click must not paint an attribute at the arrival");
                editor.picMap_MouseUp(editor, new(MouseButtons.Left, 2, 0, 0, 0));
                var painted = (MapDockAttribute)layers.CreateAttribute(MapAttributeType.Dock);
                if (!custom.Checked || painted.LandingX != 4 || painted.LandingY != 6 || !painted.HasLandingPoint)
                    throw new Exception("Map selection must configure the next Q attribute in native tile coordinates");
                if (!before.Matches(map.SaveInternal()) || editor.MapUndoStates.Count != 0)
                    throw new Exception("Selecting an arrival tile must not paint, save, fetch or replace the current map");
                choose.PerformClick(); Pick(MouseButtons.Right, 50, 60); editor.picMap_MouseUp(editor, new(MouseButtons.Right, 1, 0, 0, 0));
                if (x.Value != 4 || y.Value != 6) throw new Exception("Cancellation changes the configured point");
                choose.PerformClick();
                var message = Message.Create(IntPtr.Zero, 0x100, IntPtr.Zero, IntPtr.Zero);
                typeof(FrmMapLayers).GetMethod("ProcessCmdKey", Hidden)!.Invoke(layers, [message, Keys.Escape]);
                if (editor.ChoosingDockLanding) throw new Exception("Escape from the attributes panel must cancel point selection");
                choose.PerformClick(); Globals.CurrentMap = new MapInstance(Guid.NewGuid()); Pick(MouseButtons.Left, 50, 60);
                editor.picMap_MouseUp(editor, new(MouseButtons.Left, 1, 0, 0, 0)); Globals.CurrentMap = map;
                if (x.Value != 4 || y.Value != 6) throw new Exception("A stale click on another map must be ignored");
                choose.PerformClick(); Pick(MouseButtons.Left, 37 + 4 * Options.Instance.Map.TileWidth + 2, 51 + 6 * Options.Instance.Map.TileHeight + 2);
                editor.picMap_MouseUp(editor, new(MouseButtons.Left, 1, 0, 0, 0));
                if (Environment.GetEnvironmentVariable("EDITOR_TEST_SCREENSHOTS") is { Length: > 0 } folder)
                {
                    Directory.CreateDirectory(folder); using var image = new Bitmap(host.Width, host.Height);
                    host.DrawToBitmap(image, new Rectangle(System.Drawing.Point.Empty, image.Size)); image.Save(Path.Combine(folder, "dock-landing-" + language + ".png"));
                }
                custom.Checked = false;
                if (((MapDockAttribute)layers.CreateAttribute(MapAttributeType.Dock)).HasLandingPoint) throw new Exception("Automatic placement must remain available");
                host.Hide();
            }
        }
        finally { Globals.CurrentMap = previousMap; Globals.MapEditorWindow = previousEditor; CultureInfo.CurrentUICulture = culture; Intersect.Editor.Core.Graphics.CurrentView = view; }
        System.Console.WriteLine("PASS dock landing picker: native coordinates, non-mutating input, cancellation, stale-map protection and EN/FR/ES panels");
    }
}
