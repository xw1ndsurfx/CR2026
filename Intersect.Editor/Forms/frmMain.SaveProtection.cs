using DarkUI.Forms;
using Intersect.Editor.Core;
using Intersect.Editor.Forms.Editors.Events;
using Intersect.Editor.General;
using Intersect.Editor.Maps;
using Newtonsoft.Json;
using static Intersect.Editor.Localization.EditorSafetyText;

namespace Intersect.Editor.Forms;

public partial class FrmMain
{
    private DateTime _nextRecovery = DateTime.MinValue;
    private DateTime _nextAutoSave = DateTime.MinValue;
    private DateTime? _dirtySince;
    private Guid _trackedMap;
    private Guid? _closeAfterSave;
    private DateTime _closeDeadline;
    private readonly ToolStripStatusLabel _saveStatus = new() { IsLink = true };
    private ToolStrip? _saveReminder;

    private void InitializeSaveProtection()
    {
        var settings = new ToolStripMenuItem(Get("Save protection..."));
        settings.Click += (_, _) => { using var form = new FrmSaveProtection(); form.ShowDialog(this);
            _nextAutoSave = DateTime.UtcNow.AddMinutes(Preferences.AutoSaveMinutes); };
        var recover = new ToolStripMenuItem(Get("Recover local map copy..."));
        recover.Click += (_, _) => RecoverMap();
        fileToolStripMenuItem.DropDownItems.AddRange([new ToolStripSeparator(), settings, recover]);
        _saveStatus.Text = Get("Local recovery ready");
        _saveStatus.Click += (_, _) => { using var form = new FrmSaveProtection(); form.ShowDialog(this); };
        statusStrip1.Items.Add(_saveStatus);
        _saveReminder = new ToolStrip { Dock = DockStyle.Top, Visible = false, BackColor = System.Drawing.Color.FromArgb(105, 75, 20) };
        _saveReminder.Items.Add(new ToolStripLabel(Get("Unsaved changes: save your map and finish open event edits.")));
        var save = new ToolStripButton(Get("Save now"));
        save.Click += (_, _) =>
        {
            var editor = Application.OpenForms.OfType<FrmEvent>().FirstOrDefault(f => f.Visible);
            if (editor != null) { editor.Activate(); return; }
            if (Globals.CurrentMap != null && Networking.Network.Connected) SaveMap();
        };
        _saveReminder.Items.Add(save);
        Controls.Add(_saveReminder);
        _saveReminder.BringToFront();
        var timer = new System.Windows.Forms.Timer(components) { Interval = 1000 };
        timer.Tick += (_, _) => SaveProtectionTick();
        timer.Start();
    }

    internal static void CaptureMapRecovery(MapInstance? map)
    {
        if (map == null || !map.Changed()) return;
        EditorRecoveryStore.Current.Write(new() { Id = map.Id, Name = map.Name, Map = map.SaveInternal() });
    }

    internal static bool TryCaptureBeforeExit()
    {
        try
        {
            foreach (var editor in Application.OpenForms.OfType<FrmEvent>().ToArray()) editor.CaptureRecovery();
            CaptureMapRecovery(Globals.CurrentMap);
            return true;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        {
            if (Globals.MainForm != null) Globals.MainForm._saveStatus.Text = Get("Local recovery failed: {0}", e.Message);
            return false;
        }
    }

    // UI timer only: snapshots and control state must be read on the editor thread.
    private void SaveProtectionTick()
    {
        if (Globals.ClosingEditor || Globals.FetchingMapPreviews) return;
        var now = DateTime.UtcNow;
        if (_closeAfterSave.HasValue)
        {
            if (Globals.CurrentMap?.Id == _closeAfterSave && !Globals.CurrentMap.Changed())
            { _closeAfterSave = null; Close(); return; }
            if (now >= _closeDeadline)
            {
                _closeAfterSave = null;
                MessageBox.Show(this, Get("The server has not confirmed the save. The editor stays open; a local recovery copy is available."), Get("Save protection"));
            }
        }
        var events = Application.OpenForms.OfType<FrmEvent>().Where(f => f.Visible).ToArray();
        var map = Globals.CurrentMap;
        if (_trackedMap != map?.Id) { _trackedMap = map?.Id ?? Guid.Empty; _dirtySince = null;
            _nextAutoSave = now.AddMinutes(Preferences.AutoSaveMinutes); }
        // Avoid a full map serialization every second and never snapshot a half-painted selection.
        if (now < _nextRecovery || Globals.MouseButton != -1 || Globals.Dragging) return;
        _nextRecovery = now.AddSeconds(30);
        try
        {
            foreach (var editor in events) editor.CaptureRecovery();
            CaptureMapRecovery(map);
            var dirty = map?.Changed() == true || events.Any(e => e.HasRecoveryChanges);
            _dirtySince = dirty ? _dirtySince ?? now : null;
            if (_saveReminder != null) _saveReminder.Visible = Preferences.SaveReminders &&
                _dirtySince.HasValue && now - _dirtySince.Value >= TimeSpan.FromMinutes(5);
            if (dirty) _saveStatus.Text = Get("Local recovery: {0}", DateTime.Now.ToShortTimeString());
            if (!Preferences.AutoSaveMaps || !Networking.Network.Connected || !dirty || now < _nextAutoSave) return;
            // Sending a map while a modal event editor is open would publish unfinished edits and
            // let the server echo detach the editor from its map. Drafts protect that work instead.
            if (events.Length != 0 || Globals.MapPropertiesWindow?.ContainsFocus == true ||
                Application.OpenForms.Cast<Form>().Any(f => f != this && f.Visible && f.Modal))
            { _saveStatus.Text = Get("Local copy saved; server autosave waits for open editors."); return; }
            if (map?.Changed() == true)
            {
                SaveMap();
                _saveStatus.Text = Get("Automatic save sent to server at {0}", DateTime.Now.ToShortTimeString());
            }
            _nextAutoSave = now.AddMinutes(Preferences.AutoSaveMinutes);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        { _saveStatus.Text = Get("Local recovery failed: {0}", e.Message); }
    }

    private void RecoverMap()
    {
        var map = Globals.CurrentMap;
        if (map == null) return;
        if (Application.OpenForms.OfType<FrmEvent>().Any(f => f.Visible))
        { MessageBox.Show(this, Get("Close the event editor before restoring a map copy."), Get("Save protection")); return; }
        var draft = EditorRecoveryStore.Current.Read("map", map.Id);
        if (draft?.Map == null) { MessageBox.Show(this, Get("No local recovery copy for this map."), Get("Save protection")); return; }
        if (MessageBox.Show(this, Get("Restore the local copy of {0} from {1}? Current edits will be replaced locally. Save the map to publish it.",
                draft.Name, draft.SavedUtc.ToLocalTime()), Get("Save protection"), MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        try
        {
            var previous = map.SaveInternal();
            try { map.LoadInternal(draft.Map, import: true); }
            catch { map.LoadInternal(previous, import: true); throw; }
            Globals.MapEditorWindow?.MapUndoStates.Add(previous);
            Globals.MapEditorWindow?.MapRedoStates.Clear();
            if (Globals.MapEditorWindow != null) Globals.MapEditorWindow.CurrentMapState = map.SaveInternal();
            Globals.MapPropertiesWindow?.Update();
            Core.Graphics.TilePreviewUpdated = true;
            _saveStatus.Text = Get("Local copy restored; save to publish.");
        }
        catch (Exception e) when (e is IOException or JsonException or ArgumentException)
        { MessageBox.Show(this, Get("Recovery failed: {0}", e.Message), Get("Save protection")); }
    }
}
