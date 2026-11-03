using System.Reflection;
using System.Runtime.InteropServices;
using Intersect.Editor.Forms;
using Intersect.Editor.Forms.Editors;
using Intersect.Editor.Forms.Editors.Events;
using Intersect.Editor.Forms.Styling;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Localization;

internal static class EditorWindowTests
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    internal static void Run()
    {
        CheckLowResolutionScrolling();
        using var owner = new Form { ClientSize = new Size(640, 480), ShowInTaskbar = false, Opacity = 0 };
        owner.Show();
        using var window = new Form { ClientSize = new Size(850, 620), ShowInTaskbar = false, Opacity = 0 };
        var input = new TextBox(); window.Controls.Add(input);
        EditorWindows.Show(window, owner); Application.DoEvents();
        Check(window.Owner == owner && GetWindow(window.Handle, 4) == owner.Handle && !window.TopMost,
            "Editors have a native owner that keeps them above their parent without global topmost.");
        var original = window.Bounds;
        EditorWindows.Show(window, owner);
        Check(window.Bounds == original && owner.OwnedForms.Length == 1, "Opening an existing editor reuses it without moving or duplicating it.");
        window.WindowState = FormWindowState.Minimized;
        EditorWindows.Show(window, owner); Application.DoEvents();
        Check(window.WindowState != FormWindowState.Minimized, "Reopening a minimized editor restores it.");
        original = window.Bounds;
        var key = Message.Create(input.Handle, 0x100, (IntPtr)Keys.F11, IntPtr.Zero);
        Check(Application.FilterMessage(ref key) && EditorWindows.IsFullscreen(window) && window.FormBorderStyle == FormBorderStyle.None,
            "F11 reaches the window while a text field has focus.");
        Check(window.Bounds == Screen.FromControl(window).Bounds, "Fullscreen fills the window's own monitor.");
        var repeat = Message.Create(input.Handle, 0x100, (IntPtr)Keys.F11, (IntPtr)(1L << 30));
        Application.FilterMessage(ref repeat);
        Check(EditorWindows.IsFullscreen(window), "Holding F11 cannot repeatedly toggle fullscreen.");
        key = Message.Create(input.Handle, 0x100, (IntPtr)Keys.Escape, IntPtr.Zero);
        Check(Application.FilterMessage(ref key) && !EditorWindows.IsFullscreen(window) && window.Bounds == original,
            "Escape restores the previous bounds instead of closing the editor.");
        window.WindowState = FormWindowState.Maximized; Application.DoEvents();
        EditorWindows.ToggleFullscreen(window); EditorWindows.ToggleFullscreen(window);
        Check(window.WindowState == FormWindowState.Maximized, "Fullscreen preserves an already maximized window's state.");
        window.WindowState = FormWindowState.Normal;
        window.Bounds = new Rectangle(-30000, -30000, 850, 620);
        EditorWindows.ConstrainToScreen(window, owner);
        Check(Screen.FromControl(owner).WorkingArea.Contains(window.Bounds), "Offscreen editors return to their owner's monitor.");
        using var confirmation = new Form { FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false };
        EditorWindows.ToggleFullscreen(confirmation);
        Check(EditorWindows.IsFullscreen(confirmation), "Standalone dialogs also support fullscreen.");
        EditorWindows.ToggleFullscreen(confirmation);
        using var events = new FrmEvent(new MapDescriptor(Guid.NewGuid()));
        EditorTheme.Apply(events);
        Check(EditorWindows.SupportsFullscreen(events), "The Event Editor shares fullscreen and Escape handling with other editors.");
        using (var fixedEditor = new FrmOptions())
        {
            RemoveLoad(fixedEditor); fixedEditor.Opacity = 0; fixedEditor.ShowInTaskbar = false;
            EditorTheme.Apply(fixedEditor);
            fixedEditor.Show(); Application.DoEvents();
            Check(fixedEditor.Owner == window && fixedEditor.MaximizeBox && EditorWindows.SupportsFullscreen(fixedEditor),
                "A legacy editor opened without an owner adopts the active parent and supports fullscreen.");
            var before = fixedEditor.Bounds;
            EditorWindows.ToggleFullscreen(fixedEditor);
            Check(fixedEditor.Bounds == Screen.FromControl(fixedEditor).Bounds, "Fixed editors fill the monitor in fullscreen.");
            EditorWindows.ToggleFullscreen(fixedEditor);
            Check(fixedEditor.Bounds == before, "Fixed editors restore their previous size.");
            fixedEditor.Hide();
        }
        EditorWindows.Show(window, owner);
        using (var lateEditor = new FrmOptions())
        {
            RemoveLoad(lateEditor); lateEditor.Opacity = 0; lateEditor.ShowInTaskbar = false;
            lateEditor.Show();
            var filterType = typeof(EditorTheme).GetNestedType("FirstPaintFilter", BindingFlags.NonPublic)!;
            var filter = (IMessageFilter)Activator.CreateInstance(filterType, true)!;
            var paint = Message.Create(lateEditor.Handle, 0x000F, IntPtr.Zero, IntPtr.Zero);
            filter.PreFilterMessage(ref paint);
            Check(lateEditor.Owner == window && EditorWindows.SupportsFullscreen(lateEditor),
                "First-paint fallback repairs windows opened without theme or explicit ownership.");
            lateEditor.Hide();
        }
        window.Hide();

        foreach (var form in new Form[] { new FrmClass(), new FrmShop() })
        using (form)
        {
            RemoveLoad(form); form.Opacity = 0; form.ShowInTaskbar = false;
            var content = (Panel)form.Controls.Find("pnlContainer", true).Single(); content.Visible = true;
            var split = (SplitContainer)form.Controls.Find("EditorWorkspaceSplit", true).Single();
            var footer = form.Controls.Find("EditorActions", true).Single();
            foreach (Control action in footer.Controls) action.Visible = true;
            EditorWindows.Show(form, owner); Application.DoEvents();
            foreach (var size in new[] { new Size(1024, 700), new Size(1600, 900), new Size(800, 580) })
            {
                form.Size = size; Application.DoEvents();
                Check(form.MaximizeBox && form.FormBorderStyle == FormBorderStyle.Sizable &&
                    split.Panel2.ClientRectangle.Contains(content.Bounds), "Class/shop workspaces fill their resizable content area.");
                Check(form.ClientRectangle.Contains(footer.Bounds) && footer.Top >= split.Bottom,
                    "The action footer has its own visible space below the workspace.");
                foreach (var action in form.Controls.Find("EditorActions", true).Single().Controls.OfType<Button>())
                    Check(action.Parent!.ClientRectangle.Contains(action.Bounds), "Save and cancel remain reachable at every supported size.");
            }
            if (form is FrmClass)
            {
                var tabs = (TabControl)form.Controls.Find("ClassEditorSections", true).Single();
                Check(tabs.TabCount == 5 && tabs.TabPages.Cast<TabPage>().All(page => page.Controls.OfType<FlowLayoutPanel>().Single().AutoScroll),
                    "Class settings have five independently scrolling sections.");
                var leveling = form.Controls.Find("grpLeveling", true).Single();
                var grid = form.Controls.Find("grpExpGrid", true).Single();
                Check(leveling.Parent == grid.Parent && leveling.Location == grid.Location,
                    "The existing progression/experience-grid switch keeps its shared overlay container.");
                tabs.SelectedIndex = 2;
            }
            else
            {
                var lists = form.Controls.Find("ShopEditorSections", true).Single();
                foreach (var name in new[] { "grpItemsSold", "grpItemsBought" })
                {
                    var group = form.Controls.Find(name, true).Single();
                    var viewport = (Panel)group.Parent!;
                    Check(group.Dock == DockStyle.None && group.Height >= group.MinimumSize.Height &&
                        viewport.DisplayRectangle.Height >= group.Height,
                        "Shop offer fields retain real scrollable bounds at laptop resolutions.");
                    var action = group.Controls.OfType<Button>().OrderBy(control => control.Bottom).Last();
                    viewport.ScrollControlIntoView(action);
                    Check(viewport.ClientRectangle.Contains(viewport.RectangleToClient(action.RectangleToScreen(action.ClientRectangle))),
                        "The last shop offer action scrolls fully into view.");
                }
                Check(lists is TableLayoutPanel table && table.ColumnStyles.Cast<ColumnStyle>().All(style => style.SizeType == SizeType.Percent),
                    "Shop offer columns share the available width.");
            }
            form.Size = new Size(1300, 850); Application.DoEvents();
            if (Environment.GetEnvironmentVariable("CODEX_EDITOR_WINDOW_PREVIEW") is { Length: > 0 } folder)
            {
                Directory.CreateDirectory(folder);
                using var image = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height));
                image.Save(Path.Combine(folder, form.GetType().Name + ".png"));
            }
            form.Hide();
        }
        foreach (var text in new[] { "Full screen (F11)", "Progression", "Starting items and spells" })
        {
            foreach (var language in GameTextCatalog.Languages)
                Check(QualityOfLifeText.Catalog.Entries[text].ContainsKey(language) && GameTextTokens.Compatible(text, QualityOfLifeText.Get(text, language)),
                    "Window labels include compatible en/fr/es translations.");
            Check(QualityOfLifeText.Get(text, "unknown") == text, "Window labels fall back to English.");
        }
        foreach (var factory in new Func<Form>[] { () => new FrmItem(), () => new FrmNpc(), () => new FrmSpell(), () => new FrmProjectile(),
            () => new FrmResource(), () => new FrmCrafts(), () => new FrmAnimation(), () => new FrmSwitchVariable() })
        using (var legacy = factory())
        {
            RemoveLoad(legacy); legacy.ShowInTaskbar = false; legacy.Opacity = 0;
            foreach (var body in legacy.Controls.Find("pnlContainer", true)) body.Visible = true;
            EditorWindows.Show(legacy, owner); Application.DoEvents();
            var split = legacy.Controls.Find("EditorWorkspaceSplit", true).OfType<SplitContainer>().SingleOrDefault();
            var footer = legacy.Controls.Find("EditorActions", true).SingleOrDefault();
            foreach (var size in new[] { new Size(1100, 730), new Size(1600, 900), new Size(800, 580) })
            {
                legacy.Size = size; Application.DoEvents(); legacy.PerformLayout();
                if (split != null && footer != null)
                {
                    Check(legacy.ClientRectangle.Contains(split.Bounds) && legacy.ClientRectangle.Contains(footer.Bounds), "Global workspace bounds escaped " + legacy.GetType().Name);
                    Check(split.Width >= legacy.ClientSize.Width - 10 && split.Panel2.Width > 350, "Legacy content still has a narrow fixed width: " + legacy.GetType().Name);
                    Check(footer.Top >= split.Bottom, "Save/Cancel overlay the global workspace: " + legacy.GetType().Name);
                    Check(split.Panel2.Controls.Cast<Control>().All(c => split.Panel2.ClientRectangle.Contains(c.Bounds)), "Content viewport is clipped inside its split pane: " + legacy.GetType().Name);
                }
                else
                {
                    var content = legacy.Controls.Find("pnlContainer", true).Single();
                    Check(legacy is FrmItem or FrmNpc or FrmSpell && legacy.ClientRectangle.Contains(content.Bounds), "Custom layout was replaced or clipped: " + legacy.GetType().Name);
                    Check(content.Width > legacy.ClientSize.Width / 2, "Custom content failed to resize: " + legacy.GetType().Name);
                }
            }
            legacy.Hide();
        }
        foreach (var factory in new Func<Form>[] { () => new FrmCommonEvent(), () => new FrmTime(),
            () => new FrmDynamicRequirements(new Intersect.Framework.Core.GameObjects.Conditions.ConditionLists(), RequirementType.Item) })
        using (var editor = factory())
        {
            RemoveLoad(editor); editor.ShowInTaskbar = false; editor.Opacity = 0;
            EditorWindows.Show(editor, owner); Application.DoEvents();
            editor.Size = new Size(1500, 850); Application.DoEvents(); editor.PerformLayout();
            Check(editor.Controls.Cast<Control>().Where(c => c.Visible).All(c => editor.ClientRectangle.Contains(c.Bounds)), "Resizing clipped a selector, time editor or requirement workspace: " + editor.GetType().Name);
            editor.Hide();
        }
        CheckEditorCoverage(owner);
        owner.Hide();
        Console.WriteLine("PASS editor windows: native ownership, reopen/restore, F11/Escape, held keys, monitor bounds, class/shop responsive layouts and locales");
    }

    private static void CheckEditorCoverage(Form owner)
    {
        var types = typeof(EditorForm).Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(EditorForm)) &&
            !type.IsAbstract && type.GetConstructor(Type.EmptyTypes) != null).OrderBy(type => type.Name);
        foreach (var type in types)
        {
            using var form = (Form)Activator.CreateInstance(type)!;
            var designed = form.ClientSize;
            var designedMinimum = form.MinimumSize;
            RemoveLoad(form); form.ShowInTaskbar = false; form.Opacity = 0;
            EditorWindows.Show(form, owner); Application.DoEvents();
            form.MinimumSize = Size.Empty;
            foreach (var size in new[] { new Size(1024, 600), new Size(800, 500), new Size(1200, 700) })
            {
                form.ClientSize = size; Application.DoEvents();
                Check((form.ClientSize.Width >= designed.Width && form.ClientSize.Height >= designed.Height) ||
                    form.Controls.Find("EditorWorkspaceSplit", true).Length > 0 ||
                    (form is FrmItem or FrmNpc or FrmSpell && form.Width >= designedMinimum.Width && form.Height >= designedMinimum.Height) ||
                    form.Controls.Find("EditorScrollViewport", true).SingleOrDefault() is Panel { AutoScroll: true },
                    type.Name + " exposes a scrolling viewport when reduced to laptop dimensions.");
            }
            Console.WriteLine("PASS low resolution window: " + type.Name);
            form.Hide();
        }
    }

    private static void CheckLowResolutionScrolling()
    {
        using var dialog = new Form { ClientSize = new Size(900, 600), ShowInTaskbar = false, Opacity = 0 };
        var fill = new Panel { Dock = DockStyle.Fill };
        var bottom = new Button { Location = new System.Drawing.Point(700, 530), Size = new Size(150, 30),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
        dialog.Controls.Add(fill); dialog.PerformLayout(); fill.Controls.Add(bottom);
        EditorScrollLayout.Attach(dialog);
        dialog.ClientSize = new Size(600, 350);
        dialog.Show(); Application.DoEvents(); dialog.PerformLayout();
        var viewport = (Panel)dialog.Controls.Find("EditorScrollViewport", true).Single();
        viewport.PerformLayout();
        Check(viewport.AutoScroll && viewport.DisplayRectangle.Width >= 900 && viewport.DisplayRectangle.Height >= 600,
            "Legacy docked dialogs preserve content in both scroll directions at low resolution.");
        viewport.ScrollControlIntoView(bottom);
        Check(viewport.ClientRectangle.Contains(viewport.RectangleToClient(bottom.RectangleToScreen(bottom.ClientRectangle))),
            "The final bottom/right action can be scrolled fully into view.");
        dialog.ClientSize = new Size(1200, 700); dialog.PerformLayout(); viewport.PerformLayout();
        Check(!viewport.HorizontalScroll.Visible && !viewport.VerticalScroll.Visible,
            "Scrollbars disappear when the window fits the content again.");
    }

    private static void RemoveLoad(Form form)
    {
        var events = (System.ComponentModel.EventHandlerList)typeof(System.ComponentModel.Component)
            .GetProperty("Events", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
        var key = typeof(Form).GetFields(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(field => field.FieldType == typeof(object) && field.Name.Contains("load", StringComparison.OrdinalIgnoreCase)).GetValue(null)!;
        if (events[key] is EventHandler load) form.Load -= load;
    }

    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
}
