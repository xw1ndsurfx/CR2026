using Intersect.Client.Core;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.GenericClasses;
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
using RendererBase = Intersect.Client.Framework.Gwen.Renderer.Base;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

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
    private Label mQuestTaskHudHeader;
    private Label mQuestTaskHudTitle;
    private Label mQuestTaskHudProgressLabel;
    private ImagePanel mQuestTaskHudIcon;
    private Guid _lastHudQuestId = Guid.Empty;
    private Guid _lastHudTaskId = Guid.Empty;
    private int _lastHudProgress = -1;
    private string _lastHudText = "";
    private QuestTrackerPanel mQuestTaskHudPanel;

    public void DisposeHud()
    {
        mQuestTaskHudLabel = null;
        mQuestTaskHudTemplate = null;
        mQuestTaskHudHeader = null;
        mQuestTaskHudTitle = null;
        mQuestTaskHudProgressLabel = null;
        mQuestTaskHudIcon = null;

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

        // === Quest Tracker HUD ===
        // Small WorldMap-style window: dark translucent body, brown header,
        // white/gold typography, and no purple accents.
        mQuestTaskHudPanel = new QuestTrackerPanel(gameCanvas)
        {
            Width = 360,
            Height = 142,
            IsHidden = true,
        };
        mQuestTaskHudPanel.MouseInputEnabled = false;

        mQuestTaskHudIcon = new ImagePanel(mQuestTaskHudPanel, "QuestTrackerIcon")
        {
            TextureFilename = "questsicon.png",
            MaintainAspectRatio = true,
            MouseInputEnabled = false,
            RenderColor = new Color(a: 245, r: 255, g: 255, b: 255),
        };
        mQuestTaskHudIcon.SetBounds(23, 42, 36, 36);

        var trackerFont = GameContentManager.Current.GetFont("sourcesansproblack") ??
                          GameContentManager.Current.GetFont("sourcesanspro") ??
                          mQuestDescTemplateLabel.Font;

        mQuestTaskHudHeader = new Label(mQuestTaskHudPanel, "QuestTrackerHeader")
        {
            AutoSizeToContents = false,
            Font = trackerFont,
            FontSize = 8,
            TextColorOverride = Color.White,
            Text = "CURRENT QUEST",
            TextAlign = Pos.Left | Pos.CenterV,
            MouseInputEnabled = false,
        };
        mQuestTaskHudHeader.SetBounds(92, 7, 248, 17);

        mQuestTaskHudTitle = new Label(mQuestTaskHudPanel, "QuestTrackerTitle")
        {
            AutoSizeToContents = false,
            Font = trackerFont,
            FontSize = 11,
            TextColorOverride = new Color(a: 255, r: 244, g: 236, b: 219),
            TextAlign = Pos.Left | Pos.CenterV,
            MouseInputEnabled = false,
        };
        mQuestTaskHudTitle.SetBounds(92, 24, 248, 22);

        mQuestTaskHudLabel = new RichLabel(mQuestTaskHudPanel)
        {
            MouseInputEnabled = false,
        };
        mQuestTaskHudLabel.SetBounds(92, 49, 248, 52);

        mQuestTaskHudProgressLabel = new Label(mQuestTaskHudPanel, "QuestTrackerProgress")
        {
            AutoSizeToContents = false,
            Font = trackerFont,
            FontSize = 8,
            TextColorOverride = new Color(a: 255, r: 170, g: 220, b: 145),
            TextAlign = Pos.Left | Pos.CenterV,
            MouseInputEnabled = false,
        };
        mQuestTaskHudProgressLabel.SetBounds(92, 104, 248, 17);

        mQuestTaskHudTemplate = new Label(null)
        {
            TextColor = new Color(a: 255, r: 229, g: 225, b: 214),
            Font = trackerFont,
            FontSize = 8,
            Width = 248,
        };

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

        // --- Quest Tracker HUD ---
        if (mQuestTaskHudPanel == null ||
            mQuestTaskHudLabel == null ||
            mQuestTaskHudTemplate == null ||
            mQuestTaskHudTitle == null ||
            mQuestTaskHudProgressLabel == null ||
            Globals.Me == null)
        {
            return;
        }

        UpdateQuestTrackerPosition();
        mQuestTaskHudPanel.BringToFront();

        if (mSelectedQuest == null || !Globals.Me.QuestProgress.ContainsKey(mSelectedQuest.Id))
        {
            HideQuestTracker();
            return;
        }

        var playerQuest = Globals.Me.QuestProgress[mSelectedQuest.Id];
        var currentTask = mSelectedQuest.Tasks.FirstOrDefault(t => t.Id == playerQuest.TaskId);
        if (currentTask == null)
        {
            HideQuestTracker();
            return;
        }

        var mainText = currentTask.Description ?? string.Empty;
        if (mainText.Length > 600)
        {
            mainText = mainText[..600] + "...";
        }

        mainText = WrapText(mainText, 39);

        var progressText = string.Empty;
        var showProgressBar = currentTask.Quantity > 0;
        var progressRatio = 0f;

        if (showProgressBar)
        {
            progressRatio = Math.Clamp(
                playerQuest.TaskProgress / (float)Math.Max(1, currentTask.Quantity),
                0f,
                1f
            );
        }

        progressText = currentTask.Objective switch
        {
            QuestObjective.KillNpcs =>
                $"{playerQuest.TaskProgress} / {currentTask.Quantity} {NPCDescriptor.GetName(currentTask.TargetId)}",
            QuestObjective.GatherItems =>
                $"{playerQuest.TaskProgress} / {currentTask.Quantity} {ItemDescriptor.GetName(currentTask.TargetId)}",
            QuestObjective.PokerWinHands =>
                $"{playerQuest.TaskProgress} / {currentTask.Quantity} Poker hands won",
            QuestObjective.PokerWinAmount =>
                $"{playerQuest.TaskProgress} / {currentTask.Quantity} net Poker winnings",
            QuestObjective.PokerReachLevel =>
                $"Poker level {playerQuest.TaskProgress} / {currentTask.Quantity}",
            QuestObjective.PokerPlayHands =>
                $"{playerQuest.TaskProgress} / {currentTask.Quantity} Poker hands played",
            QuestObjective.BlackjackWinHands =>
                $"{playerQuest.TaskProgress} / {currentTask.Quantity} Blackjack hands won",
            QuestObjective.BlackjackWinAmount =>
                $"{playerQuest.TaskProgress} / {currentTask.Quantity} net Blackjack winnings",
            QuestObjective.BlackjackReachLevel =>
                $"Blackjack level {playerQuest.TaskProgress} / {currentTask.Quantity}",
            QuestObjective.BlackjackPlayHands =>
                $"{playerQuest.TaskProgress} / {currentTask.Quantity} Blackjack hands played",
            _ => string.Empty,
        };

        if (string.IsNullOrEmpty(progressText))
        {
            showProgressBar = false;
        }

        var hudTextKey = mainText + "\n" + progressText;
        if (_lastHudQuestId == mSelectedQuest.Id &&
            _lastHudTaskId == currentTask.Id &&
            _lastHudProgress == playerQuest.TaskProgress &&
            _lastHudText == hudTextKey)
        {
            mQuestTaskHudPanel.IsHidden = false;
            return;
        }

        _lastHudQuestId = mSelectedQuest.Id;
        _lastHudTaskId = currentTask.Id;
        _lastHudProgress = playerQuest.TaskProgress;
        _lastHudText = hudTextKey;

        mQuestTaskHudPanel.IsHidden = false;
        mQuestTaskHudTitle.Text = mSelectedQuest.Name;
        mQuestTaskHudProgressLabel.Text = progressText;
        mQuestTaskHudProgressLabel.IsHidden = string.IsNullOrEmpty(progressText);

        mQuestTaskHudLabel.ClearText();
        if (!string.IsNullOrWhiteSpace(mainText))
        {
            mQuestTaskHudLabel.AddText(mainText, mQuestTaskHudTemplate);
        }

        mQuestTaskHudPanel.ShowProgressBar = showProgressBar;
        mQuestTaskHudPanel.ProgressRatio = progressRatio;
        mQuestTaskHudLabel.Invalidate();
        mQuestTaskHudPanel.Invalidate();
    }

    private void UpdateQuestTrackerPosition()
    {
        if (mQuestTaskHudPanel?.Parent == null)
        {
            return;
        }

        var parentWidth = mQuestTaskHudPanel.Parent.Width;
        var parentHeight = mQuestTaskHudPanel.Parent.Height;

        var desiredX = Math.Max(12, parentWidth - mQuestTaskHudPanel.Width - 20);
        var desiredY = 20;

        if (global::Intersect.Client.Interface.Interface.HasInGameUI)
        {
            var minimap = global::Intersect.Client.Interface.Interface.GameUi.MinimapHud;
            if (minimap != null && !minimap.IsHidden)
            {
                desiredX = Math.Max(12, minimap.X - mQuestTaskHudPanel.Width - 14);
                desiredY = Math.Max(12, minimap.Y + 8);
            }
        }

        desiredY = Math.Min(
            desiredY,
            Math.Max(12, parentHeight - mQuestTaskHudPanel.Height - 12)
        );

        if (mQuestTaskHudPanel.X != desiredX || mQuestTaskHudPanel.Y != desiredY)
        {
            mQuestTaskHudPanel.SetBounds(
                desiredX,
                desiredY,
                mQuestTaskHudPanel.Width,
                mQuestTaskHudPanel.Height
            );
        }
    }

    private void HideQuestTracker()
    {
        if (mQuestTaskHudPanel != null)
        {
            mQuestTaskHudPanel.IsHidden = true;
            mQuestTaskHudPanel.ShowProgressBar = false;
        }

        if (_lastHudQuestId == Guid.Empty)
        {
            return;
        }

        mQuestTaskHudLabel?.ClearText();
        if (mQuestTaskHudTitle != null)
        {
            mQuestTaskHudTitle.Text = string.Empty;
        }

        if (mQuestTaskHudProgressLabel != null)
        {
            mQuestTaskHudProgressLabel.Text = string.Empty;
        }

        _lastHudQuestId = Guid.Empty;
        _lastHudTaskId = Guid.Empty;
        _lastHudProgress = -1;
        _lastHudText = string.Empty;
    }

    private sealed class QuestTrackerPanel : Base
    {
        private float _progressRatio;

        public QuestTrackerPanel(Base parent) : base(parent, "QuestTaskTracker")
        {
            MouseInputEnabled = false;
            KeyboardInputEnabled = false;
            ShouldDrawBackground = true;
        }

        public bool ShowProgressBar { get; set; }

        public float ProgressRatio
        {
            get => _progressRatio;
            set => _progressRatio = Math.Clamp(value, 0f, 1f);
        }

        protected override void Render(SkinBase skin)
        {
            base.Render(skin);
            var renderer = skin.Renderer;

            Fill(renderer, new Color(a: 220, r: 24, g: 14, b: 15), 0, 0, Width, Height);
            Fill(renderer, new Color(a: 242, r: 94, g: 60, b: 49), 0, 0, Width, 30);
            Fill(renderer, new Color(a: 255, r: 126, g: 82, b: 62), 0, 29, Width, 1);
            Outline(renderer, new Color(a: 255, r: 72, g: 43, b: 35), 0, 0, Width, Height, 2);
            Fill(renderer, new Color(a: 72, r: 12, g: 10, b: 10), 82, 39, Width - 94, Height - 52);

            if (ShowProgressBar)
            {
                const int barX = 92;
                var barY = Height - 13;
                var barWidth = Width - 112;
                const int barHeight = 5;

                Fill(renderer, new Color(a: 205, r: 42, g: 31, b: 31), barX, barY, barWidth, barHeight);

                var fillWidth = (int)Math.Round(barWidth * ProgressRatio);
                if (fillWidth > 0)
                {
                    Fill(renderer, new Color(a: 235, r: 119, g: 178, b: 93), barX, barY, fillWidth, barHeight);
                }

                Outline(renderer, new Color(a: 220, r: 155, g: 113, b: 92), barX, barY, barWidth, barHeight, 1);
            }
        }

        private static void Outline(
            RendererBase renderer,
            Color color,
            int x,
            int y,
            int width,
            int height,
            int thickness
        )
        {
            Fill(renderer, color, x, y, width, thickness);
            Fill(renderer, color, x, y + height - thickness, width, thickness);
            Fill(renderer, color, x, y, thickness, height);
            Fill(renderer, color, x + width - thickness, y, thickness, height);
        }

        private static void Fill(RendererBase renderer, Color color, int x, int y, int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return;
            }

            renderer.DrawColor = color;
            renderer.DrawFilledRect(new Rectangle(x, y, width, height));
        }
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
                            else if (mSelectedQuest.Tasks[i].Objective is QuestObjective.PokerWinHands or QuestObjective.PokerWinAmount or
                                     QuestObjective.PokerReachLevel or QuestObjective.PokerPlayHands)
                            {
                                var progress = Globals.Me.QuestProgress[mSelectedQuest.Id].TaskProgress;
                                var quantity = mSelectedQuest.Tasks[i].Quantity;
                                var text = mSelectedQuest.Tasks[i].Objective switch
                                {
                                    QuestObjective.PokerWinHands => $"{progress} / {quantity} Poker hands won",
                                    QuestObjective.PokerWinAmount => $"{progress} / {quantity} net Poker winnings",
                                    QuestObjective.PokerReachLevel => $"Poker level {progress} / {quantity}",
                                    QuestObjective.PokerPlayHands => $"{progress} / {quantity} Poker hands played",
                                    _ => string.Empty,
                                };
                                mQuestDescLabel.AddText(text, mQuestDescTemplateLabel);
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

        HideQuestTracker();

    }

}
