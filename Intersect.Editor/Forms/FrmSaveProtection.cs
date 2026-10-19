using DarkUI.Forms;
using Intersect.Editor.Core;
using Intersect.Editor.Forms.Editors;
using static Intersect.Editor.Localization.EditorSafetyText;

namespace Intersect.Editor.Forms;

internal sealed class FrmSaveProtection : DarkForm
{
    internal FrmSaveProtection()
    {
        Text = Get("Save protection");
        Size = new Size(700, 410);
        MinimumSize = Size;
        StartPosition = FormStartPosition.CenterParent;
        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true, Padding = new Padding(16) };
        var reminders = new CheckBox { Name = "SaveReminders", AutoSize = true,
            Text = Get("Remind me after five minutes of unsaved changes"), Checked = Preferences.SaveReminders };
        var autosave = new CheckBox { Name = "AutoSaveMaps", AutoSize = true,
            Text = Get("Automatically save maps and completed map events to the server"), Checked = Preferences.AutoSaveMaps };
        var label = new Label { AutoSize = true, Text = Get("Automatic save interval (minutes)") };
        var interval = new NumericUpDown { Name = "AutoSaveMinutes", Minimum = 1, Maximum = 30, Value = Preferences.AutoSaveMinutes };
        var help = new Label { AutoSize = true, MaximumSize = new Size(620, 0), Text = Get(
            "Local recovery copies are made every 30 seconds for changed maps and open events. Server autosave waits until event windows are closed and drawing or property editing has stopped. Changes become live on the server. Use File > Recover local map copy, or Recover in the event window. Unsaved command dialogs must be confirmed first.") };
        var path = new TextBox { ReadOnly = true, Width = 620, Text = EditorRecoveryStore.ServerDirectory };
        var save = new Button { Text = Get("Save settings"), AutoSize = true };
        save.Click += (_, _) => { Preferences.SaveReminders = reminders.Checked; Preferences.AutoSaveMaps = autosave.Checked;
            Preferences.AutoSaveMinutes = (int)interval.Value; DialogResult = DialogResult.OK; Close(); };
        layout.Controls.AddRange([reminders, autosave, label, interval, help, path, save]);
        Controls.Add(layout);
        MiniGameEditorTheme.Apply(this);
    }
}
