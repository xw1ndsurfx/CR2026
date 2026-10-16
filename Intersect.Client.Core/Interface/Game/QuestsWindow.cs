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
    // Match the dungeon registry's drawn pixel-gold and dark bronze design.
    // Decorative elements use Gwen rectangles, never font icons or Unicode glyphs.
    private static readonly Color JournalGold = new(a: 255, r: 220, g: 188, b: 111);
    private static readonly Color JournalCream = new(a: 255, r: 241, g: 225, b: 195);
    private static readonly Color JournalMuted = new(a: 255, r: 184, g: 171, b: 151);
    private static readonly Color JournalGood = new(a: 255, r: 135, g: 213, b: 143);

    private readonly QuestJournalChrome mQuestJournalChrome;
    private readonly Label mQuestJournalHeading;
    private readonly Label mQuestJournalSubtitle;
    private readonly Label mQuestJournalSummary;
    private readonly Label mQuestDetailsProgress;

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

        // Saved legacy alignments otherwise move these controls back on the next layout pass.
        foreach (var control in new Base[] { _questList, mQuestTitle, mQuestStatus, mQuestDescArea, mBackButton, mQuitButton })
        {
            control.Alignment = [];
            control.Dock = Pos.None;
        }

        // Keep the saved JSON skin for input/close behavior while giving the
        // quest journal its own fixed, readable dungeon-registry layout.
        mQuestsWindow.SetSize(464, 388);
        mQuestsWindow.Alignment = [Alignments.Center];
        mQuestsWindow.TitleLabel.Font =
            GameContentManager.Current.GetFont("sourcesansproblack") ?? mQuestsWindow.TitleLabel.Font;
        mQuestsWindow.TitleLabel.FontSize = 12;
        mQuestsWindow.TitleLabel.TextColorOverride = JournalGold;

        // Keep separate quest and detail columns inside the single native window.
        mQuestJournalChrome = new QuestJournalChrome(mQuestsWindow, "RoyalQuestJournalFrame");
        mQuestJournalChrome.SetBounds(4, 0, 456, 350);
        mQuestJournalChrome.SendToBack();

        mQuestJournalHeading = AddJournalText(mQuestsWindow, "QuestJournalHeading",
            "ROYAL QUEST JOURNAL", 216, 8, 236, 32, 16, JournalCream, bold: true, center: true);
        mQuestJournalSubtitle = AddJournalText(mQuestsWindow, "QuestJournalSubtitle",
            "CHRONICLES OF YOUR ADVENTURES",
            216, 44, 236, 20, 10, JournalMuted, center: true);
        mQuestJournalSummary = AddJournalText(mQuestsWindow, "QuestJournalSummary",
            "SELECT A QUEST TO REVIEW ITS OBJECTIVES",
            216, 68, 236, 43, 11, JournalCream, bold: true, center: true);
        mQuestDetailsProgress = AddJournalText(mQuestsWindow, "QuestJournalProgress",
            string.Empty, 216, 68, 236, 43, 11, JournalGold, bold: true, center: true);
        mQuestDetailsProgress.Hide();

        _questList.IsDisabled = false;
        _questList.IsVisibleInTree = true;
        _questList.EnableScroll(false, true);
        _questList.FontName = "sourcesanspro";
        _questList.FontSize = 12;
        _questList.ShouldDrawBackground = false;
        _questList.SetBounds(10, 6, 190, 336);

        mQuestTitle.AutoSizeToContents = false;
        mQuestTitle.Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? mQuestTitle.Font;
        mQuestTitle.FontSize = 21;
        mQuestTitle.TextColorOverride = JournalCream;
        mQuestTitle.TextAlign = Pos.Center;
        mQuestTitle.SetBounds(220, 6, 230, 32);

        mQuestStatus.AutoSizeToContents = false;
        mQuestStatus.Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? mQuestStatus.Font;
        mQuestStatus.FontSize = 11;
        mQuestStatus.TextAlign = Pos.Center;
        mQuestStatus.SetBounds(220, 38, 230, 20);

        mQuestDescArea.SetBounds(224, 114, 225, 188);
        mQuestDescArea.OverflowX = OverflowBehavior.Hidden;
        mQuestDescArea.OverflowY = OverflowBehavior.Scroll;
        mQuestDescArea.AutoHideBars = true;

        mQuestDescTemplateLabel.Font =
            GameContentManager.Current.GetFont("sourcesanspro") ?? mQuestDescTemplateLabel.Font;
        mQuestDescTemplateLabel.FontSize = 11;
        mQuestDescTemplateLabel.SetTextColor(JournalCream, ComponentState.Normal);
        mQuestDescTemplateLabel.Hide();

        mBackButton.SetBounds(222, 315, 98, 28);
        mBackButton.Text = string.Empty;
        AddJournalButtonChrome(mBackButton, "Back", "BACK",
            98, 28, primary: false);
        // The artist's tiny right-facing quest arrow is flipped for Back.
        var returnArrow = AdmurinGuiArt.Add(mBackButton, "QuestBackArrow",
            "questlog_1", 8, 6, 6, 14);
        returnArrow?.SetUVs(1f, 0f, 0f, 1f);

        mQuitButton.SetBounds(326, 315, 126, 28);
        mQuitButton.Text = string.Empty;
        AddJournalButtonChrome(mQuitButton, "Abandon", "Abandon",
            126, 28, primary: true);

        // All native controls stay above the decorative Gwen background.
        _questList.BringToFront();
        mQuestTitle.BringToFront();
        mQuestStatus.BringToFront();
        mQuestDescArea.BringToFront();
        mBackButton.BringToFront();
        mQuitButton.BringToFront();

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
        var trackerArt = AdmurinGuiArt.Add(mQuestTaskHudPanel, "AdmurinTrackerBadge",
            "questlog_2", 23, 49, 43, 43);
        mQuestTaskHudPanel.HasArtistBadge = trackerArt != null;
        if (trackerArt != null)
        {
            // questlog_2 is a blank 34x34 medallion. The companion arrow
            // gives it a readable "current objective" symbol.
            AdmurinGuiArt.Add(mQuestTaskHudPanel, "AdmurinTrackerMarker",
                "questlog_1", 36, 59, 15, 23);
        }

        var trackerFont = GameContentManager.Current.GetFont("sourcesansproblack") ??
                          GameContentManager.Current.GetFont("sourcesanspro") ??
                          mQuestDescTemplateLabel.Font;

        mQuestTaskHudHeader = new Label(mQuestTaskHudPanel, "QuestTrackerHeader")
        {
            AutoSizeToContents = false,
            Font = trackerFont,
            FontSize = 8,
            TextColorOverride = JournalGold,
            Text = Intersect.Client.Localization.ClientLanguages.Text("CURRENT QUEST"),
            TextAlign = Pos.Left | Pos.CenterV,
            MouseInputEnabled = false,
        };
        mQuestTaskHudHeader.SetBounds(92, 7, 248, 17);

        mQuestTaskHudTitle = new Label(mQuestTaskHudPanel, "QuestTrackerTitle")
        {
            AutoSizeToContents = false,
            Font = trackerFont,
            FontSize = 11,
            TextColorOverride = JournalCream,
            TextAlign = Pos.Left | Pos.CenterV,
            MouseInputEnabled = false,
        };
        mQuestTaskHudTitle.SetBounds(92, 24, 248, 22);

        var trackerTextPanel = new Panel(mQuestTaskHudPanel, "QuestTrackerTextPanel")
        {
            ShouldDrawBackground = false,
            MouseInputEnabled = false,
        };
        trackerTextPanel.SetBounds(92, 49, 248, 58);

        mQuestTaskHudLabel = new RichLabel(trackerTextPanel)
        {
            Dock = Pos.Fill,
            MouseInputEnabled = false,
        };
        mQuestTaskHudLabel.SetBounds(0, 0, 248, 58);

        mQuestTaskHudProgressLabel = new Label(mQuestTaskHudPanel, "QuestTrackerProgress")
        {
            AutoSizeToContents = false,
            Font = trackerFont,
            FontSize = 8,
            TextColorOverride = JournalGood,
            TextAlign = Pos.Left | Pos.CenterV,
            MouseInputEnabled = false,
        };
        mQuestTaskHudProgressLabel.SetBounds(92, 104, 248, 17);

        mQuestTaskHudTemplate = new Label(null)
        {
            TextColor = JournalCream,
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

        if (Globals.Database?.ShowQuestHud == false || Interface.InGameUiOrNull?.SuppressStatusHuds == true) { HideQuestTracker(); return; }
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

        // Let RichLabel wrap against its fixed pixel width. Character-count wrapping
        // made the control progressively shrink and eventually render one word per line.
        mainText = mainText.Trim();

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
            QuestObjective.CompleteDungeon =>
                $"{playerQuest.TaskProgress} / {currentTask.Quantity} {(string.IsNullOrWhiteSpace(currentTask.TargetName) ? "dungeon completions" : currentTask.TargetName)}",
            QuestObjective.LockpickLocks =>
                $"{playerQuest.TaskProgress} / {currentTask.Quantity} locks picked",
            QuestObjective.LockpickSpecificLock =>
                $"{playerQuest.TaskProgress} / {currentTask.Quantity} {(string.IsNullOrWhiteSpace(currentTask.TargetName) ? "required lock" : currentTask.TargetName)}",
            QuestObjective.LockpickMinimumDifficulty =>
                $"Pick a difficulty {currentTask.Quantity}+ lock",
            QuestObjective.LockpickPerfect =>
                $"{playerQuest.TaskProgress} / {currentTask.Quantity} Perfect Lockpicks",
            QuestObjective.LockpickWithoutBreaking =>
                $"{playerQuest.TaskProgress} / {currentTask.Quantity} locks without breaking a pick",
            QuestObjective.LockpickInDungeon =>
                $"{playerQuest.TaskProgress} / {currentTask.Quantity} Dungeon locks",
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
        var questPercent = GetQuestCompletionPercent(mSelectedQuest, playerQuest);
        var completedTasks = GetCompletedTaskCount(mSelectedQuest, playerQuest);
        progressText = string.IsNullOrEmpty(progressText)
            ? $"QUEST {completedTasks}/{mSelectedQuest.Tasks.Count}  /  {questPercent}%"
            : $"{progressText}  /  QUEST {questPercent}%";

        mQuestTaskHudProgressLabel.Text = progressText;
        mQuestTaskHudProgressLabel.IsHidden = false;

        mQuestTaskHudLabel.ClearText();
        if (!string.IsNullOrWhiteSpace(mainText))
        {
            mQuestTaskHudLabel.AddText(mainText, mQuestTaskHudTemplate);
            mQuestTaskHudLabel.ForceImmediateRebuild();
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

        public bool HasArtistBadge { get; set; }

        public float ProgressRatio
        {
            get => _progressRatio;
            set => _progressRatio = Math.Clamp(value, 0f, 1f);
        }

        protected override void Render(SkinBase skin)
        {
            base.Render(skin);
            var renderer = skin.Renderer;

            var darkest = new Color(a: 234, r: 24, g: 15, b: 14);
            var bronze = new Color(a: 255, r: 77, g: 46, b: 34);
            var gold = new Color(a: 255, r: 158, g: 112, b: 51);
            var brightGold = new Color(a: 255, r: 224, g: 179, b: 88);

            Fill(renderer, darkest, 0, 0, Width, Height);
            Fill(renderer, bronze, 2, 2, Width - 4, 28);
            Fill(renderer, gold, 12, 29, Width - 24, 1);
            Outline(renderer, gold, 0, 0, Width, Height, 2);
            Fill(renderer, brightGold, 8, 5, 17, 2);
            Fill(renderer, brightGold, Width - 25, 5, 17, 2);
            Fill(renderer, new Color(a: 100, r: 12, g: 10, b: 10),
                82, 39, Width - 94, Height - 52);

            if (!HasArtistBadge)
            {
                // Legacy fallback when Admurin's original tracker tile is missing.
                Fill(renderer, darkest, 17, 39, 52, 57);
                Outline(renderer, brightGold, 17, 39, 52, 57, 2);
                Fill(renderer, new Color(a: 255, r: 194, g: 165, b: 111), 28, 49, 30, 37);
                Fill(renderer, new Color(a: 255, r: 93, g: 64, b: 40), 32, 57, 22, 2);
                Fill(renderer, new Color(a: 255, r: 93, g: 64, b: 40), 32, 65, 19, 2);
                Fill(renderer, new Color(a: 255, r: 93, g: 64, b: 40), 32, 73, 21, 2);
                Fill(renderer, brightGold, 26, 47, 34, 3);
                Fill(renderer, brightGold, 26, 85, 34, 3);
            }

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

            var entries = dict.Values.SelectMany(group => group).ToArray();
            var activeCount = entries.Count(entry => entry.Item2 == 1);
            var availableCount = entries.Count(entry => entry.Item2 == 2);
            var completedCount = entries.Count(entry => entry.Item2 == 3);
            mQuestJournalSummary.Text =
                Intersect.Client.Localization.ClientLanguages.Interpolated($"ACTIVE {activeCount:N0}  /  AVAILABLE {availableCount:N0}  /  COMPLETED {completedCount:N0}");

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

    private static int GetCompletedTaskCount(QuestDescriptor quest, QuestProgress progress)
    {
        if (quest.Tasks.Count == 0) return progress.Completed ? 1 : 0;
        if (progress.Completed && progress.TaskId == Guid.Empty) return quest.Tasks.Count;
        if (progress.TaskId == Guid.Empty) return 0;

        var currentIndex = quest.GetTaskIndex(progress.TaskId);
        return currentIndex < 0 ? 0 : Math.Clamp(currentIndex, 0, quest.Tasks.Count);
    }

    private static int GetQuestCompletionPercent(QuestDescriptor quest, QuestProgress progress)
    {
        if (quest.Tasks.Count == 0) return progress.Completed ? 100 : 0;
        return (int)Math.Round(
            GetCompletedTaskCount(quest, progress) * 100d / quest.Tasks.Count,
            MidpointRounding.AwayFromZero
        );
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
                color = JournalGold;
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
                        color = JournalGood;
                        orderVal = 3;
                    }
                }
                else
                {
                    if (quest.LogBeforeOffer && !Globals.Me.HiddenQuests.Contains(quest.Id))
                    {
                        add = true;
                        category = !TextUtils.IsNone(quest.UnstartedCategory) ? quest.UnstartedCategory : "";
                        color = JournalCream;
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
                color = JournalCream;
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
        if (QuestDescriptor.TryGet(questId, out var quest) &&
            Globals.Me?.QuestProgress.TryGetValue(questId, out var progress) == true &&
            progress.TaskId != Guid.Empty)
        {
            name += $" [{GetQuestCompletionPercent(quest, progress)}%]";
        }

        var item = _questList.AddRow((indented ? "    " : "") + name);
        item.UserData = questId;
        item.Clicked += QuestListItem_Clicked;
        item.Selected += Item_Selected;
        item.SetTextColor(clr);
        item.RenderColor = new Color(50, 255, 255, 255);
    }

    private void AddCategoryToList(string name, Color clr)
    {
        var item = _questList.AddRow(name.ToUpperInvariant());
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
        var detail = mSelectedQuest != null;
        mQuestsWindow.Title = Strings.QuestLog.Title;
        mQuestJournalHeading.IsHidden = detail;
        mQuestJournalSubtitle.IsHidden = detail;
        mQuestJournalHeading.Text = detail ? "QUEST DETAILS" : "ROYAL QUEST JOURNAL";
        mQuestJournalSubtitle.Text = detail
            ? "TRACK YOUR OBJECTIVES AND PROGRESS"
            : "CHRONICLES OF YOUR ADVENTURES";
        mQuestJournalSummary.IsHidden = detail;
        mQuestDetailsProgress.IsHidden = !detail;

        if (mSelectedQuest == null)
        {
            mQuestsWindow.Title = Strings.QuestLog.Title;
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
                    mQuestStatus.SetTextColor(JournalGold, ComponentState.Normal);
                    mQuestDescTemplateLabel.SetTextColor(JournalCream, ComponentState.Normal);

                    if (mSelectedQuest.InProgressDescription.Length > 0)
                    {
                        mQuestDescLabel.AddText(mSelectedQuest.InProgressDescription, mQuestDescTemplateLabel);

                        mQuestDescLabel.AddLineBreak();
                        mQuestDescLabel.AddLineBreak();
                    }

                    var overallPercent = GetQuestCompletionPercent(
                        mSelectedQuest,
                        Globals.Me.QuestProgress[mSelectedQuest.Id]
                    );
                    var completedTaskCount = GetCompletedTaskCount(
                        mSelectedQuest,
                        Globals.Me.QuestProgress[mSelectedQuest.Id]
                    );
                    mQuestDescLabel.AddText(
                        $"Quest progress: {completedTaskCount}/{mSelectedQuest.Tasks.Count} tasks ({overallPercent}%)",
                        mQuestDescTemplateLabel
                    );
                    mQuestDescLabel.AddLineBreak();
                    mQuestDescLabel.AddLineBreak();

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
                            else if (mSelectedQuest.Tasks[i].Objective == QuestObjective.CompleteDungeon)
                            {
                                var progress = Globals.Me.QuestProgress[mSelectedQuest.Id].TaskProgress;
                                var quantity = mSelectedQuest.Tasks[i].Quantity;
                                var dungeonName = string.IsNullOrWhiteSpace(mSelectedQuest.Tasks[i].TargetName)
                                    ? "Dungeon"
                                    : mSelectedQuest.Tasks[i].TargetName;
                                mQuestDescLabel.AddText(
                                    $"{progress} / {quantity} {dungeonName} completion(s)",
                                    mQuestDescTemplateLabel
                                );
                            }
                            else if (mSelectedQuest.Tasks[i].Objective is QuestObjective.LockpickLocks or
                                     QuestObjective.LockpickSpecificLock or QuestObjective.LockpickMinimumDifficulty or
                                     QuestObjective.LockpickPerfect or QuestObjective.LockpickWithoutBreaking or
                                     QuestObjective.LockpickInDungeon)
                            {
                                var progress = Globals.Me.QuestProgress[mSelectedQuest.Id].TaskProgress;
                                var quantity = mSelectedQuest.Tasks[i].Quantity;
                                var text = mSelectedQuest.Tasks[i].Objective switch
                                {
                                    QuestObjective.LockpickSpecificLock =>
                                        $"{progress} / {quantity} {(string.IsNullOrWhiteSpace(mSelectedQuest.Tasks[i].TargetName) ? "required lock" : mSelectedQuest.Tasks[i].TargetName)}",
                                    QuestObjective.LockpickMinimumDifficulty =>
                                        $"Pick a difficulty {quantity}+ lock",
                                    QuestObjective.LockpickPerfect =>
                                        $"{progress} / {quantity} Perfect Lockpicks",
                                    QuestObjective.LockpickWithoutBreaking =>
                                        $"{progress} / {quantity} locks without breaking a pick",
                                    QuestObjective.LockpickInDungeon =>
                                        $"{progress} / {quantity} Dungeon locks",
                                    _ => $"{progress} / {quantity} locks picked",
                                };
                                mQuestDescLabel.AddText(text, mQuestDescTemplateLabel);
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
                            mQuestStatus.SetTextColor(JournalGood, ComponentState.Normal);
                            mQuestDescLabel.AddText(mSelectedQuest.EndDescription, mQuestDescTemplateLabel);
                        }
                    }
                    else
                    {
                        //Not Started
                        if (mSelectedQuest.LogBeforeOffer)
                        {
                            mQuestStatus.SetText(Strings.QuestLog.NotStarted);
                            mQuestStatus.SetTextColor(JournalMuted, ComponentState.Normal);
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
                    mQuestStatus.SetTextColor(JournalMuted, ComponentState.Normal);
                    mQuestDescLabel.AddText(mSelectedQuest.BeforeDescription, mQuestDescTemplateLabel);
                }
            }

            if (Globals.Me?.QuestProgress.TryGetValue(mSelectedQuest.Id, out var currentProgress) == true)
            {
                var percent = GetQuestCompletionPercent(mSelectedQuest, currentProgress);
                var completed = GetCompletedTaskCount(mSelectedQuest, currentProgress);
                mQuestDetailsProgress.Text =
                    Intersect.Client.Localization.ClientLanguages.Interpolated($"PROGRESS {percent}%  /  {completed:N0} OF {mSelectedQuest.Tasks.Count:N0} TASKS");
            }
            else
            {
                mQuestDetailsProgress.Text = Intersect.Client.Localization.ClientLanguages.Text("QUEST NOT STARTED");
            }

            // Keep the list visible in the left pane while the
            // selected quest occupies the right pane, without losing focus.
            _questList.Show();
            mQuestTitle.IsHidden = false;
            mQuestTitle.Text = mSelectedQuest.Name;
            mQuestDescArea.IsHidden = false;
            mQuestDescLabel.Width = mQuestDescArea.Width - mQuestDescArea.VerticalScrollBar.Width - 8;
            mQuestDescLabel.SizeToChildren(false, true);
            mQuestStatus.Show();
            mBackButton.Show();

            // An unavailable "abandon" action should not look clickable.
            var canAbandon = Globals.Me?.QuestProgress.TryGetValue(mSelectedQuest.Id, out var abandonQuestProgress) == true
                && abandonQuestProgress.TaskId != Guid.Empty && mSelectedQuest.Quitable;
            if (canAbandon)
                mQuitButton.Show();
            else
                mQuitButton.Hide();
        }
    }

    private static Label AddJournalText(
        Base parent, string name, string text, int x, int y, int width, int height,
        int fontSize, Color color, bool bold = false, bool center = false)
    {
        var label = new Label(parent, name)
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont(bold ? "sourcesansproblack" : "sourcesanspro")
                   ?? parent.Skin.DefaultFont,
            FontSize = fontSize,
            TextColorOverride = color,
            TextAlign = center ? Pos.Center : Pos.Left | Pos.CenterV,
            Text = text,
            MouseInputEnabled = false,
        };
        label.SetBounds(x, y, width, height);
        return label;
    }

    private static void AddJournalButtonChrome(
        Button button, string suffix, string label, int width, int height, bool primary)
    {
        var frame = new QuestActionChrome(button, "QuestAction" + suffix, primary);
        frame.SetBounds(0, 0, width, height);
        AddJournalText(button, "QuestActionLabel" + suffix, label,
            5, 4, width - 10, height - 8, 10, JournalCream, bold: true, center: true);
    }

    private sealed class QuestActionChrome : Base
    {
        private readonly bool _primary;

        public QuestActionChrome(Base parent, string name, bool primary) : base(parent, name)
        {
            _primary = primary;
            MouseInputEnabled = false;
            KeyboardInputEnabled = false;
        }

        protected override void Render(SkinBase skin)
        {
            var rect = RenderBounds;
            var face = _primary
                ? new Color(a: 255, r: 77, g: 46, b: 34)
                : new Color(a: 255, r: 38, g: 23, b: 19);
            QuestJournalChrome.Fill(skin, rect, face);
            QuestJournalChrome.Border(skin, rect, JournalGold, 2);
            QuestJournalChrome.Border(skin, QuestJournalChrome.Inset(rect, 3),
                new Color(a: 255, r: 100, g: 69, b: 40), 1);
        }
    }

    /// <summary>
    /// Background and column divider inside the native quest journal window.
    /// </summary>
    private sealed class QuestJournalChrome : Base
    {
        private static readonly Color InnerGold = new(a: 255, r: 100, g: 69, b: 40);
        private static readonly Color Darkest = new(a: 255, r: 24, g: 15, b: 14);

        public QuestJournalChrome(Base parent, string name) : base(parent, name)
        {
            MouseInputEnabled = false;
            KeyboardInputEnabled = false;
        }

        protected override void Render(SkinBase skin)
        {
            var bounds = RenderBounds;
            Fill(skin, bounds, Darkest);
            // Content columns, not a second window with its own header and border.
            Fill(skin, bounds.X + 2, bounds.Y + 2, 201, bounds.Height - 4,
                new Color(a: 255, r: 12, g: 18, b: 9));
            Fill(skin, bounds.X + 205, bounds.Y + 2, 2, bounds.Height - 4, InnerGold);
        }

        public static Rectangle Inset(Rectangle rect, int amount) =>
            new(rect.X + amount, rect.Y + amount,
                rect.Width - amount * 2, rect.Height - amount * 2);

        public static void Fill(SkinBase skin, Rectangle rect, Color color) =>
            Fill(skin, rect.X, rect.Y, rect.Width, rect.Height, color);

        private static void Fill(SkinBase skin, int x, int y, int width, int height, Color color)
        {
            if (width <= 0 || height <= 0)
                return;

            skin.Renderer.DrawColor = color;
            skin.Renderer.DrawFilledRect(new Rectangle(x, y, width, height));
        }

        public static void Border(SkinBase skin, Rectangle rect, Color color, int thickness)
        {
            Fill(skin, rect.X, rect.Y, rect.Width, thickness, color);
            Fill(skin, rect.X, rect.Bottom - thickness, rect.Width, thickness, color);
            Fill(skin, rect.X, rect.Y, thickness, rect.Height, color);
            Fill(skin, rect.Right - thickness, rect.Y, thickness, rect.Height, color);
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
