using Intersect.Editor.Core;
using Newtonsoft.Json;
using static Intersect.Editor.Localization.EditorSafetyText;

namespace Intersect.Editor.Forms.Editors.Events;

public partial class FrmEvent
{
    private readonly Label _recoveryStatus = new() { AutoSize = true, Padding = new Padding(6, 5, 0, 0) };
    private DateTime? _eventDirtySince;
    internal bool HasRecoveryChanges => MyEvent != null && MyEvent.Id != Guid.Empty &&
        mEventBackup != null && MyEvent.JsonData != mEventBackup;

    private void InitializeEventRecovery(FlowLayoutPanel footer)
    {
        var recover = new Button { Name = "RecoverEvent", AutoSize = true, Text = Get("Recover local event copy...") };
        recover.Click += (_, _) => RecoverEvent();
        footer.Controls.Add(recover);
        footer.Controls.Add(_recoveryStatus);
    }

    internal void CaptureRecovery()
    {
        if (!HasRecoveryChanges) { _eventDirtySince = null; return; }
        _eventDirtySince ??= DateTime.UtcNow;
        // Command controls may contain a draft not yet applied to MyEvent. Do not click Save/Cancel
        // on behalf of the author; save only the current event model and explain that boundary.
        EditorRecoveryStore.Current.Write(new() { Kind = "event", Id = MyEvent.Id, Name = MyEvent.Name, EventJson = MyEvent.JsonData });
        _recoveryStatus.Text = Get("Local copy: {0}", DateTime.Now.ToShortTimeString());
        if (Preferences.SaveReminders && DateTime.UtcNow - _eventDirtySince.Value >= TimeSpan.FromMinutes(5))
            _recoveryStatus.Text = Get("Unsaved event: confirm commands, then Save.");
    }

    private void RecoverEvent()
    {
        if (MyEvent == null || !btnSave.Enabled) return;
        var draft = EditorRecoveryStore.Current.Read("event", MyEvent.Id);
        if (draft?.EventJson == null) { MessageBox.Show(this, Get("No local recovery copy for this event."), Get("Save protection")); return; }
        if (MessageBox.Show(this, Get("Restore the event copy from {0}? Current event edits will be replaced locally.",
                draft.SavedUtc.ToLocalTime()), Get("Save protection"), MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        var original = MyEvent.JsonData;
        var backup = mEventBackup;
        try
        {
            MyEvent.Load(draft.EventJson);
            InitEditor(!txtEventname.Enabled, !grpTriggers.Visible, MyEvent.Id == Guid.Empty);
            mEventBackup = backup;
            _recoveryStatus.Text = Get("Local copy restored; save to publish.");
        }
        catch (Exception e) when (e is JsonException or ArgumentException)
        { MyEvent.Load(original); MessageBox.Show(this, Get("Recovery failed: {0}", e.Message), Get("Save protection")); }
    }
}
