using Intersect.Editor.Forms.EditorHub;
using Intersect.Editor.Networking;
using Intersect.Editor.General;
using Intersect.Editor.Forms.Styling;

namespace Intersect.Editor.Forms;

public partial class FrmMain
{
    private EditorHubView? _editorHub;
    private HubRegistry? _hubRegistry;
    private HubPreferences? _hubPreferences;

    private void InitializeEditorNavigation(bool requestConfigurations = true)
    {
        AddRewardConfigurationEditorMenu();
        AddLogiCoinShopEditorMenu();
        AddProfessionEditorMenu();
        if (requestConfigurations) PacketSender.SendRequestProfessionConfiguration(openEditor: false);
        AddAchievementEditorMenu();
        AddMountEditorMenu();
        AddJoustEditorMenu();
        AddRoyalCombatEditorMenus();
        if (requestConfigurations) PacketSender.SendRequestAchievementConfiguration(openEditor: false);
        AddDungeonEditorMenu();
        if (requestConfigurations) PacketSender.SendRequestDungeonConfiguration(openEditor: false);
        AddQuestShopEditorMenu();
        if (requestConfigurations) PacketSender.SendRequestQuestShopConfiguration(openEditor: false);
        var housingEditor = new ToolStripMenuItem("Housing & Furniture Catalog") { Name = "housingEditor" };
        housingEditor.Click += (_, _) => PacketSender.RequestHousingConfiguration();
        contentEditorsToolStripMenuItem.DropDownItems.Add(housingEditor);
        AddRoyalStylistEditorMenu();
        if (requestConfigurations) PacketSender.SendRequestRoyalStylistConfiguration(openEditor: false);
        AddCharacterGeneratorEditorMenu();
        AddAnimationImportEditorMenu();
        AddSoundImportEditorMenu();
        AddCinematicDirectorMenu();
        AddBalanceLabEditorMenu();
        AddWorldEventsEditorMenu();
        AddSystemEditorMenu();
        AddWorldMapEditorMenu();
        var resourceBrush=new ToolStripMenuItem(Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get("Resource brush",System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)){Name="ResourceBrushTool"};
        resourceBrush.Click+=(_,_)=>{Globals.MapLayersWindow.Show();Globals.MapLayersWindow.ActivateResourceBrush();};
        toolsToolStripMenuItem.DropDownItems.Add(resourceBrush);
        var uiDesigner=new ToolStripMenuItem(Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get("UI Editor",System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName));
        uiDesigner.Name="uiDesigner";
        uiDesigner.Click+=(_,_)=>new Intersect.Editor.Forms.Editors.FrmUiDesigner().Show(this);
        toolsToolStripMenuItem.DropDownItems.Add(uiDesigner);
    }

    private void InitializeEditorHub(HubPreferences? preferences = null)
    {
        if (_editorHub != null) return;
        _hubPreferences = preferences ?? new HubPreferences();
        var rootNames = new[] { "contentEditorsToolStripMenuItem", "miniGameEditorsToolStripMenuItem", "worldEventsToolStripMenuItem", "systemEditorToolStripMenuItem", "toolsToolStripMenuItem" };
        var roots = menuStrip.Items.OfType<ToolStripMenuItem>().Where(item => rootNames.Contains(item.Name)).ToArray();
        _hubRegistry = new HubRegistry(roots, id => _hubPreferences.Record(id));
        HubDefinition Definition(string id) => HubRegistry.Definitions.Single(d => d.Id == id);
        _hubRegistry.Register(new(Definition("map"), ReturnToMap, () => Globals.MapEditorWindow is { IsDisposed: false }));
        _hubRegistry.Register(new(Definition("mapEvents"), ActivateMapEventCreation, () => Globals.CurrentMap != null));
        _editorHub = new EditorHubView(_hubRegistry, _hubPreferences) { Visible = false };
        _editorHub.CanEditMap = () => Globals.CurrentMap != null;
        _editorHub.CloseRequested += ReturnToMap;
        _editorHub.NewMapRequested += () => { if (Globals.CurrentMap == null) return; ReturnToMap(); newMapToolStripMenuItem.PerformClick(); };
        _editorHub.SaveMapRequested += () => { if (Globals.CurrentMap != null) toolStripBtnSaveMap.PerformClick(); };
        _editorHub.MapEventRequested += ActivateMapEventCreation;
        Controls.Add(_editorHub);
        // Top-docked chrome is laid out in reverse z-order. Keep it above the workspace.
        Controls.SetChildIndex(menuStrip, Controls.Count - 1);
        Controls.SetChildIndex(toolStrip1, Controls.Count - 2);
        var hubMenu = new ToolStripMenuItem(HubText.T("Hub")) { Name = "editorHubToolStripMenuItem", ShortcutKeys = Keys.Control | Keys.K };
        hubMenu.Click += (_, _) => ShowEditorHub();
        menuStrip.Items.Insert(Math.Min(3, menuStrip.Items.Count), hubMenu);
        var button = new ToolStripButton(HubText.T("Hub")) { Name = "editorHubToolbar", DisplayStyle = ToolStripItemDisplayStyle.Text,
            ToolTipText = HubText.T("Hub") + " (Ctrl+K)" };
        button.Click += (_, _) => ShowEditorHub();
        toolStrip1.Items.Insert(0, button); toolStrip1.Items.Insert(1, new ToolStripSeparator());
        Resize += (_, _) => PositionEditorHub();
        dockLeft.LocationChanged += (_, _) => PositionEditorHub();
        dockLeft.SizeChanged += (_, _) => PositionEditorHub();
        if (_saveReminder != null)
        {
            _saveReminder.VisibleChanged += (_, _) => PositionEditorHub();
            _saveReminder.LocationChanged += (_, _) => PositionEditorHub();
            _saveReminder.SizeChanged += (_, _) => PositionEditorHub();
        }
        Disposed += (_, _) => _hubRegistry.Dispose();
        ShowEditorHub(focusSearch: false);
    }

    private void PositionEditorHub()
    {
        if (_editorHub == null) return;
        var area = dockLeft.Bounds;
        if (_saveReminder is { Visible: true } reminder && reminder.Bottom > area.Top)
        { var bottom = area.Bottom; area.Y = reminder.Bottom; area.Height = Math.Max(0, bottom - area.Y); }
        // This sibling overlay never reparents the map's swap chain or docking panels.
        var width = Math.Min(area.Width, Math.Min(1480, Math.Max(760, area.Width - Math.Max(240, area.Width / 5))));
        _editorHub.Bounds = new Rectangle(area.X, area.Y, width, area.Height);
        if (_editorHub.Visible) _editorHub.BringToFront();
    }

    internal void ShowEditorHub(bool focusSearch = true)
    {
        if (_editorHub == null) return;
        _hubRegistry?.Refresh(); PositionEditorHub(); _editorHub.Show(); _editorHub.BringToFront();
        if (focusSearch) _editorHub.FocusSearch();
    }

    private void ReturnToMap()
    {
        _editorHub?.Hide();
        if (Globals.MapEditorWindow is { IsDisposed: false } map) { map.Show(); map.Activate(); }
    }
    private void ActivateMapEventCreation()
    {
        ReturnToMap();
        Globals.MapLayersWindow?.ActivateEvents();
        Globals.MapLayersWindow?.Show();
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.K) && _editorHub != null) { ShowEditorHub(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
