using System.Reflection;
using System.Windows.Forms;
using Intersect.Framework.Core.Cinematics;
using DrawingColor = System.Drawing.Color;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;

// Hidden WinForms smoke test. The generated bitmap is an explicitly synthetic layout fixture,
// not evidence of a connected map, GPU renderer, live player or completed in-game acceptance test.
internal static class Program
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static object Field(object target, string name) => target.GetType().GetField(name, Flags)!.GetValue(target)!;
    private static void Set(object target, string name, object value) => target.GetType().GetProperty(name, Flags)!.SetValue(target, value);
    private static void Mouse(object target, string method, int x, int y) =>
        target.GetType().GetMethod(method, Flags)!.Invoke(target, [new MouseEventArgs(MouseButtons.Left, 1, x, y, 0)]);
    private static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.FirstOrDefault() == "--native-map") return NativeMapSmoke.Run(args[1], args[2]);
            if (args.FirstOrDefault() == "--interactive-scenario") return InteractiveScenario.Export(args[1], args[2]);
            Intersect.Options.LoadFromServer("{}");
            var assembly = typeof(Intersect.Editor.Forms.FrmMain).Assembly;
            using (var textStream = assembly.GetManifestResourceStream("Cinematics.Texts.json")!)
            {
                var catalog = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(textStream)!;
                Require(catalog.Values.All(v => new[] { "en", "fr", "es" }.All(language => v.TryGetValue(language, out var text) && !string.IsNullOrWhiteSpace(text))),
                    "Incomplete cinematic locale coverage.");
            }
            var translate = assembly.GetType("Intersect.Editor.Forms.Cinematics.CinematicText")!.GetMethod("T", BindingFlags.NonPublic | BindingFlags.Static)!;
            System.Globalization.CultureInfo.CurrentUICulture = new("de-DE");
            Require((string)translate.Invoke(null, ["New"])! == "New scene", "Missing English locale fallback.");
            System.Globalization.CultureInfo.CurrentUICulture = new("fr-FR");
            Require((string)translate.Invoke(null, ["New"])! == "Nouvelle scène", "French catalog selection failed.");
            using var form = (Form)Activator.CreateInstance(assembly.GetType("Intersect.Editor.Forms.Cinematics.FrmCinematicDirector")!, true)!;
            form.Text += " — SYNTHETIC UI TEST";
            form.ClientSize = new(1400, 900);
            // WinForms omits invisible child controls from DrawToBitmap. Realize the
            // form outside the desktop, then stop its game-dependent preview timer.
            form.ShowInTaskbar = false;
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new(-20000, -20000);
            form.Show();
            ((System.Windows.Forms.Timer)Field(form, "_timer")).Stop();
            var scene = new CinematicScene { Name = "UI fixture", Saga = "QA", Chapter = "Layout", Sequence = "Input", MapId = Guid.NewGuid() };
            scene.Actors.Add(new() { Name = "Player", StartX = 2, StartY = 3 });
            var movement = new CinematicClip { Track = CinematicTrack.Movement, Name = "Path fixture", StartMs = 2000, DurationMs = 1500,
                Points = [new() { X = 2, Y = 3 }, new() { X = 5, Y = 3 }] };
            scene.Clips.Add(movement);
            scene.Clips.Add(new() { Track = CinematicTrack.Camera, Name = "Camera fixture", X = 12, Y = 8, Zoom = 2, DurationMs = 1500 });
            scene.Clips.Add(new() { Track = CinematicTrack.Dialogue, Name = "Interactive dialogue", Text = "Synthetic test — no game assets or server.", StartMs = 4000, DurationMs = 1500 });
            form.GetType().GetField("_scene", Flags)!.SetValue(form, scene);
            var viewport = (Control)Field(form, "_viewport"); var timeline = (Control)Field(form, "_timeline");
            Set(viewport, "Scene", scene); Set(timeline, "Scene", scene); Set(viewport, "Selected", movement);
            var width = Intersect.Options.Instance.Map.MapWidth * Intersect.Options.Instance.Map.TileWidth;
            var height = Intersect.Options.Instance.Map.MapHeight * Intersect.Options.Instance.Map.TileHeight;
            using var fixture = new System.Drawing.Bitmap(width, height);
            using (var graphics = System.Drawing.Graphics.FromImage(fixture))
            {
                graphics.Clear(DrawingColor.FromArgb(35, 54, 61));
                using var pen = new System.Drawing.Pen(DrawingColor.FromArgb(65, 92, 96));
                for (var x = 0; x < width; x += Intersect.Options.Instance.Map.TileWidth) graphics.DrawLine(pen, x, 0, x, height);
                for (var y = 0; y < height; y += Intersect.Options.Instance.Map.TileHeight) graphics.DrawLine(pen, 0, y, width, y);
                graphics.DrawString("SYNTHETIC LAYOUT FIXTURE", System.Drawing.SystemFonts.DefaultFont, System.Drawing.Brushes.White, 20, 20);
            }
            Set(viewport, "MapImage", fixture);
            form.GetType().GetMethod("RefreshTree", Flags)!.Invoke(form, null);
            form.GetType().GetMethod("SelectObject", Flags)!.Invoke(form, [movement]);
            form.GetType().GetMethod("RefreshPoints", Flags)!.Invoke(form, null);
            Set(timeline, "Selected", movement);
            form.CreateControl(); form.PerformLayout();
            using var screenshot = new System.Drawing.Bitmap(form.Width, form.Height);
            form.DrawToBitmap(screenshot, new DrawingRectangle(0, 0, form.Width, form.Height));
            if (args.Length > 0) screenshot.Save(Path.GetFullPath(args[0]), System.Drawing.Imaging.ImageFormat.Png);
            Require(viewport.Width >= 300 && viewport.Height >= 200, "Viewport collapsed.");
            Require(timeline.Height >= 150 && timeline.Width >= 600, "Timeline collapsed.");
            Require(((TreeView)Field(form, "_explorer")).Nodes.Count > 0 &&
                ((PropertyGrid)Field(form, "_properties")).SelectedObject != null,
                "Scene hierarchy or selected-clip inspector did not populate.");
            Mouse(timeline, "OnMouseDown", 330, 80); Mouse(timeline, "OnMouseMove", 410, 80); Mouse(timeline, "OnMouseUp", 410, 80);
            Require(movement.StartMs == 3000, "Timeline drag did not move the clip by one second.");
            Mouse(timeline, "OnMouseDown", 518, 80); Mouse(timeline, "OnMouseMove", 558, 80); Mouse(timeline, "OnMouseUp", 558, 80);
            Require(movement.DurationMs == 2000, "Timeline right-edge drag did not resize the clip.");
            Set(viewport, "MapImage", fixture); Set(viewport, "Selected", movement);
            var tile = (System.Drawing.PointF)viewport.GetType().GetMethod("Screen", Flags)!.Invoke(viewport, [6f, 6f])!;
            Mouse(viewport, "OnMouseDown", (int)tile.X, (int)tile.Y);
            Require(movement.Points.Count == 3 && movement.Points[^1].X == 6 && movement.Points[^1].Y == 6, "Viewport click did not author a tile waypoint.");
            Console.WriteLine("PASS: hidden editor construction, panel layout, timeline drag/resize, viewport waypoint input.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error is TargetInvocationException { InnerException: { } inner } ? inner : error);
            return 1;
        }
    }
}
