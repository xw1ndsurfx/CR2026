using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using Intersect.Editor.Forms;
using Intersect.Editor.Forms.EditorHub;
using Intersect.Editor.Forms.Styling;
using Intersect.Editor.General;
using Intersect.Localization;

internal static class EditorHubTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    internal static void Run()
    {
        TestPreferences(); TestRegistry(); TestLocalizationAndArt(); TestIntegratedWorkspace();
        Console.WriteLine("PASS Editor Hub: exhaustive live-menu coverage, original command dispatch, live additions, bilingual/accent search, filters, preferences, real recents, disposal, embedded artwork, keyboard and four responsive sizes");
    }
    private static void TestPreferences()
    {
        var saved = "";
        var first = new HubPreferences(_ => "", (_, value) => saved = value);
        first.ToggleFavorite("npcEditorToolStripMenuItem"); first.ToggleSidebar();
        first.Record("itemEditorToolStripMenuItem"); first.Record("npcEditorToolStripMenuItem"); first.Record("itemEditorToolStripMenuItem");
        var restored = new HubPreferences(_ => saved, (_, _) => { });
        Check(restored.Collapsed && restored.Favorites.SetEquals(["npcEditorToolStripMenuItem"]), "Favorites and sidebar restore from the existing preference mechanism.");
        Check(restored.Recent.Count == 2 && restored.Recent[0].Id == "itemEditorToolStripMenuItem", "Recent tools retain real IDs in usage order without duplicates.");
        var corrupt = new HubPreferences(_ => "broken json", (_, _) => { });
        Check(corrupt.Favorites.Count == 0 && corrupt.Recent.Count == 0, "Corrupt optional preferences cannot prevent startup.");
        var partial = new HubPreferences(_ => "{\"Favorites\":[null,\"map\"],\"Recent\":[null]}", (_, _) => { });
        Check(partial.Favorites.SetEquals(["map"]) && partial.Recent.Count == 0, "Partial or null entries are ignored.");
    }
    private static void TestRegistry()
    {
        var opens = 0; var records = new List<string>();
        using var menu = new ToolStripMenuItem("Editors") { Name = "contentEditorsToolStripMenuItem" };
        using var nested = new ToolStripMenuItem("Nested") { Name = "nested" };
        var npc = new ToolStripMenuItem("NPC") { Name = "npcEditorToolStripMenuItem" };
        npc.Click += (_, _) => opens++; nested.DropDownItems.Add(npc); menu.DropDownItems.Add(nested);
        var registry = new HubRegistry([menu], records.Add);
        Check(registry.Open(npc.Name) && opens == 1 && records.SequenceEqual([npc.Name]), "A hub card invokes the original menu handler exactly once.");
        npc.PerformClick(); Check(opens == 2 && records.Count == 2, "Legacy menu access also records real history.");
        npc.Enabled = false; Check(!registry.Open(npc.Name) && opens == 2, "Unavailable commands remain protected."); npc.Enabled = true;
        Check(registry.Search("PNJ").Single().Id == npc.Name && registry.Search("npc").Single().Id == npc.Name, "French and English synonyms resolve the same entry.");
        var item = new ToolStripMenuItem("Items") { Name = "itemEditorToolStripMenuItem" }; nested.DropDownItems.Add(item);
        Check(registry.Find(item.Name) != null && registry.Search("OBJET", tag: "Economy").Single().Id == item.Name, "Runtime additions and search/filter combinations work.");
        Check(registry.Search("equipements").Any(e => e.Id == item.Name), "Search ignores French accents.");
        Check(registry.Search("no-such-tool").Length == 0 && registry.Search("", "Favorites", favorites: new HashSet<string> { item.Name }).Single().Id == item.Name,
            "Empty results and favorite filtering are accurate.");
        nested.DropDownItems.Remove(item);
        Check(registry.Find(item.Name) == null, "Removed tools disappear from search instead of leaving dead cards.");
        using (var host = new Form { Opacity = 0, ShowInTaskbar = false })
        using (var view = new EditorHubView(registry, new HubPreferences(_ => "", (_, _) => { })) { Dock = DockStyle.Fill })
        {
            host.Controls.Add(view); host.Show(); view.SetQuery("npc"); Application.DoEvents();
            var canvas = view.Controls.Cast<Control>().SelectMany(Descendants).Single(c => c.GetType().Name == "Canvas");
            var prior = opens;
            canvas.GetType().GetMethod("OnKeyDown", Hidden)!.Invoke(canvas, [new KeyEventArgs(Keys.Down)]);
            canvas.GetType().GetMethod("OnKeyDown", Hidden)!.Invoke(canvas, [new KeyEventArgs(Keys.Enter)]);
            Check(opens == prior + 1, "Arrow navigation and Enter activate the selected real command.");
            canvas.GetType().GetMethod("OnKeyDown", Hidden)!.Invoke(canvas, [new KeyEventArgs(Keys.Space)]);
            Check(view.Preferences.Favorites.Contains(npc.Name), "Space toggles the focused card favorite.");
            var closed = 0; view.CloseRequested += () => closed++;
            view.SetQuery("npc");
            var escape = typeof(EditorHubView).GetMethod("ProcessCmdKey", Hidden)!;
            escape.Invoke(view, [Message.Create(view.Handle, 0x100, IntPtr.Zero, IntPtr.Zero), Keys.Escape]);
            Check(closed == 0 && view.CurrentEntries().Length > 0, "Escape first clears the current search.");
            escape.Invoke(view, [Message.Create(view.Handle, 0x100, IntPtr.Zero, IntPtr.Zero), Keys.Escape]);
            Check(closed == 1, "Escape closes the hub once search is empty.");
            host.Hide();
        }
        var recorded = records.Count; var beforeDispose = opens;
        registry.Dispose(); npc.PerformClick(); Check(opens == beforeDispose + 1 && records.Count == recorded, "Disposal removes hub handlers without breaking historical commands.");
    }
    private static IEnumerable<Control> Descendants(Control root) { yield return root; foreach (Control child in root.Controls) foreach (var item in Descendants(child)) yield return item; }
    private static void TestLocalizationAndArt()
    {
        Check(HubRegistry.Definitions.Length > 0 && HubRegistry.Definitions.Select(d => d.Id).Distinct().Count() == HubRegistry.Definitions.Length,
            "Every built-in navigation entry has a unique ID.");
        var text = HubText.Read<Dictionary<string, Dictionary<string, string>>>("Texts.json");
        using var art = new HubArt();
        foreach (var entry in HubRegistry.Definitions)
        {
            foreach (var language in new[] { "en", "fr", "es" })
                Check(!string.IsNullOrWhiteSpace(entry.Title[language]) && !string.IsNullOrWhiteSpace(entry.Description[language]), "Every editor title and description is localized: " + entry.Id);
            Check(art.Get(entry.Art) is { Width: 256, Height: 96 }, "Every built-in card has independent embedded artwork: " + entry.Id);
            Check(ReferenceEquals(art.Get(entry.Art), art.Get(entry.Art)), "Images are cached rather than reloaded.");
            Check(HubText.Localize(entry.Title, "unknown") == entry.Title["en"], "Editor names have English fallback.");
        }
        foreach (var pair in text)
            foreach (var language in new[] { "en", "fr", "es" })
                Check(!string.IsNullOrWhiteSpace(pair.Value[language]) && GameTextTokens.Compatible(pair.Value["en"], pair.Value[language]), "Hub labels have compatible translations: " + pair.Key);
        Check(art.Get("banner.png") is { Width: 720 }, "The banner is packaged with the application.");
    }
    private static void TestIntegratedWorkspace()
    {
        var oldCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr");
        try
        {
            using var main = new FrmMain { Opacity = 0, ShowInTaskbar = false, MaximumSize = new Size(3000, 2000) };
            RemoveLoad(main, "frmMain_Load"); RemoveLoad(Globals.MapLayersWindow, "frmMapLayers_Load");
            RemoveLoad(Globals.MapGridWindowNew, "frmMapGrid_Load"); RemoveLoad(Globals.MapEditorWindow, "frmMapEditor_Load");
            main.FormClosing -= (FormClosingEventHandler)Delegate.CreateDelegate(typeof(FormClosingEventHandler), main, typeof(FrmMain).GetMethod("frmMain_FormClosing", Hidden)!);
            typeof(FrmMain).GetMethod("InitializeEditorNavigation", Hidden)!.Invoke(main, [false]);
            main.Show(); EditorTheme.Apply(main); Application.DoEvents();
            var reminder = new ToolStrip { Dock = DockStyle.Top, Visible = false, Height = 25 };
            reminder.Items.Add(new ToolStripLabel("Save warning fixture")); main.Controls.Add(reminder); reminder.BringToFront();
            typeof(FrmMain).GetField("_saveReminder", Hidden)!.SetValue(main, reminder);
            typeof(FrmMain).GetMethod("InitializeEditorHub", Hidden)!.Invoke(main, [new HubPreferences(_ => "", (_, _) => { })]);
            var menubar = (MenuStrip)typeof(FrmMain).GetField("menuStrip", Hidden)!.GetValue(main)!;
            var toolbar = (ToolStrip)typeof(FrmMain).GetField("toolStrip1", Hidden)!.GetValue(main)!;
            Console.WriteLine($"Hub chrome: menu visible={menubar.Visible}, menu={menubar.Bounds}, toolbar={toolbar.Bounds}");
            var registry = (HubRegistry)typeof(FrmMain).GetField("_hubRegistry", Hidden)!.GetValue(main)!;
            var hub = (EditorHubView)typeof(FrmMain).GetField("_editorHub", Hidden)!.GetValue(main)!;
            Check(HubRegistry.Definitions.All(d => registry.Find(d.Id) != null), "Every catalog entry is backed by a real current menu command or map workspace action.");
            Check(registry.Entries.Select(e => e.Id).ToHashSet().SetEquals(HubRegistry.Definitions.Select(d => d.Id)),
                "The live menu and catalog have the same identities without unidentified duplicates. Extra: " + string.Join(", ", registry.Entries.Select(e => e.Id).Except(HubRegistry.Definitions.Select(d => d.Id))));
            reminder.Visible = true; Application.DoEvents(); main.PerformLayout();
            Check(hub.Top >= reminder.Bottom, "The hub follows save-reminder visibility and geometry without covering it.");
            reminder.Visible = false; Application.DoEvents(); main.PerformLayout();
            var parent = Globals.MapEditorWindow.picMap.Parent; var map = Globals.CurrentMap;
            var undoCount = Globals.MapEditorWindow.MapUndoStates.Count;
            foreach (var size in new[] { new Size(1920,1080), new Size(1600,900), new Size(1366,768), new Size(1280,720) })
            {
                SetWindowPos(main.Handle, IntPtr.Zero, 0, 0, size.Width, size.Height, 0x416); Check(main.Size == size, "The test window must really use the requested resolution: " + size); main.ShowEditorHub(); Application.DoEvents(); main.PerformLayout();
                Check(menubar.Visible && hub.Top >= Math.Max(menubar.Bottom, toolbar.Bottom), "The hub cannot cover the historical menus or toolbar.");
                Check(hub.Visible && main.ClientRectangle.Contains(hub.Bounds), "Hub fits the workspace at " + size);
                Check(ReferenceEquals(parent, Globals.MapEditorWindow.picMap.Parent) && ReferenceEquals(map, Globals.CurrentMap) && Globals.MapEditorWindow.MapUndoStates.Count == undoCount,
                    "Opening/resizing the hub never reparents or reloads the map or resets its undo history.");
                Check(hub.CurrentEntries().Length == 8, "The home page provides eight working primary cards.");
                Capture(main, hub, size.Width + "x" + size.Height);
                foreach (var category in new[] { "Game", "System", "MiniGames", "Events", "Tools" })
                { hub.SetCategory(category); Application.DoEvents(); Check(hub.CurrentEntries().Length > 0, "Each category has real tools: " + category); }
                hub.SetCategory("Home");
            }
            hub.SetQuery("npc"); Check(hub.CurrentEntries().Any(e => e.Id == "npcEditorToolStripMenuItem"), "Search reaches actual integrated entries.");
            var key = Message.Create(hub.Handle, 0x100, (IntPtr)Keys.K, IntPtr.Zero);
            typeof(FrmMain).GetMethod("ProcessCmdKey", Hidden)!.Invoke(main, [key, Keys.Control | Keys.K]);
            Check(main.ContainsFocus, "Ctrl+K can focus search from the main workspace.");
            hub.SetQuery(""); hub.Preferences.ToggleSidebar(); Application.DoEvents(); Capture(main, hub, "collapsed"); hub.Preferences.ToggleSidebar();
            typeof(FrmMain).GetMethod("ReturnToMap", Hidden)!.Invoke(main, null);
            Check(!hub.Visible && ReferenceEquals(map, Globals.CurrentMap) && ReferenceEquals(parent, Globals.MapEditorWindow.picMap.Parent), "Closing the hub returns to the existing map.");
            main.Hide();
        }
        finally { CultureInfo.CurrentUICulture = oldCulture; }
    }
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    private static void RemoveLoad(Form form, string method) => form.Load -= (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), form, form.GetType().GetMethod(method, Hidden)!);
    private static void Capture(Form main, EditorHubView hub, string name)
    {
        if (Environment.GetEnvironmentVariable("CODEX_HUB_PREVIEW") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder);
        using var image = new Bitmap(main.Width, main.Height); main.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
        // Native MDI children may be omitted; paint the actual sibling Hub explicitly.
        using var view = new Bitmap(hub.Width, hub.Height); hub.DrawToBitmap(view, new Rectangle(Point.Empty, view.Size));
        using var graphics = Graphics.FromImage(image); var position = main.PointToClient(hub.PointToScreen(Point.Empty));
        var frame = (main.Width - main.ClientSize.Width) / 2;
        position.Offset(frame, main.Height - main.ClientSize.Height - frame);
        graphics.DrawImageUnscaled(view, position);
        image.Save(Path.Combine(folder, "editor-hub-" + name + ".png"));
    }
}
