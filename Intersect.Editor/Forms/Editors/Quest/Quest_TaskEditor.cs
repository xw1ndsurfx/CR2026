using Intersect.Editor.Forms.Editors.Events;
using Intersect.Editor.General;
using Intersect.Editor.Localization;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.Framework.Core.MiniGames;
using Intersect.GameObjects;
using Microsoft.Extensions.Logging;


namespace Intersect.Editor.Forms.Editors.Quest;


public partial class QuestTaskEditor : UserControl
{

    public bool Cancelled;

    private string mEventBackup = null;

    private QuestDescriptor mMyQuest;

    private QuestTaskDescriptor mMyTask;

    private readonly List<Guid> mMiniGameCurrencyIds = new();
    private DarkUI.Controls.DarkGroupBox mGrpMiniGame = null!;
    private DarkUI.Controls.DarkComboBox mCmbMiniGame = null!;
    private DarkUI.Controls.DarkComboBox mCmbMiniGameCurrency = null!;
    private DarkUI.Controls.DarkNumericUpDown mNudMiniGameAmount = null!;
    private Label mLblMiniGame = null!;
    private Label mLblMiniGameCurrency = null!;

    public QuestTaskEditor(QuestDescriptor refQuest, QuestTaskDescriptor refTask)
    {
        if (refQuest == null)
        {
            Intersect.Core.ApplicationContext.Context.Value?.Logger.LogWarning($@"{nameof(refQuest)} is null.");
        }

        if (refTask == null)
        {
            Intersect.Core.ApplicationContext.Context.Value?.Logger.LogWarning($@"{nameof(refTask)} is null.");
        }

        InitializeComponent();
        InitializeMiniGameControls();
        mMyTask = refTask;
        mMyQuest = refQuest;

        if (mMyTask?.EditingEvent == null)
        {
            Intersect.Core.ApplicationContext.Context.Value?.Logger.LogWarning($@"{nameof(mMyTask.EditingEvent)} is null.");
        }

        mEventBackup = mMyTask?.EditingEvent?.JsonData;
        InitLocalization();
        cmbTaskType.SelectedIndex = mMyTask == null ? -1 : (int) mMyTask.Objective;
        txtStartDesc.Text = mMyTask?.Description;
        UpdateFormElements();
        switch (cmbTaskType.SelectedIndex)
        {
            case 0: //Event Driven
                break;
            case 1: //Gather Items
                cmbItem.SelectedIndex = ItemDescriptor.ListIndex(mMyTask?.TargetId ?? Guid.Empty);
                nudItemAmount.Value = mMyTask?.Quantity ?? 0;

                break;
            case 2: //Kill NPCS
                cmbNpc.SelectedIndex = NPCDescriptor.ListIndex(mMyTask?.TargetId ?? Guid.Empty);
                nudNpcQuantity.Value = mMyTask?.Quantity ?? 0;

                break;
            case 3:
            case 4:
            case 5:
                mCmbMiniGame.SelectedIndex = 0;
                mNudMiniGameAmount.Value = Math.Clamp(mMyTask?.Quantity ?? 1, 1, int.MaxValue);
                if (cmbTaskType.SelectedIndex == 4)
                {
                    var currencyIndex = mMiniGameCurrencyIds.IndexOf(mMyTask?.TargetId ?? Guid.Empty);
                    mCmbMiniGameCurrency.SelectedIndex = currencyIndex >= 0 ? currencyIndex : 0;
                }
                break;
        }
    }

    private void InitLocalization()
    {
        grpEditor.Text = Strings.TaskEditor.editor;

        lblType.Text = Strings.TaskEditor.type;
        cmbTaskType.Items.Clear();
        for (var i = 0; i < Strings.TaskEditor.types.Count; i++)
        {
            cmbTaskType.Items.Add(Strings.TaskEditor.types[i]);
        }
        while (cmbTaskType.Items.Count < 3) cmbTaskType.Items.Add("Task");
        cmbTaskType.Items.Add("Mini-game: Win games");
        cmbTaskType.Items.Add("Mini-game: Win amount");
        cmbTaskType.Items.Add("Mini-game: Reach level");

        lblDesc.Text = Strings.TaskEditor.desc;

        grpKillNpcs.Text = Strings.TaskEditor.killnpcs;
        lblNpc.Text = Strings.TaskEditor.npc;
        lblNpcQuantity.Text = Strings.TaskEditor.npcamount;

        grpGatherItems.Text = Strings.TaskEditor.gatheritems;
        lblItem.Text = Strings.TaskEditor.item;
        lblItemQuantity.Text = Strings.TaskEditor.gatheramount;

        lblEventDriven.Text = Strings.TaskEditor.eventdriven;

        btnEditTaskEvent.Text = Strings.TaskEditor.editcompletionevent;
        btnSave.Text = Strings.TaskEditor.ok;
        btnCancel.Text = Strings.TaskEditor.cancel;
    }

    private void UpdateFormElements()
    {
        grpGatherItems.Hide();
        grpKillNpcs.Hide();
        mGrpMiniGame.Hide();
        switch (cmbTaskType.SelectedIndex)
        {
            case 0: //Event Driven
                break;
            case 1: //Gather Items
                grpGatherItems.Show();
                cmbItem.Items.Clear();
                cmbItem.Items.AddRange(ItemDescriptor.Names);
                if (cmbItem.Items.Count > 0)
                {
                    cmbItem.SelectedIndex = 0;
                }

                nudItemAmount.Value = 1;

                break;
            case 2: //Kill Npcs
                grpKillNpcs.Show();
                cmbNpc.Items.Clear();
                cmbNpc.Items.AddRange(NPCDescriptor.Names);
                if (cmbNpc.Items.Count > 0)
                {
                    cmbNpc.SelectedIndex = 0;
                }

                nudNpcQuantity.Value = 1;

                break;
            case 3:
            case 4:
            case 5:
                mGrpMiniGame.Show();
                var winnings = cmbTaskType.SelectedIndex == 4;
                mLblMiniGame.Visible = !winnings;
                mCmbMiniGame.Visible = !winnings;
                mLblMiniGameCurrency.Visible = winnings;
                mCmbMiniGameCurrency.Visible = winnings;
                if (mCmbMiniGame.SelectedIndex < 0) mCmbMiniGame.SelectedIndex = 0;
                if (mCmbMiniGameCurrency.SelectedIndex < 0 && mCmbMiniGameCurrency.Items.Count > 0) mCmbMiniGameCurrency.SelectedIndex = 0;
                if (mNudMiniGameAmount.Value < 1) mNudMiniGameAmount.Value = 1;
                break;
        }
    }

    private void btnSave_Click(object sender, EventArgs e)
    {
        mMyTask.Objective = (QuestObjective) cmbTaskType.SelectedIndex;
        mMyTask.Description = txtStartDesc.Text;
        switch (mMyTask.Objective)
        {
            case QuestObjective.EventDriven: //Event Driven
                mMyTask.TargetId = Guid.Empty;
                mMyTask.Quantity = 1;

                break;
            case QuestObjective.GatherItems: //Gather Items
                mMyTask.TargetId = ItemDescriptor.IdFromList(cmbItem.SelectedIndex);
                mMyTask.Quantity = (int) nudItemAmount.Value;

                break;
            case QuestObjective.KillNpcs: //Kill Npcs
                mMyTask.TargetId = NPCDescriptor.IdFromList(cmbNpc.SelectedIndex);
                mMyTask.Quantity = (int) nudNpcQuantity.Value;

                break;
            case QuestObjective.MiniGameWins:
            case QuestObjective.MiniGameWinnings:
            case QuestObjective.MiniGameLevel:
                mMyTask.MiniGameKey = mCmbMiniGame.SelectedIndex == 0 ? MiniGameProgression.Poker : MiniGameProgression.Poker;
                mMyTask.Quantity = (int)mNudMiniGameAmount.Value;
                mMyTask.TargetId = mMyTask.Objective == QuestObjective.MiniGameWinnings && mCmbMiniGameCurrency.SelectedIndex >= 0
                    ? mMiniGameCurrencyIds[mCmbMiniGameCurrency.SelectedIndex]
                    : Guid.Empty;
                break;
        }

        ParentForm.Close();
    }

    private void InitializeMiniGameControls()
    {
        mGrpMiniGame = new DarkUI.Controls.DarkGroupBox
        {
            Text = "Mini-game objective", Location = new System.Drawing.Point(10, 110),
            Size = new System.Drawing.Size(236, 83), Visible = false,
            BackColor = System.Drawing.Color.FromArgb(45, 45, 48),
            ForeColor = System.Drawing.Color.Gainsboro,
        };
        mLblMiniGame = new Label { Text = "Game:", AutoSize = true, Location = new System.Drawing.Point(7, 23) };
        mCmbMiniGame = new DarkUI.Controls.DarkComboBox
        {
            Location = new System.Drawing.Point(104, 19), Size = new System.Drawing.Size(116, 21),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        mCmbMiniGame.Items.Add("Poker"); mCmbMiniGame.SelectedIndex = 0;

        var lblAmount = new Label { Text = "Target:", AutoSize = true, Location = new System.Drawing.Point(7, 55) };
        mNudMiniGameAmount = new DarkUI.Controls.DarkNumericUpDown
        {
            Location = new System.Drawing.Point(104, 52), Size = new System.Drawing.Size(116, 20),
            Minimum = 1, Maximum = int.MaxValue, Value = 1
        };
        mLblMiniGameCurrency = new Label { Text = "Currency:", AutoSize = true, Location = new System.Drawing.Point(7, 23), Visible = false };
        mCmbMiniGameCurrency = new DarkUI.Controls.DarkComboBox
        {
            Location = new System.Drawing.Point(104, 19), Size = new System.Drawing.Size(116, 21),
            DropDownStyle = ComboBoxStyle.DropDownList, Visible = false
        };
        foreach (var item in MiniGameCurrency.CompatibleItems(ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>()))
        {
            mMiniGameCurrencyIds.Add(item.Id);
            mCmbMiniGameCurrency.Items.Add(item.Name);
        }
        if (mCmbMiniGameCurrency.Items.Count > 0) mCmbMiniGameCurrency.SelectedIndex = 0;

        mGrpMiniGame.Controls.Add(mLblMiniGame); mGrpMiniGame.Controls.Add(mCmbMiniGame);
        mGrpMiniGame.Controls.Add(lblAmount); mGrpMiniGame.Controls.Add(mNudMiniGameAmount);
        mGrpMiniGame.Controls.Add(mLblMiniGameCurrency); mGrpMiniGame.Controls.Add(mCmbMiniGameCurrency);
        grpEditor.Controls.Add(mGrpMiniGame);
        mGrpMiniGame.BringToFront();
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        Cancelled = true;
        mMyTask.EditingEvent.Load(mEventBackup);
        ParentForm.Close();
    }

    private void cmbConditionType_SelectedIndexChanged(object sender, EventArgs e)
    {
        UpdateFormElements();
    }

    private void btnEditTaskEvent_Click(object sender, EventArgs e)
    {
        mMyTask.EditingEvent.Name = Strings.TaskEditor.completionevent.ToString(mMyQuest.Name);
        var editor = new FrmEvent(null)
        {
            MyEvent = mMyTask.EditingEvent
        };

        editor.InitEditor(true, true, true);
        editor.ShowDialog();
        Globals.MainForm.BringToFront();
        BringToFront();
    }

}
