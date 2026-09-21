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
    private readonly List<(Guid Id, string Name)> _pokerCurrencies = new();

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
            case 3: //Poker winnings
                PreparePokerCurrencies();
                cmbItem.SelectedIndex = Math.Max(0, _pokerCurrencies.FindIndex(x => x.Id == (mMyTask?.TargetId ?? Guid.Empty)));
                nudItemAmount.Value = Math.Clamp(mMyTask?.Quantity ?? 1, 1, (int)nudItemAmount.Maximum);
                break;
            case 4: //Poker hands
            case 5: //Poker level
                nudItemAmount.Value = Math.Clamp(mMyTask?.Quantity ?? 1, 1, (int)nudItemAmount.Maximum);
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
        cmbTaskType.Items.Add("Poker - Win amount");
        cmbTaskType.Items.Add("Poker - Win hands");
        cmbTaskType.Items.Add("Poker - Reach level");

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
        cmbItem.Show(); lblItem.Show();
        grpGatherItems.Text = Strings.TaskEditor.gatheritems;
        lblItem.Text = Strings.TaskEditor.item;
        lblItemQuantity.Text = Strings.TaskEditor.gatheramount;
        nudItemAmount.Maximum = 100000;
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
                if (cmbNpc.Items.Count > 0) cmbNpc.SelectedIndex = 0;
                nudNpcQuantity.Value = 1;
                break;
            case 3:
                grpGatherItems.Show(); grpGatherItems.Text = "Poker - Win amount";
                lblItem.Text = "Currency:"; lblItemQuantity.Text = "Amount:";
                nudItemAmount.Maximum = 1000000000; nudItemAmount.Value = 1; PreparePokerCurrencies();
                break;
            case 4:
                grpGatherItems.Show(); grpGatherItems.Text = "Poker - Win hands";
                cmbItem.Hide(); lblItem.Hide(); lblItemQuantity.Text = "Winning hands:";
                nudItemAmount.Maximum = 100000; nudItemAmount.Value = 1;
                break;
            case 5:
                grpGatherItems.Show(); grpGatherItems.Text = "Poker - Reach level";
                cmbItem.Hide(); lblItem.Hide(); lblItemQuantity.Text = "Poker level:";
                nudItemAmount.Maximum = MiniGameProgression.MaximumLevel; nudItemAmount.Value = 1;
                break;
        }
    }

    private void PreparePokerCurrencies()
    {
        _pokerCurrencies.Clear(); _pokerCurrencies.Add((Guid.Empty, "Any poker currency"));
        _pokerCurrencies.AddRange(MiniGameCurrency.CompatibleItems(ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>())
            .Select(item => (item.Id, MiniGameCurrency.DisplayName(item))));
        cmbItem.Items.Clear(); cmbItem.Items.AddRange(_pokerCurrencies.Select(x => (object)x.Name).ToArray());
        if (cmbItem.Items.Count > 0 && cmbItem.SelectedIndex < 0) cmbItem.SelectedIndex = 0;
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
            case QuestObjective.PokerWinAmount:
                mMyTask.TargetId = cmbItem.SelectedIndex >= 0 && cmbItem.SelectedIndex < _pokerCurrencies.Count
                    ? _pokerCurrencies[cmbItem.SelectedIndex].Id : Guid.Empty;
                mMyTask.Quantity = (int)nudItemAmount.Value;
                break;
            case QuestObjective.PokerWinHands:
            case QuestObjective.PokerReachLevel:
                mMyTask.TargetId = Guid.Empty;
                mMyTask.Quantity = (int)nudItemAmount.Value;
                break;
        }

        ParentForm.Close();
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
