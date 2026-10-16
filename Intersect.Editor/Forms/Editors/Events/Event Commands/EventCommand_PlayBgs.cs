using Intersect.Editor.Content;
using Intersect.Editor.Localization;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Utilities;

namespace Intersect.Editor.Forms.Editors.Events.Event_Commands;


public partial class EventCommandPlayBgs : UserControl
{

    private readonly FrmEvent mEventEditor;

    private PlaySoundCommand mMyCommand;
    private readonly CheckBox _voiceSound = new() { Text = "Voice Sound", AutoSize = true, Location = new System.Drawing.Point(7, 54) };

    public EventCommandPlayBgs(PlaySoundCommand refCommand, FrmEvent editor)
    {
        InitializeComponent();
        mMyCommand = refCommand;
        mEventEditor = editor;
        InitLocalization();
        _voiceSound.Checked = mMyCommand.VoiceSound;
        grpPlayBGS.Controls.Add(_voiceSound);
        cmbSound.DropDownWidth = 350;
        _voiceSound.CheckedChanged += (_, _) => ReloadSoundList();
        ReloadSoundList();
    }

    private void ReloadSoundList()
    {
        grpPlayBGS.Text = _voiceSound.Checked ? "Play Voice Sound" : "Play Sound";
        cmbSound.Items.Clear();
        cmbSound.Items.Add(Strings.General.None);
        cmbSound.Items.AddRange(_voiceSound.Checked ? GameContentManager.SmartSortedVoiceSoundNames : GameContentManager.SmartSortedSoundNames);
        if (cmbSound.Items.IndexOf(TextUtils.NullToNone(mMyCommand.File)) > -1)
        {
            cmbSound.SelectedIndex = cmbSound.Items.IndexOf(TextUtils.NullToNone(mMyCommand.File));
        }
        else
        {
            cmbSound.SelectedIndex = 0;
        }
    }

    private void InitLocalization()
    {
        grpPlayBGS.Text = Strings.EventPlayBgs.title;
        lblSound.Text = Strings.EventPlayBgs.label;
        btnSave.Text = Strings.EventPlayBgs.okay;
        btnCancel.Text = Strings.EventPlayBgs.cancel;
    }

    private void btnSave_Click(object sender, EventArgs e)
    {
        mMyCommand.File = TextUtils.SanitizeNone(cmbSound?.Text);
        mMyCommand.VoiceSound = _voiceSound.Checked;
        mEventEditor.FinishCommandEdit();
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        mEventEditor.CancelCommandEdit();
    }

    private void cmbSprite_SelectedIndexChanged(object sender, EventArgs e)
    {
    }

}
