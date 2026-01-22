using Intersect.Client.Core;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.Control.EventArguments;
using Intersect.Client.General;
using Intersect.Client.Interface.Shared;
using Intersect.Client.Localization;
using Intersect.Client.Networking;
using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.GameObjects;
using Intersect.Utilities;

namespace Intersect.Client.Interface.Game;


public partial class QuestsWindow
{

    private readonly Button mBackButton;

    private readonly ScrollControl mQuestDescArea;

    private readonly RichLabel mQuestDescLabel;

    private readonly Label mQuestDescTemplateLabel;

    private readonly ListBox _questList;

    private readonly Label mQuestStatus;

    //Controls
    private readonly WindowControl mQuestsWindow;

    private readonly Label mQuestTitle;

    private readonly Button mQuitButton;

    private QuestDescriptor mSelectedQuest;

    //QuestHud
    private RichLabel mQuestTaskHudLabel;
    private Label mQuestTaskHudTemplate;
    private Guid _lastHudQuestId = Guid.Empty;
    private Guid _lastHudTaskId = Guid.Empty;
    private int _lastHudProgress = -1;
    private string _lastHudText = "";
    private Label mQuestTaskHudTemplateGreen;
    private Base mQuestTaskHudPanel;

    public void DisposeHud()
    {
        mQuestTaskHudLabel = null;
        mQuestTaskHudTemplate = null;
        mQuestTaskHudTemplateGreen = null;

        mQuestTaskHudPanel?.Dispose();
        mQuestTaskHudPanel = null;

        _lastHudQuestId = Guid.Empty;
        _lastHudTaskId = Guid.Empty;
        _lastHudProgress = -1;
        _lastHudText = "";
    }

    //Init
    public QuestsWindow(Canvas gameCanvas)
    {
        mQuestsWindow = new WindowControl(gameCanvas, Strings.QuestLog.Title, false, "QuestsWindow");
        mQuestsWindow.DisableResizing();

        _questList = new ListBox(mQuestsWindow, "QuestList");
        _questList.EnableScroll(false, true);

        mQuestTitle = new Label(mQuestsWindow, "QuestTitle");
        mQuestTitle.SetText("");

        mQuestStatus = new Label(mQuestsWindow, "QuestStatus");
        mQuestStatus.SetText("");

        mQuestDescArea = new ScrollControl(mQuestsWindow, "QuestDescription");

        mQuestDescTemplateLabel = new Label(mQuestsWindow, "QuestDescriptionTemplate");

        mQuestDescLabel = new RichLabel(mQuestDescArea);

        mBackButton = new Button(mQuestsWindow, "BackButton");
        mBackButton.Text = Strings.QuestLog.Back;
        mBackButton.Clicked += _backButton_Clicked;

        mQuitButton = new Button(mQuestsWindow, "AbandonQuestButton");
        mQuitButton.SetText(Strings.QuestLog.Abandon);
        mQuitButton.Clicked += _quitButton_Clicked;

        mQuestsWindow.LoadJsonUi(GameContentManager.UI.InGame, Graphics.Renderer.GetResolutionString());

        // Override stupid decisions in the JSON
        _questList.IsDisabled = false;
        _questList.IsVisibleInTree = true;

        // === HUD Quête CONSTRUCTEUR ===

        // 0) Panel conteneur (créé UNE SEULE FOIS)
        mQuestTaskHudPanel = new Base(gameCanvas)
        {
            Width = 400,
            Height = 200,
        };

        // (Optionnel debug) pour voir la zone du HUD
        // mQuestTaskHudPanel.ShouldDrawBackground = true;
        // mQuestTaskHudPanel.BackgroundColor = new Color(120, 0, 0, 0);

        // 1) Positionne le panel
        mQuestTaskHudPanel.SetBounds(
            gameCanvas.Width - mQuestTaskHudPanel.Width - 20,
            20,
            mQuestTaskHudPanel.Width,
            mQuestTaskHudPanel.Height
        );

        // 2) RichLabel dans le panel (UNE SEULE FOIS)
        mQuestTaskHudLabel = new RichLabel(mQuestTaskHudPanel)
        {
            Dock = Pos.Fill
        };

        // évite que ça capte des clics
        mQuestTaskHudPanel.MouseInputEnabled = false;
        mQuestTaskHudLabel.MouseInputEnabled = false;

        // 3) Templates (UNE SEULE FOIS)
        mQuestTaskHudTemplate = new Label(null)
        {
            TextColor = Color.White,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? mQuestDescTemplateLabel.Font,
            Width = mQuestTaskHudPanel.Width
        };

        mQuestTaskHudTemplateGreen = new Label(null)
        {
            TextColor = Color.ForestGreen,
            Font = mQuestTaskHudTemplate.Font,
            Width = mQuestTaskHudPanel.Width
        };

        // 4) au-dessus
        mQuestTaskHudPanel.BringToFront();

    }

    private string WrapText(string text, int maxLineLength)
    {
        var words = text.Split(' ');
        var line = "";
        var result = "";

        foreach (var word in words)
        {
            if ((line + word).Length > maxLineLength)
            {
                result += line.TrimEnd() + "\n";
                line = "";
            }
            line += word + " ";
        }

        result += line.TrimEnd();
        return result;
    }

    private void _quitButton_Clicked(Base sender, MouseButtonState arguments)
    {
        if (mSelectedQuest != null)
        {
            _ = new InputBox(
                title: Strings.QuestLog.AbandonTitle.ToString(mSelectedQuest.Name),
                prompt: Strings.QuestLog.AbandonPrompt.ToString(mSelectedQuest.Name),
                inputType: InputType.YesNo,
                userData: mSelectedQuest.Id,
                onSubmit: (s, e) =>
                {
                    if (s is InputBox inputBox && inputBox.UserData is Guid questId)
                    {
                        PacketSender.SendAbandonQuest(questId);
                    }
                }
            );
        }
    }

    void AbandonQuest(object sender, EventArgs e)
    {
        PacketSender.SendAbandonQuest((Guid) ((InputBox) sender).UserData);
    }

    private void _backButton_Clicked(Base sender, MouseButtonState arguments)
    {
        mSelectedQuest = null;
        UpdateSelectedQuest();
    }

    private bool _shouldUpdateList;

    public void Update(bool shouldUpdateList)
    {
        if (!mQuestsWindow.IsVisibleInTree)
        {
            _shouldUpdateList |= shouldUpdateList;
            return;
        }

        if (mQuestsWindow.IsHidden || Globals.Me == null)
        {
            return;
        }

        UpdateInternal(shouldUpdateList);
    }

    private void UpdateInternal(bool shouldUpdateList)
    {
        if (shouldUpdateList)
        {
            UpdateQuestList();
            UpdateSelectedQuest();
        }

        // --- HUD Quête ---
        if (mQuestTaskHudPanel == null || mQuestTaskHudLabel == null ||
            mQuestTaskHudTemplate == null || mQuestTaskHudTemplateGreen == null ||
            Globals.Me == null)
        {
            return;
        }

        // Reposition si résolution/canvas change
        var parentWidth = mQuestTaskHudPanel.Parent?.Width ?? 0;
        if (parentWidth > 0)
        {
            var desiredX = parentWidth - mQuestTaskHudPanel.Width - 20;
            if (mQuestTaskHudPanel.X != desiredX)
            {
                mQuestTaskHudPanel.SetBounds(desiredX, 20, mQuestTaskHudPanel.Width, mQuestTaskHudPanel.Height);
            }
        }

        // ✅ resync templates à chaque frame (coût négligeable)
        mQuestTaskHudTemplate.Width = mQuestTaskHudPanel.Width;
        mQuestTaskHudTemplateGreen.Width = mQuestTaskHudPanel.Width;


        mQuestTaskHudPanel.BringToFront();

        // Pas de quête sélectionnée/progrès → clear une fois
        if (mSelectedQuest == null || !Globals.Me.QuestProgress.ContainsKey(mSelectedQuest.Id))
        {
            if (_lastHudQuestId != Guid.Empty)
            {
                mQuestTaskHudLabel.ClearText();
                _lastHudQuestId = Guid.Empty;
                _lastHudTaskId = Guid.Empty;
                _lastHudProgress = -1;
                _lastHudText = "";
            }
            return;
        }

        var playerQuest = Globals.Me.QuestProgress[mSelectedQuest.Id];
        var currentTask = mSelectedQuest.Tasks.FirstOrDefault(t => t.Id == playerQuest.TaskId);
        if (currentTask == null) return;

        var mainText = currentTask.Description ?? "";

        // clamp anti-gros textes
        if (mainText.Length > 800)
            mainText = mainText.Substring(0, 800) + "...";

        string progressText = "";
        if (currentTask.Objective == QuestObjective.KillNpcs)
            progressText = $"{playerQuest.TaskProgress} / {currentTask.Quantity} {NPCDescriptor.GetName(currentTask.TargetId)}";
        else if (currentTask.Objective == QuestObjective.GatherItems)
            progressText = $"{playerQuest.TaskProgress} / {currentTask.Quantity} {ItemDescriptor.GetName(currentTask.TargetId)}";

        // cache
        if (_lastHudQuestId == mSelectedQuest.Id &&
            _lastHudTaskId == currentTask.Id &&
            _lastHudProgress == playerQuest.TaskProgress &&
            _lastHudText == mainText)
        {
            return;
        }

        _lastHudQuestId = mSelectedQuest.Id;
        _lastHudTaskId = currentTask.Id;
        _lastHudProgress = playerQuest.TaskProgress;
        _lastHudText = mainText;

        // rebuild
        mQuestTaskHudLabel.ClearText();
        mQuestTaskHudLabel.AddText(mainText, mQuestTaskHudTemplate);

        if (!string.IsNullOrEmpty(progressText))
        {
            mQuestTaskHudLabel.AddLineBreak();
            mQuestTaskHudLabel.AddText(progressText, mQuestTaskHudTemplateGreen);
        }

        mQuestTaskHudLabel.Invalidate();
        mQuestTaskHudPanel.Invalidate();

    }


    private void UpdateQuestList()
    {
        _questList.RemoveAllRows();
        if (Globals.Me != null)
        {
            var quests = QuestDescriptor.Lookup.Values;

            var dict = new Dictionary<string, List<Tuple<QuestDescriptor, int, Color>>>();

            foreach (QuestDescriptor quest in quests)
            {
                if (quest != null)
                {
                    AddQuestToDict(dict, quest);
                }
            }


            foreach (var category in Options.Instance.Quest.Categories)
            {
                if (dict.ContainsKey(category))
                {
                    AddCategoryToList(category, Color.White);
                    var sortedList = dict[category].OrderBy(l => l.Item2).ThenBy(l => l.Item1.OrderValue).ToList();
                    foreach (var qst in sortedList)
                    {
                        AddQuestToList(qst.Item1.Name, qst.Item3, qst.Item1.Id, true);
                    }
                }
            }

            if (dict.ContainsKey(""))
            {
                var sortedList = dict[""].OrderBy(l => l.Item2).ThenBy(l => l.Item1.OrderValue).ToList();
                foreach (var qst in sortedList)
                {
                    AddQuestToList(qst.Item1.Name, qst.Item3, qst.Item1.Id, false);
                }
            }

        }
    }

    private void AddQuestToDict(Dictionary<string, List<Tuple<QuestDescriptor, int, Color>>> dict, QuestDescriptor quest)
    {
        var category = string.Empty;
        var add = false;
        var color = Color.White;
        var orderVal = -1;
        if (Globals.Me.QuestProgress.ContainsKey(quest.Id))
        {
            if (Globals.Me.QuestProgress[quest.Id].TaskId != Guid.Empty)
            {
                add = true;
                category = !TextUtils.IsNone(quest.InProgressCategory) ? quest.InProgressCategory : "";
                color = CustomColors.QuestWindow.InProgress;
                orderVal = 1;
            }
            else
            {
                if (Globals.Me.QuestProgress[quest.Id].Completed)
                {
                    if (quest.LogAfterComplete)
                    {
                        add = true;
                        category = !TextUtils.IsNone(quest.CompletedCategory) ? quest.CompletedCategory : "";
                        color = CustomColors.QuestWindow.Completed;
                        orderVal = 3;
                    }
                }
                else
                {
                    if (quest.LogBeforeOffer && !Globals.Me.HiddenQuests.Contains(quest.Id))
                    {
                        add = true;
                        category = !TextUtils.IsNone(quest.UnstartedCategory) ? quest.UnstartedCategory : "";
                        color = CustomColors.QuestWindow.NotStarted;
                        orderVal = 2;
                    }
                }
            }
        }
        else
        {
            if (quest.LogBeforeOffer && !Globals.Me.HiddenQuests.Contains(quest.Id))
            {
                add = true;
                category = !TextUtils.IsNone(quest.UnstartedCategory) ? quest.UnstartedCategory : "";
                color = CustomColors.QuestWindow.NotStarted;
                orderVal = 2;
            }
        }

        if (add)
        {
            if (!dict.ContainsKey(category))
            {
                dict.Add(category, new List<Tuple<QuestDescriptor, int, Color>>());
            }

            dict[category].Add(new Tuple<QuestDescriptor, int, Color>(quest, orderVal, color));
        }
    }

    private void AddQuestToList(string name, Color clr, Guid questId, bool indented = true)
    {
        var item = _questList.AddRow((indented ? "\t\t\t" : "") + name);
        item.UserData = questId;
        item.Clicked += QuestListItem_Clicked;
        item.Selected += Item_Selected;
        item.SetTextColor(clr);
        item.RenderColor = new Color(50, 255, 255, 255);
    }

    private void AddCategoryToList(string name, Color clr)
    {
        var item = _questList.AddRow(name);
        item.MouseInputEnabled = false;
        item.SetTextColor(clr);
        item.RenderColor = new Color(0, 255, 255, 255);
    }

    private void Item_Selected(Base sender, ItemSelectedEventArgs arguments)
    {
        _questList.UnselectAll();
    }

    private void QuestListItem_Clicked(Base sender, MouseButtonState arguments)
    {
        if (sender.UserData is not Guid questId)
        {
            return;
        }

        if (!QuestDescriptor.TryGet(questId, out var questDescriptor))
        {
            _questList.UnselectAll();
            return;
        }

        mSelectedQuest = questDescriptor;
        UpdateSelectedQuest();
    }

    private void UpdateSelectedQuest()
    {
        if (mSelectedQuest == null)
        {
            _questList.Show();
            mQuestTitle.Hide();
            mQuestDescArea.Hide();
            mQuestStatus.Hide();
            mBackButton.Hide();
            mQuitButton.Hide();
        }
        else
        {
            mQuestDescLabel.ClearText();
            mQuitButton.IsDisabled = true;
            ListBoxRow rw;
            string[] myText = null;
            var taskString = new List<string>();
            if (Globals.Me.QuestProgress.ContainsKey(mSelectedQuest.Id))
            {
                if (Globals.Me.QuestProgress[mSelectedQuest.Id].TaskId != Guid.Empty)
                {
                    //In Progress
                    mQuestStatus.SetText(Strings.QuestLog.InProgress);
                    mQuestStatus.SetTextColor(CustomColors.QuestWindow.InProgress, ComponentState.Normal);
                    mQuestDescTemplateLabel.SetTextColor(CustomColors.QuestWindow.QuestDesc, ComponentState.Normal);

                    if (mSelectedQuest.InProgressDescription.Length > 0)
                    {
                        mQuestDescLabel.AddText(mSelectedQuest.InProgressDescription, mQuestDescTemplateLabel);

                        mQuestDescLabel.AddLineBreak();
                        mQuestDescLabel.AddLineBreak();
                    }

                    mQuestDescLabel.AddText(Strings.QuestLog.CurrentTask, mQuestDescTemplateLabel);

                    mQuestDescLabel.AddLineBreak();
                    for (var i = 0; i < mSelectedQuest.Tasks.Count; i++)
                    {
                        if (mSelectedQuest.Tasks[i].Id == Globals.Me.QuestProgress[mSelectedQuest.Id].TaskId)
                        {
                            if (mSelectedQuest.Tasks[i].Description.Length > 0)
                            {
                                mQuestDescLabel.AddText(mSelectedQuest.Tasks[i].Description, mQuestDescTemplateLabel);

                                mQuestDescLabel.AddLineBreak();
                                mQuestDescLabel.AddLineBreak();
                            }

                            if (mSelectedQuest.Tasks[i].Objective == QuestObjective.GatherItems) //Gather Items
                            {
                                mQuestDescLabel.AddText(
                                    Strings.QuestLog.TaskItem.ToString(
                                        Globals.Me.QuestProgress[mSelectedQuest.Id].TaskProgress,
                                        mSelectedQuest.Tasks[i].Quantity,
                                        ItemDescriptor.GetName(mSelectedQuest.Tasks[i].TargetId)
                                    ), mQuestDescTemplateLabel
                                );
                            }
                            else if (mSelectedQuest.Tasks[i].Objective == QuestObjective.KillNpcs) //Kill Npcs
                            {
                                mQuestDescLabel.AddText(
                                    Strings.QuestLog.TaskNpc.ToString(
                                        Globals.Me.QuestProgress[mSelectedQuest.Id].TaskProgress,
                                        mSelectedQuest.Tasks[i].Quantity,
                                        NPCDescriptor.GetName(mSelectedQuest.Tasks[i].TargetId)
                                    ), mQuestDescTemplateLabel
                                );
                            }
                        }
                    }

                    mQuitButton.IsDisabled = !mSelectedQuest.Quitable;
                }
                else
                {
                    if (Globals.Me.QuestProgress[mSelectedQuest.Id].Completed)
                    {
                        //Completed
                        if (mSelectedQuest.LogAfterComplete)
                        {
                            mQuestStatus.SetText(Strings.QuestLog.Completed);
                            mQuestStatus.SetTextColor(CustomColors.QuestWindow.Completed, ComponentState.Normal);
                            mQuestDescLabel.AddText(mSelectedQuest.EndDescription, mQuestDescTemplateLabel);
                        }
                    }
                    else
                    {
                        //Not Started
                        if (mSelectedQuest.LogBeforeOffer)
                        {
                            mQuestStatus.SetText(Strings.QuestLog.NotStarted);
                            mQuestStatus.SetTextColor(CustomColors.QuestWindow.NotStarted, ComponentState.Normal);
                            mQuestDescLabel.AddText(mSelectedQuest.BeforeDescription, mQuestDescTemplateLabel);

                            mQuitButton?.Hide();
                        }
                    }
                }
            }
            else
            {
                //Not Started
                if (mSelectedQuest.LogBeforeOffer)
                {
                    mQuestStatus.SetText(Strings.QuestLog.NotStarted);
                    mQuestStatus.SetTextColor(CustomColors.QuestWindow.NotStarted, ComponentState.Normal);
                    mQuestDescLabel.AddText(mSelectedQuest.BeforeDescription, mQuestDescTemplateLabel);
                }
            }

            _questList.Hide();
            mQuestTitle.IsHidden = false;
            mQuestTitle.Text = mSelectedQuest.Name;
            mQuestDescArea.IsHidden = false;
            mQuestDescLabel.Width = mQuestDescArea.Width - mQuestDescArea.VerticalScrollBar.Width;
            mQuestDescLabel.SizeToChildren(false, true);
            mQuestStatus.Show();
            mBackButton.Show();
            mQuitButton.Show();
        }
    }

    public void Show()
    {
        if (_shouldUpdateList)
        {
            UpdateInternal(_shouldUpdateList);
            _shouldUpdateList = false;
        }

        mQuestsWindow.IsHidden = false;
    }

    public bool IsVisible()
    {
        return !mQuestsWindow.IsHidden;
    }

    public void Hide()
    {
        mQuestsWindow.IsHidden = true;
        mSelectedQuest = null;

        // Optionnel : vider le HUD quand on cache
        mQuestTaskHudLabel?.ClearText();
        _lastHudQuestId = Guid.Empty;
        _lastHudTaskId = Guid.Empty;
        _lastHudProgress = -1;
        _lastHudText = "";

    }

}
