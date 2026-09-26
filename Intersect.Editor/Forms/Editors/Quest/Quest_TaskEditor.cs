using Intersect.Editor.Forms.Editors.Events;
using Intersect.Editor.General;
using Intersect.Editor.Localization;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Resources;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.Framework.Core.MiniGames;
using Intersect.Framework.Core.MiniGames.Potions;
using Intersect.GameObjects;
using Microsoft.Extensions.Logging;


namespace Intersect.Editor.Forms.Editors.Quest;


public partial class QuestTaskEditor : UserControl
{

    public bool Cancelled;

    private string mEventBackup = null;

    private QuestDescriptor mMyQuest;

    private QuestTaskDescriptor mMyTask;

    private readonly GroupBox _guidanceGroup = new();
    private readonly ComboBox _guideAnimation = new();
    private readonly ComboBox _guideResource = new();
    private readonly Label _guideAnimationLabel = new();
    private readonly Label _guideResourceLabel = new();
    private readonly CheckBox _showNavigationArrow = new();

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
        InitializeGuidanceControls();
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
        LoadGuidanceControls();
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
            case 6:
            case 7:
            case 8:
            case 9:
            case 10:
            case 11:
            case 13:
            case 14:
            case 15:
            case 16:
                nudItemAmount.Value = Math.Max(1, mMyTask?.Quantity ?? 1);
                break;
            case 12:
            case 17:
                cmbItem.SelectedIndex = PotionRecipeListIndex(mMyTask?.TargetId ?? Guid.Empty);
                nudItemAmount.Value = Math.Max(1, mMyTask?.Quantity ?? 1);
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
        cmbTaskType.Items.Add("Poker - Win hands");
        cmbTaskType.Items.Add("Poker - Win net amount");
        cmbTaskType.Items.Add("Poker - Reach level");
        cmbTaskType.Items.Add("Poker - Play hands");
        cmbTaskType.Items.Add("Blackjack - Win hands");
        cmbTaskType.Items.Add("Blackjack - Win net amount");
        cmbTaskType.Items.Add("Blackjack - Reach level");
        cmbTaskType.Items.Add("Blackjack - Play hands");
        cmbTaskType.Items.Add("Potions - Brew recipes");
        cmbTaskType.Items.Add("Potions - Brew specific recipe");
        cmbTaskType.Items.Add("Potions - Reach Alchemy level");
        cmbTaskType.Items.Add("Potions - Earn score");
        cmbTaskType.Items.Add("Potions - Reach chain");
        cmbTaskType.Items.Add("Potions - Brew with max occupied cells");
        cmbTaskType.Items.Add("Potions - Brew specific recipe with minimum score");

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
        _guidanceGroup.Hide();
        cmbItem.Show();
        lblItem.Show();
        lblItemQuantity.Text = Strings.TaskEditor.gatheramount;
        switch (cmbTaskType.SelectedIndex)
        {
            case 0: //Event Driven
                break;
            case 1: //Gather Items
                grpGatherItems.Show();
                _guidanceGroup.Show();
                _guideResourceLabel.Show();
                _guideResource.Show();
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
                _guidanceGroup.Show();
                _guideResourceLabel.Hide();
                _guideResource.Hide();
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
            case 6:
            case 7:
            case 8:
            case 9:
            case 10:
            case 11:
            case 13:
            case 14:
            case 15:
            case 16:
                grpGatherItems.Show();
                grpGatherItems.Text = cmbTaskType.SelectedItem?.ToString() ?? "Mini-game objective";
                cmbItem.Hide();
                lblItem.Hide();
                var isLevel = cmbTaskType.SelectedIndex is 5 or 9 or 13;
                lblItemQuantity.Text = cmbTaskType.SelectedIndex switch
                {
                    15 => "Chain:",
                    16 => "Max cells:",
                    _ => isLevel ? "Level:" : "Target:",
                };
                nudItemAmount.Maximum = cmbTaskType.SelectedIndex == 16
                    ? PotionPuzzle.Columns * PotionPuzzle.Rows
                    : isLevel
                        ? MiniGameProgression.MaximumLevel
                        : 1_000_000_000;
                nudItemAmount.Value = 1;
                break;

            case 12:
            case 17:
                grpGatherItems.Show();
                grpGatherItems.Text = cmbTaskType.SelectedItem?.ToString() ??
                                      (cmbTaskType.SelectedIndex == 17
                                          ? "Potions - Brew specific recipe with minimum score"
                                          : "Potions - Brew specific recipe");
                cmbItem.Show();
                lblItem.Show();
                lblItem.Text = "Recipe:";
                lblItemQuantity.Text = cmbTaskType.SelectedIndex == 17 ? "Min score:" : "Count:";
                cmbItem.Items.Clear();
                cmbItem.Items.AddRange(PotionRecipes().Select(recipe => recipe.Name).ToArray());
                if (cmbItem.Items.Count > 0) cmbItem.SelectedIndex = 0;
                nudItemAmount.Maximum = 1_000_000_000;
                nudItemAmount.Value = 1;
                break;
        }
    }

    private void btnSave_Click(object sender, EventArgs e)
    {
        mMyTask.Objective = (QuestObjective) cmbTaskType.SelectedIndex;
        mMyTask.Description = txtStartDesc.Text;
        mMyTask.GuideAnimationId = AnimationIdFromGuideList(_guideAnimation.SelectedIndex);
        mMyTask.ShowNavigationArrow = _showNavigationArrow.Checked;
        mMyTask.GuideResourceId = mMyTask.Objective == QuestObjective.GatherItems
            ? ResourceIdFromGuideList(_guideResource.SelectedIndex)
            : Guid.Empty;

        switch (mMyTask.Objective)
        {
            case QuestObjective.EventDriven: //Event Driven
                mMyTask.TargetId = Guid.Empty;
                mMyTask.TargetName = string.Empty;
                mMyTask.Quantity = 1;

                break;
            case QuestObjective.GatherItems: //Gather Items
                mMyTask.TargetId = ItemDescriptor.IdFromList(cmbItem.SelectedIndex);
                mMyTask.TargetName = string.Empty;
                mMyTask.Quantity = (int) nudItemAmount.Value;

                break;
            case QuestObjective.KillNpcs: //Kill Npcs
                mMyTask.TargetId = NPCDescriptor.IdFromList(cmbNpc.SelectedIndex);
                mMyTask.TargetName = string.Empty;
                mMyTask.Quantity = (int) nudNpcQuantity.Value;
                break;
            case QuestObjective.PokerWinHands:
            case QuestObjective.PokerWinAmount:
            case QuestObjective.PokerReachLevel:
            case QuestObjective.PokerPlayHands:
            case QuestObjective.BlackjackWinHands:
            case QuestObjective.BlackjackWinAmount:
            case QuestObjective.BlackjackReachLevel:
            case QuestObjective.BlackjackPlayHands:
            case QuestObjective.PotionBrewRecipes:
            case QuestObjective.PotionReachLevel:
            case QuestObjective.PotionEarnScore:
            case QuestObjective.PotionReachChain:
            case QuestObjective.PotionBrewUnderOccupiedCells:
                mMyTask.TargetId = Guid.Empty;
                mMyTask.TargetName = string.Empty;
                mMyTask.Quantity = (int) nudItemAmount.Value;
                break;

            case QuestObjective.PotionBrewSpecificRecipe:
            case QuestObjective.PotionBrewSpecificRecipeMinScore:
                mMyTask.TargetId = PotionRecipeIdFromList(cmbItem.SelectedIndex);
                mMyTask.TargetName = PotionRecipeNameFromList(cmbItem.SelectedIndex);
                mMyTask.Quantity = (int) nudItemAmount.Value;
                break;
        }

        ParentForm.Close();
    }

    private void InitializeGuidanceControls()
    {
        Size = new Size(255, 372);
        grpEditor.Size = new Size(256, 366);
        btnEditTaskEvent.Top = 303;
        btnSave.Top = 332;
        btnCancel.Top = 332;

        _guidanceGroup.Text = "Quest Guidance";
        _guidanceGroup.ForeColor = System.Drawing.Color.Gainsboro;
        _guidanceGroup.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
        _guidanceGroup.Location = new System.Drawing.Point(9, 200);
        _guidanceGroup.Size = new Size(236, 96);

        _guideAnimationLabel.Text = "Marker animation:";
        _guideAnimationLabel.AutoSize = true;
        _guideAnimationLabel.Location = new System.Drawing.Point(7, 22);
        _guideAnimationLabel.ForeColor = System.Drawing.Color.Gainsboro;

        _guideAnimation.DropDownStyle = ComboBoxStyle.DropDownList;
        _guideAnimation.Location = new System.Drawing.Point(104, 18);
        _guideAnimation.Size = new Size(116, 21);
        _guideAnimation.Items.Add("None");
        _guideAnimation.Items.AddRange(AnimationDescriptor.Names);

        _guideResourceLabel.Text = "Resource:";
        _guideResourceLabel.AutoSize = true;
        _guideResourceLabel.Location = new System.Drawing.Point(7, 49);
        _guideResourceLabel.ForeColor = System.Drawing.Color.Gainsboro;

        _guideResource.DropDownStyle = ComboBoxStyle.DropDownList;
        _guideResource.Location = new System.Drawing.Point(104, 45);
        _guideResource.Size = new Size(116, 21);
        _guideResource.Items.Add("None");
        _guideResource.Items.AddRange(ResourceDescriptor.Names);

        _showNavigationArrow.Text = "Show objective arrow";
        _showNavigationArrow.AutoSize = true;
        _showNavigationArrow.Location = new System.Drawing.Point(7, 72);
        _showNavigationArrow.ForeColor = System.Drawing.Color.Gainsboro;
        _showNavigationArrow.BackColor = System.Drawing.Color.Transparent;
        _showNavigationArrow.Checked = true;

        _guidanceGroup.Controls.Add(_guideAnimationLabel);
        _guidanceGroup.Controls.Add(_guideAnimation);
        _guidanceGroup.Controls.Add(_guideResourceLabel);
        _guidanceGroup.Controls.Add(_guideResource);
        _guidanceGroup.Controls.Add(_showNavigationArrow);
        grpEditor.Controls.Add(_guidanceGroup);
        _guidanceGroup.BringToFront();
    }

    private void LoadGuidanceControls()
    {
        _guideAnimation.SelectedIndex = GuideAnimationListIndex(mMyTask?.GuideAnimationId ?? Guid.Empty);
        _guideResource.SelectedIndex = GuideResourceListIndex(mMyTask?.GuideResourceId ?? Guid.Empty);
        _showNavigationArrow.Checked = mMyTask?.ShowNavigationArrow ?? true;
    }

    private static int GuideAnimationListIndex(Guid id) =>
        id == Guid.Empty ? 0 : AnimationDescriptor.ListIndex(id) + 1;

    private static Guid AnimationIdFromGuideList(int index) =>
        index <= 0 ? Guid.Empty : AnimationDescriptor.IdFromList(index - 1);

    private static int GuideResourceListIndex(Guid id) =>
        id == Guid.Empty ? 0 : ResourceDescriptor.ListIndex(id) + 1;

    private static Guid ResourceIdFromGuideList(int index) =>
        index <= 0 ? Guid.Empty : ResourceDescriptor.IdFromList(index - 1);

    private static PotionRecipeDefinition[] PotionRecipes() =>
        (RewardConfiguration.Instance.PotionRecipes ?? [])
            .Where(recipe => recipe.IsStructurallyValid)
            .OrderBy(recipe => recipe.RequiredLevel)
            .ThenBy(recipe => recipe.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static int PotionRecipeListIndex(Guid id) =>
        Array.FindIndex(PotionRecipes(), recipe => recipe.Id == id);

    private static Guid PotionRecipeIdFromList(int index)
    {
        var recipes = PotionRecipes();
        return index >= 0 && index < recipes.Length ? recipes[index].Id : Guid.Empty;
    }

    private static string PotionRecipeNameFromList(int index)
    {
        var recipes = PotionRecipes();
        return index >= 0 && index < recipes.Length ? recipes[index].Name : string.Empty;
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
