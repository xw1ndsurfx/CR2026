using System.Reflection;
using Intersect.Editor.Forms;
using Intersect.Editor.Forms.DockingElements;
using Intersect.Editor.General;
using WeifenLuo.WinFormsUI.Docking;
using DrawingColor = System.Drawing.Color;

internal static class WorkspaceThemeTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    internal static void Run()
    {
        using var main = new FrmMain();
        var theme = typeof(FrmMain).Assembly.GetType("Intersect.Editor.Forms.Styling.EditorTheme", true)!;
        var apply = theme.GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic)!;
        var background = (DrawingColor)theme.GetField("Background", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var input = (DrawingColor)theme.GetField("Input", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var selection = (DrawingColor)theme.GetField("Selection", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var menu = (MenuStrip)typeof(FrmMain).GetField("menuStrip", Flags)!.GetValue(main)!;
        var dock = (DockPanel)typeof(FrmMain).GetField("dockLeft", Flags)!.GetValue(main)!;
        Check(menu.Items.OfType<ToolStripLabel>().Any(item => item.Name == "LogiklikWorkspaceIdentity"),
            "Startup regression must include the non-command workspace identity.");
        using (var separator = new ToolStripSeparator())
        {
            menu.Items.Add(separator);
            // InitEditor runs this after login, independently of the GPU/network initialization.
            typeof(FrmMain).GetMethod("GrabMouseDownEvents", Flags, null, Type.EmptyTypes, null)!.Invoke(main, null);
            menu.Items.Remove(separator);
        }
        Console.WriteLine("PASS editor startup registers mouse handlers on menus with commands, labels and separators");
        Check(main.BackColor == background && dock.Theme.ColorPalette.TabSelectedActive.Background == selection,
            "Main window and docking chrome must use the Logiklik login palette.");
        var command = (ToolStripButton)typeof(FrmMain).GetField("toolStripBtnBrush", Flags)!.GetValue(main)!;
        var clicks = 0; command.Click += (_, _) => clicks++; command.PerformClick();
        Check(clicks == 1 && command.Checked, "Restyling must retain existing tool handlers and checked state.");
        using var properties = new FrmMapProperties();
        var grid = (PropertyGrid)typeof(FrmMapProperties).GetField("gridMapProperties", Flags)!.GetValue(properties)!;
        grid.SelectedObject = new SampleProperties();
        Check(grid.ViewBackColor == input && grid.SelectedItemWithFocusBackColor == selection && grid.SelectedObject is SampleProperties,
            "The map PropertyGrid keeps its editable content with readable theme colors.");
        using var controls = new Panel();
        var swatch = new PictureBox { BackColor = DrawingColor.Magenta }; controls.Controls.Add(swatch);
        var colorPanel = new Panel { Name = "pnlLightColor", BackColor = DrawingColor.White }; controls.Controls.Add(colorPanel);
        var warning = new ToolStrip { BackColor = DrawingColor.FromArgb(105, 75, 20) }; controls.Controls.Add(warning);
        var error = new Label { ForeColor = DrawingColor.Red }; controls.Controls.Add(error);
        var value = new TextBox { Text = "unchanged", Enabled = false }; controls.Controls.Add(value);
        apply.Invoke(null, [controls]); apply.Invoke(null, [controls]);
        Check(swatch.BackColor == DrawingColor.Magenta && colorPanel.BackColor == DrawingColor.White && warning.BackColor.R == 105 && value.Text == "unchanged" && !value.Enabled,
            "Theme application must preserve authored swatches, save-warning colors and input state.");
        Check(error.ForeColor.R > error.ForeColor.G * 1.5, "Semantic error text must stay distinct after repeated theme application.");
        var dynamicInput = new TextBox(); controls.Controls.Add(dynamicInput);
        Check(dynamicInput.BackColor == input, "Controls created after the initial theme must inherit it.");
        Check(menu.Items.Cast<ToolStripItem>().Count(item => item.Name == "LogiklikWorkspaceIdentity") == 1,
            "The workspace identity must remain a single compact menu item.");

        RemoveLoad(main, "frmMain_Load");
        RemoveLoad(Globals.MapLayersWindow, "frmMapLayers_Load");
        RemoveLoad(Globals.MapGridWindowNew, "frmMapGrid_Load");
        RemoveLoad(Globals.MapEditorWindow, "frmMapEditor_Load");
        var close = (FormClosingEventHandler)Delegate.CreateDelegate(typeof(FormClosingEventHandler), main,
            typeof(FrmMain).GetMethod("frmMain_FormClosing", Flags)!);
        main.FormClosing -= close;
        main.Opacity = 0; main.ShowInTaskbar = false; main.Size = new Size(1440, 900);
        main.Show(); Application.DoEvents(); main.PerformLayout();
        properties.Show(Globals.MapListWindow.Pane, DockAlignment.Bottom, .42);
        Globals.MapListWindow.mapTreeList.list.Nodes.Add("Polonya Island").Nodes.Add("Polonya Port");
        Globals.MapListWindow.mapTreeList.list.Nodes.Add("Dungeon");
        Globals.MapListWindow.mapTreeList.list.Nodes.Add("Ocean");
        Globals.MapListWindow.mapTreeList.list.ExpandAll();
        Application.DoEvents();
        Check(menu.Bottom <= dock.Top && dock.Bottom <= main.ClientSize.Height - 24,
            "Menu/toolbar/status chrome must leave an unobscured central map workspace.");
        foreach (var size in new[] { new Size(1920, 1080), new Size(1024, 700), new Size(1440, 900) })
        {
            main.Size = size;
            Globals.MapGridWindowNew.Activate();
            Application.DoEvents();
            CheckWorkspaceFill(Globals.MapGridWindowNew, "pnlMapGrid");
            Globals.MapEditorWindow.Activate();
            Globals.MapEditorWindow.picMap.Visible = true;
            Application.DoEvents();
            var container = Globals.MapEditorWindow.pnlMapContainer;
            Check(container.Bounds == Globals.MapEditorWindow.ClientRectangle &&
                Globals.MapEditorWindow.picMap.Bounds == container.ClientRectangle &&
                !container.HorizontalScroll.Visible && !container.VerticalScroll.Visible,
                $"Map editor canvas must fill its docked viewport without scrollbars at {size}.");
            CheckWorkspaceFill(Globals.MapListWindow, "mapTreeList");
            Check(Globals.MapListWindow.mapTreeList.list.Bounds == Globals.MapListWindow.mapTreeList.ClientRectangle,
                $"Map tree must use the entire list viewport at {size}.");
        }
        Console.WriteLine("PASS map grid, editor canvas and map list fill their docked panes after resizing and tab switching");
        if (Environment.GetEnvironmentVariable("CODEX_EDITOR_THEME_PREVIEW") is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder);
            using var bitmap = new Bitmap(main.Width, main.Height);
            main.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
            // WinForms DrawToBitmap omits native MDI children. Render the actual
            // docking panes/content explicitly into their real client positions.
            using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
                foreach (var pane in dock.Panes.Where(pane => pane.Visible))
                {
                    Draw(pane);
                    foreach (var content in pane.Contents.OfType<DockContent>().Where(content => content.Visible)) Draw(content);
                    void Draw(Control control)
                    {
                        if (control.Width < 1 || control.Height < 1) return;
                        using var image = new Bitmap(control.Width, control.Height);
                        control.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height));
                        var position = main.PointToClient(control.PointToScreen(System.Drawing.Point.Empty));
                        position.Offset((main.Width - main.ClientSize.Width) / 2, main.Height - main.ClientSize.Height - (main.Width - main.ClientSize.Width) / 2);
                        graphics.DrawImageUnscaled(image, position);
                    }
                }
            bitmap.Save(Path.Combine(folder, "editor-workspace.png"));
        }
        theme.GetMethod("Install", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null);
        using (var legacy = new Form { Opacity = 0, ShowInTaskbar = false })
        {
            var field = new TextBox { Text = "kept" }; legacy.Controls.Add(field);
            legacy.Show(); Application.RaiseIdle(EventArgs.Empty);
            Check(legacy.BackColor == background && field.BackColor == input && field.Text == "kept",
                "New legacy dialogs inherit the common theme through the application hook.");
            legacy.Hide();
        }
        main.Hide();
        Console.WriteLine("PASS Logiklik workspace: docking palette, tool actions, editable properties, dynamic controls, preserved swatches/save warnings and unobscured map viewport");
    }
    private static void CheckWorkspaceFill(Form form, string field)
    {
        var content = (Control)form.GetType().GetField(field, Flags)!.GetValue(form)!;
        var toolbar = form.Controls.OfType<ToolStrip>().Single();
        Check(content.Bounds == new Rectangle(0, toolbar.Bottom, form.ClientSize.Width, form.ClientSize.Height - toolbar.Bottom),
            $"{field} must fill the docked pane below its toolbar: {content.Bounds}, client {form.ClientSize}.");
    }
    private static void RemoveLoad(Form form, string method) => form.Load -= (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), form, form.GetType().GetMethod(method, Flags)!);
    private sealed class SampleProperties
    {
        public string Name { get; set; } = "Polonya Island";
        public int Brightness { get; set; } = 100;
        public bool Indoors { get; set; }
    }
}
