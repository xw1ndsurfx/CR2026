using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.MiniGames;
using Intersect.Framework.Core.MiniGames.Cooking;
using Intersect.Network.Packets.MiniGames;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

internal sealed class CookingWindow : Base
{
    private readonly Canvas _canvas;
    private readonly Action<CookingRequestKind, Guid, Guid, bool, CookingActionInput> _send;
    private readonly Label _title;
    private readonly Label _recipe;
    private readonly Label _ingredients;
    private readonly Label _partner;
    private readonly Label _stage;
    private readonly Label _status;
    private readonly Label _score;
    private readonly Label _players;
    private readonly Label _professionXp;
    private readonly Label _recipeHeader;
    private readonly Label _stationHeader;
    private readonly CookingRecipePickerPanel _recipePicker;
    private readonly CookingResultPanel _resultPanel;
    private readonly Label _resultTitle;
    private readonly Label _resultQuality;
    private readonly Label _resultScore;
    private readonly Label _resultReward;
    private readonly Label _resultXp;
    private readonly Label _resultStats;
    private readonly Button _resultAgain;
    private readonly Button _resultLeave;
    private readonly Label _recipePickerTitle;
    private readonly Label _recipePickerPage;
    private readonly Button[] _recipeCards = new Button[6];
    private readonly Button _recipePagePrevious;
    private readonly Button _recipePageNext;
    private readonly Dictionary<Base, int> _fontSizes = [];
    private readonly Button _previousRecipe;
    private readonly Button _nextRecipe;
    private readonly Button _solo;
    private readonly Button _coop;
    private readonly Button _previousPartner;
    private readonly Button _nextPartner;
    private readonly Button _action;
    private readonly Button _actionSecondary;
    private readonly Button _actionTertiary;
    private readonly Button _accept;
    private readonly Button _decline;
    private readonly Button _leave;

    private CookingSessionState? _state;
    private long _serverOffset;
    private int _recipeIndex;
    private int _recipePage;
    private int _partnerIndex;
    private long _lastActionSequence;
    private long _lastComicEventSequence;
    private long _feedbackUntil;
    private long _comicUntil;
    private int _feedbackScore;
    private CookingStageType _feedbackStage;
    private CookingComicEventType _comicEventType;
    private string _comicEventText = string.Empty;
    private bool _stateTransitionsInitialized;
    private bool _previousSelecting;
    private bool _previousWaitingForPartner;
    private bool _previousInvitePending;
    private bool _previousComplete;
    private Guid _previousPartnerId;
    private bool _destroyed;

    public bool ExitRequested { get; private set; }

    public CookingWindow(
        Canvas canvas,
        Action<CookingRequestKind, Guid, Guid, bool, CookingActionInput> send
    ) : base(canvas, nameof(CookingWindow))
    {
        _canvas = canvas;
        _send = send;
        ShouldDrawBackground = false;
        MouseInputEnabled = true;

        _title = MakeLabel("CookingTitle", 82, 20, 860, 42, 22);
        _title.Text = "ROYAL KITCHEN";
        _title.TextAlign = Pos.Center;
        _title.TextColorOverride = new Color(255, 236, 210, 117);

        _recipeHeader = MakeLabel("CookingRecipeHeader", 62, 101, 286, 18, 10);
        _recipeHeader.Text = "RECIPE";
        _recipeHeader.TextColorOverride = new Color(255, 202, 166, 96);

        _stationHeader = MakeLabel("CookingStationHeader", 420, 101, 530, 18, 10);
        _stationHeader.Text = "KITCHEN STATION";
        _stationHeader.TextAlign = Pos.Center;
        _stationHeader.TextColorOverride = new Color(255, 202, 166, 96);

        _recipe = MakeLabel("CookingRecipe", 62, 115, 286, 92, 15);
        _ingredients = MakeLabel("CookingIngredients", 62, 228, 286, 198, 11);
        _ingredients.TextColorOverride = new Color(255, 239, 232, 214);
        _partner = MakeLabel("CookingPartner", 62, 448, 286, 78, 11);
        _partner.TextColorOverride = new Color(255, 220, 205, 176);

        _stage = MakeLabel("CookingStage", 420, 112, 530, 86, 18);
        _stage.TextAlign = Pos.Center;
        _stage.TextColorOverride = new Color(255, 236, 210, 117);
        _status = MakeLabel("CookingStatus", 420, 210, 530, 92, 12);
        _status.TextAlign = Pos.Center;
        _score = MakeLabel("CookingScore", 420, 314, 530, 60, 15);
        _score.TextAlign = Pos.Center;
        _score.TextColorOverride = new Color(255, 235, 224, 198);
        _players = MakeLabel("CookingPlayers", 420, 390, 530, 72, 11);
        _players.TextAlign = Pos.Center;

        _professionXp = MakeLabel("CookingProfessionXp", 55, 626, 700, 28, 11);
        _professionXp.TextColorOverride = new Color(255, 236, 210, 117);

        _recipePicker = new CookingRecipePickerPanel(this)
        {
            IsHidden = true,
            MouseInputEnabled = true,
            KeyboardInputEnabled = false,
        };

        _resultPanel = new CookingResultPanel(this)
        {
            IsHidden = true,
            MouseInputEnabled = true,
            KeyboardInputEnabled = false,
        };

        _resultTitle = new Label(_resultPanel, "CookingResultTitle")
        {
            AutoSizeToContents = false,
            Font = Skin.DefaultFont,
            FontSize = 16,
            Text = "DINNER IS READY!",
            TextAlign = Pos.Center,
            TextColorOverride = new Color(255, 236, 210, 117),
            MouseInputEnabled = false,
            KeyboardInputEnabled = false,
        };

        _resultQuality = new Label(_resultPanel, "CookingResultQuality")
        {
            AutoSizeToContents = false,
            Font = Skin.DefaultFont,
            FontSize = 30,
            TextAlign = Pos.Center,
            TextColorOverride = Color.White,
            MouseInputEnabled = false,
            KeyboardInputEnabled = false,
        };

        _resultScore = new Label(_resultPanel, "CookingResultScore")
        {
            AutoSizeToContents = false,
            Font = Skin.DefaultFont,
            FontSize = 15,
            TextAlign = Pos.Center,
            TextColorOverride = new Color(255, 235, 224, 198),
            MouseInputEnabled = false,
            KeyboardInputEnabled = false,
        };

        _resultReward = new Label(_resultPanel, "CookingResultReward")
        {
            AutoSizeToContents = false,
            Font = Skin.DefaultFont,
            FontSize = 11,
            TextAlign = Pos.Center,
            TextColorOverride = Color.White,
            MouseInputEnabled = false,
            KeyboardInputEnabled = false,
        };

        _resultXp = new Label(_resultPanel, "CookingResultXp")
        {
            AutoSizeToContents = false,
            Font = Skin.DefaultFont,
            FontSize = 13,
            TextAlign = Pos.Center,
            TextColorOverride = new Color(255, 236, 210, 117),
            MouseInputEnabled = false,
            KeyboardInputEnabled = false,
        };

        _resultStats = new Label(_resultPanel, "CookingResultStats")
        {
            AutoSizeToContents = false,
            Font = Skin.DefaultFont,
            FontSize = 10,
            TextAlign = Pos.Center,
            TextColorOverride = new Color(255, 216, 205, 182),
            MouseInputEnabled = false,
            KeyboardInputEnabled = false,
        };

        _resultAgain = new Button(_resultPanel, "CookingResultAgain")
        {
            Font = Skin.DefaultFont,
            FontSize = 11,
            Text = "CHOOSE ANOTHER RECIPE",
            TextColorOverride = new Color(255, 236, 210, 117),
        };
        _resultAgain.Clicked += (_, _) =>
            _send(CookingRequestKind.ReturnToRecipes, Guid.Empty, Guid.Empty, false, CookingActionInput.Primary);

        _resultLeave = new Button(_resultPanel, "CookingResultLeave")
        {
            Font = Skin.DefaultFont,
            FontSize = 11,
            Text = "LEAVE KITCHEN",
        };
        _resultLeave.Clicked += (_, _) => ExitRequested = true;

        _recipePickerTitle = new Label(_recipePicker, "CookingRecipePickerTitle")
        {
            AutoSizeToContents = false,
            Font = Skin.DefaultFont,
            FontSize = 17,
            Text = "CHOOSE A RECIPE",
            TextAlign = Pos.Center,
            TextColorOverride = new Color(255, 236, 210, 117),
            MouseInputEnabled = false,
            KeyboardInputEnabled = false,
        };

        _recipePickerPage = new Label(_recipePicker, "CookingRecipePickerPage")
        {
            AutoSizeToContents = false,
            Font = Skin.DefaultFont,
            FontSize = 10,
            TextAlign = Pos.Center,
            TextColorOverride = new Color(255, 211, 188, 137),
            MouseInputEnabled = false,
            KeyboardInputEnabled = false,
        };

        for (var slot = 0; slot < _recipeCards.Length; ++slot)
        {
            var captured = slot;
            var card = new Button(_recipePicker, "CookingRecipeCard" + slot)
            {
                Font = Skin.DefaultFont,
                FontSize = 11,
                Text = string.Empty,
                TextColorOverride = Color.White,
            };
            card.Clicked += (_, _) => SelectRecipeCard(captured);
            _recipeCards[slot] = card;
        }

        _recipePagePrevious = new Button(_recipePicker, "CookingRecipePagePrevious")
        {
            Font = Skin.DefaultFont,
            FontSize = 11,
            Text = "< Previous",
        };
        _recipePagePrevious.Clicked += (_, _) =>
        {
            if (_recipePage > 0) --_recipePage;
            RefreshRecipePicker();
        };

        _recipePageNext = new Button(_recipePicker, "CookingRecipePageNext")
        {
            Font = Skin.DefaultFont,
            FontSize = 11,
            Text = "Next >",
        };
        _recipePageNext.Clicked += (_, _) =>
        {
            ++_recipePage;
            RefreshRecipePicker();
        };

        _previousRecipe = MakeButton("CookingPreviousRecipe", "< Recipe", 55, 538, 92, () =>
        {
            _recipeIndex = Math.Max(0, _recipeIndex - 1);
            RefreshSelection();
        });
        _nextRecipe = MakeButton("CookingNextRecipe", "Recipe >", 154, 538, 92, () =>
        {
            if (_state != null)
                _recipeIndex = Math.Min(Math.Max(0, _state.Recipes.Length - 1), _recipeIndex + 1);
            RefreshSelection();
        });
        _solo = MakeButton("CookingSolo", "COOK SOLO", 55, 538, 142, StartSolo);

        _previousPartner = MakeButton("CookingPreviousPartner", "< Friend", 55, 578, 92, () =>
        {
            _partnerIndex = Math.Max(0, _partnerIndex - 1);
            RefreshSelection();
        });
        _nextPartner = MakeButton("CookingNextPartner", "Friend >", 154, 578, 92, () =>
        {
            if (_state != null)
                _partnerIndex = Math.Min(Math.Max(0, _state.PartyCandidates.Length - 1), _partnerIndex + 1);
            RefreshSelection();
        });
        _coop = MakeButton("CookingCoop", "COOK TOGETHER", 206, 538, 147, StartCoop);

        _action = MakeButton("CookingAction", "DO IT!", 435, 528, 160, () => DoAction(CookingActionInput.Primary));
        _actionSecondary = MakeButton("CookingActionSecondary", "SECONDARY", 606, 528, 160, () => DoAction(CookingActionInput.Secondary));
        _actionTertiary = MakeButton("CookingActionTertiary", "TERTIARY", 777, 528, 160, () => DoAction(CookingActionInput.Tertiary));
        _accept = MakeButton("CookingAccept", "ACCEPT COOKING INVITE", 500, 528, 220, () =>
            _send(CookingRequestKind.RespondInvite, Guid.Empty, Guid.Empty, true, CookingActionInput.Primary)
        );
        _decline = MakeButton("CookingDecline", "DECLINE", 730, 528, 140, () =>
            _send(CookingRequestKind.RespondInvite, Guid.Empty, Guid.Empty, false, CookingActionInput.Primary)
        );
        _leave = MakeButton("CookingLeave", "Leave Kitchen", 805, 650, 145, () => ExitRequested = true);

        _solo.TextColorOverride = new Color(255, 236, 210, 117);
        _coop.TextColorOverride = new Color(255, 236, 210, 117);
        _action.TextColorOverride = new Color(255, 236, 210, 117);
        _actionSecondary.TextColorOverride = Color.White;
        _actionTertiary.TextColorOverride = Color.White;

        ResizeToCanvas();
    }

    public void ResizeToCanvas()
    {
        SetPosition(0, 0);
        SetSize(_canvas.Width, _canvas.Height);

        var scaleX = Width / 1024f;
        var scaleY = Height / 720f;
        var scale = Math.Min(scaleX, scaleY);
        var offsetX = (int)((Width - 1024 * scale) / 2f);
        var offsetY = (int)((Height - 720 * scale) / 2f);

        foreach (var control in Children)
        {
            if (control.UserData is not Rectangle design) continue;
            control.SetBounds(
                offsetX + (int)(design.X * scale),
                offsetY + (int)(design.Y * scale),
                Math.Max(1, (int)(design.Width * scale)),
                Math.Max(1, (int)(design.Height * scale))
            );

            if (control is Label label && _fontSizes.TryGetValue(control, out var baseFont))
                label.FontSize = Math.Max(8, (int)Math.Round(baseFont * scale));
        }

        var pickerX = offsetX + (int)(405 * scale);
        var pickerY = offsetY + (int)(118 * scale);
        var pickerW = Math.Max(1, (int)(550 * scale));
        var pickerH = Math.Max(1, (int)(390 * scale));
        _recipePicker.SetBounds(pickerX, pickerY, pickerW, pickerH);

        int PX(int value) => (int)Math.Round(value * pickerW / 550d);
        int PY(int value) => (int)Math.Round(value * pickerH / 390d);

        _recipePickerTitle.SetBounds(PX(18), PY(14), PX(514), PY(32));
        _recipePickerTitle.FontSize = Math.Max(9, (int)Math.Round(17 * scale));

        for (var slot = 0; slot < _recipeCards.Length; ++slot)
        {
            var column = slot % 2;
            var row = slot / 2;
            var card = _recipeCards[slot];
            card.SetBounds(
                PX(20 + column * 258),
                PY(58 + row * 82),
                PX(246),
                PY(70)
            );
            card.FontSize = Math.Max(8, (int)Math.Round(11 * scale));
        }

        _recipePagePrevious.SetBounds(PX(20), PY(322), PX(108), PY(34));
        _recipePagePrevious.FontSize = Math.Max(8, (int)Math.Round(11 * scale));
        _recipePickerPage.SetBounds(PX(138), PY(322), PX(274), PY(34));
        _recipePickerPage.FontSize = Math.Max(8, (int)Math.Round(10 * scale));
        _recipePageNext.SetBounds(PX(422), PY(322), PX(108), PY(34));
        _recipePageNext.FontSize = Math.Max(8, (int)Math.Round(11 * scale));

        var resultX = offsetX + (int)(425 * scale);
        var resultY = offsetY + (int)(130 * scale);
        var resultW = Math.Max(1, (int)(520 * scale));
        var resultH = Math.Max(1, (int)(410 * scale));
        _resultPanel.SetBounds(resultX, resultY, resultW, resultH);

        int RX(int value) => (int)Math.Round(value * resultW / 520d);
        int RY(int value) => (int)Math.Round(value * resultH / 410d);

        _resultTitle.SetBounds(RX(18), RY(18), RX(484), RY(28));
        _resultTitle.FontSize = Math.Max(9, (int)Math.Round(16 * scale));
        _resultQuality.SetBounds(RX(18), RY(56), RX(484), RY(62));
        _resultQuality.FontSize = Math.Max(14, (int)Math.Round(30 * scale));
        _resultScore.SetBounds(RX(18), RY(126), RX(484), RY(34));
        _resultScore.FontSize = Math.Max(9, (int)Math.Round(15 * scale));
        _resultReward.SetBounds(RX(35), RY(176), RX(450), RY(64));
        _resultReward.FontSize = Math.Max(8, (int)Math.Round(11 * scale));
        _resultXp.SetBounds(RX(18), RY(250), RX(484), RY(34));
        _resultXp.FontSize = Math.Max(8, (int)Math.Round(13 * scale));
        _resultStats.SetBounds(RX(18), RY(292), RX(484), RY(42));
        _resultStats.FontSize = Math.Max(8, (int)Math.Round(10 * scale));
        _resultAgain.SetBounds(RX(58), RY(350), RX(250), RY(38));
        _resultAgain.FontSize = Math.Max(8, (int)Math.Round(11 * scale));
        _resultLeave.SetBounds(RX(320), RY(350), RX(142), RY(38));
        _resultLeave.FontSize = Math.Max(8, (int)Math.Round(11 * scale));
    }

    public void Update(CookingClientModel model)
    {
        ResizeToCanvas();

        if (model.Current?.State is not { } state)
            return;

        _state = state;
        _serverOffset = model.Current.ServerUnixMs - Environment.TickCount64;
        HandleStateSounds(state);
        HandleActionFeedback(state);
        HandleComicEvent(state);

        if (_recipeIndex >= state.Recipes.Length) _recipeIndex = Math.Max(0, state.Recipes.Length - 1);
        if (_partnerIndex >= state.PartyCandidates.Length) _partnerIndex = Math.Max(0, state.PartyCandidates.Length - 1);

        var selecting = state.RecipeSelectionRequired;
        _recipePicker.IsHidden = !selecting;
        if (selecting) _recipePicker.BringToFront();

        _resultPanel.IsHidden = !state.Complete;
        if (state.Complete)
        {
            RefreshResultPanel(state);
            _resultPanel.BringToFront();
        }

        _previousRecipe.IsHidden = true;
        _nextRecipe.IsHidden = true;
        _solo.IsHidden = _coop.IsHidden = !selecting;
        _previousPartner.IsHidden = _nextPartner.IsHidden = !selecting;
        var hideActions = selecting || state.WaitingForPartner || state.InvitePendingForYou || state.Complete;
        _action.IsHidden = hideActions;
        _actionSecondary.IsHidden = hideActions;
        _actionTertiary.IsHidden = hideActions;
        _accept.IsHidden = _decline.IsHidden = !state.InvitePendingForYou;
        _leave.IsHidden = state.Complete;

        ConfigureStageButtons(state);
        _action.IsDisabled = model.Pending || !state.YourTurn;
        _actionSecondary.IsDisabled = model.Pending || !state.YourTurn;
        _actionTertiary.IsDisabled = model.Pending || !state.YourTurn;
        _solo.IsDisabled = model.Pending;
        _coop.IsDisabled = model.Pending;

        RefreshSelection();
        RefreshRecipePicker();

        // RefreshSelection computes availability; pending always wins.
        if (selecting)
        {
            _solo.IsDisabled = _solo.IsDisabled || model.Pending;
            _coop.IsDisabled = _coop.IsDisabled || model.Pending;
        }

        if (!selecting)
        {
            _recipe.Text =
                $"{state.RecipeName}\n{state.ProfessionName} Lv {state.ProfessionLevel}\n" +
                $"{(state.PartnerId == Guid.Empty ? "Solo kitchen" : "Two-player kitchen")}";

            var stageNumber = Math.Min(state.StageCount, state.StageIndex + 1);
            _stage.Text = state.Complete
                ? $"DINNER IS READY!\n{state.Quality}"
                : state.WaitingForPartner
                    ? $"Waiting for {state.PartnerName}..."
                    : $"STAGE {stageNumber}/{state.StageCount}\n{StageName(state.StageType)}\n" +
                      $"{state.CompletedActions}/{state.RequiredActions} actions";

            var comicText = _comicUntil > Environment.TickCount64 && !string.IsNullOrWhiteSpace(_comicEventText)
                ? $"\n⚠ {_comicEventText}"
                : string.Empty;
            _status.Text = string.IsNullOrWhiteSpace(model.ErrorCode)
                ? $"{state.Status}\n{state.ActionHint}{comicText}"
                : ErrorText(model.ErrorCode);

            _score.Text = state.Complete
                ? $"TEAM SCORE: {state.TeamScore}%\n{state.Quality}\n{state.RewardText}"
                : $"Stage score: {state.StageScore}%   Combo x{state.Combo}   Mishaps {state.Mishaps}";

            _players.Text = string.Join(
                "\n",
                state.Participants.Select(player =>
                    $"{player.Name}: {player.Actions} action(s) • {player.Score}%"
                )
            );

            _professionXp.Text = FormatProfessionProgress(
                state.ProfessionName,
                state.ProfessionLevel,
                state.ProfessionMaximumLevel,
                state.ProfessionExperienceIntoLevel,
                state.ProfessionExperienceRequiredForLevel,
                state.ProfessionExperienceToNextLevel,
                state.ProfessionExperiencePercent,
                state.ProfessionMaximumLevelReached
            );
        }
    }

    private void RefreshSelection()
    {
        if (_state == null || !_state.RecipeSelectionRequired)
            return;

        if (_state.Recipes.Length == 0)
        {
            _recipe.Text = "No Royal Kitchen recipes have been created in Game Editor.";
            _ingredients.Text = string.Empty;
            _partner.Text = string.Empty;
            _solo.IsDisabled = _coop.IsDisabled = true;
            return;
        }

        var recipe = _state.Recipes[Math.Clamp(_recipeIndex, 0, _state.Recipes.Length - 1)];
        var lockText = recipe.Unlocked ? "READY" : $"LOCKED — {recipe.LockedReason}";
        _recipe.Text =
            $"{recipe.Name}\n{recipe.ProfessionName} Lv {Math.Max(1, recipe.ProfessionLevel)}/{Math.Max(1, recipe.ProfessionMaximumLevel)} • " +
            $"{recipe.Experience:N0} base XP\n{lockText}";

        _ingredients.Text =
            "INGREDIENTS\n" +
            string.Join(
                "\n",
                recipe.Ingredients.Select(ingredient =>
                    $"{(ingredient.Available >= ingredient.Needed ? "✓" : "✗")} " +
                    $"{ingredient.Name}: {ingredient.Available}/{ingredient.Needed}"
                )
            );

        var hasIngredients = recipe.Ingredients.All(value => value.Available >= value.Needed);
        _solo.IsDisabled = !recipe.Unlocked || !recipe.AllowSolo || recipe.RequireCoop || !hasIngredients;
        _coop.IsDisabled = !recipe.Unlocked || !recipe.AllowCoop || _state.PartyCandidates.Length == 0;

        if (_state.PartyCandidates.Length == 0)
        {
            _partner.Text = "CO-OP\nNo nearby party member is available.";
            _previousPartner.IsDisabled = _nextPartner.IsDisabled = true;
        }
        else
        {
            var candidate = _state.PartyCandidates[
                Math.Clamp(_partnerIndex, 0, _state.PartyCandidates.Length - 1)
            ];
            _partner.Text =
                $"CO-OP PARTNER\n{candidate.Name}\nParty member • nearby • ready to invite";
            _previousPartner.IsDisabled = _partnerIndex <= 0;
            _nextPartner.IsDisabled = _partnerIndex >= _state.PartyCandidates.Length - 1;
        }

        _previousRecipe.IsDisabled = _recipeIndex <= 0;
        _nextRecipe.IsDisabled = _recipeIndex >= _state.Recipes.Length - 1;
        _stage.Text = "CHOOSE A RECIPE";
        _status.Text = hasIngredients
            ? "Everything is ready. Cook solo or invite a nearby Party member."
            : "Gather the missing ingredients before starting the kitchen.";
        _score.Text = string.Empty;
        _players.Text = string.Empty;
        _professionXp.Text = FormatProfessionProgress(
            recipe.ProfessionName,
            recipe.ProfessionLevel,
            recipe.ProfessionMaximumLevel,
            recipe.ProfessionExperienceIntoLevel,
            recipe.ProfessionExperienceRequiredForLevel,
            recipe.ProfessionExperienceToNextLevel,
            recipe.ProfessionExperiencePercent,
            recipe.ProfessionMaximumLevelReached
        );
    }

    private void SelectRecipeCard(int slot)
    {
        if (_state?.RecipeSelectionRequired != true)
            return;

        var index = _recipePage * _recipeCards.Length + slot;
        if (index < 0 || index >= _state.Recipes.Length)
            return;

        _recipeIndex = index;
        RefreshSelection();
        RefreshRecipePicker();
    }

    private void RefreshRecipePicker()
    {
        if (_state == null)
            return;

        var recipes = _state.Recipes;
        var pageCount = Math.Max(1, (recipes.Length + _recipeCards.Length - 1) / _recipeCards.Length);
        _recipePage = Math.Clamp(_recipePage, 0, pageCount - 1);

        if (_recipeIndex >= 0 && _recipeIndex < recipes.Length)
        {
            var selectedPage = _recipeIndex / _recipeCards.Length;
            if (_recipePage < 0 || _recipePage >= pageCount)
                _recipePage = selectedPage;
        }

        _recipePickerTitle.Text =
            recipes.Length == 0
                ? "NO RECIPES AVAILABLE"
                : $"CHOOSE A RECIPE   •   {recipes.Length} AVAILABLE";

        for (var slot = 0; slot < _recipeCards.Length; ++slot)
        {
            var card = _recipeCards[slot];
            var index = _recipePage * _recipeCards.Length + slot;

            if (index >= recipes.Length)
            {
                card.IsHidden = true;
                continue;
            }

            var recipe = recipes[index];
            var missingCount = recipe.Ingredients.Count(
                ingredient => ingredient.Available < ingredient.Needed
            );
            var mode = recipe.RequireCoop
                ? "2P REQUIRED"
                : recipe.AllowSolo && recipe.AllowCoop
                    ? "SOLO / 2P"
                    : recipe.AllowCoop
                        ? "2P"
                        : "SOLO";

            var selected = index == _recipeIndex;
            var stateText = !recipe.Unlocked
                ? $"LOCKED • {recipe.LockedReason}"
                : missingCount > 0
                    ? $"MISSING {missingCount} INGREDIENT{(missingCount == 1 ? "" : "S")}"
                    : "READY TO COOK";

            card.IsHidden = false;
            card.IsDisabled = false;
            card.Text =
                $"{(selected ? "▶ " : "")}{recipe.Name}\n" +
                $"Lv {recipe.RequiredLevel} • +{recipe.Experience:N0} XP • {mode}\n" +
                stateText;

            card.TextColorOverride = !recipe.Unlocked
                ? new Color(255, 157, 128, 113)
                : selected
                    ? new Color(255, 236, 210, 117)
                    : missingCount > 0
                        ? new Color(255, 221, 177, 108)
                        : Color.White;
        }

        _recipePickerPage.Text = $"Page {_recipePage + 1} / {pageCount}";
        _recipePagePrevious.IsDisabled = _recipePage <= 0;
        _recipePageNext.IsDisabled = _recipePage >= pageCount - 1;
        _recipePicker.SelectedSlot = _recipeIndex - _recipePage * _recipeCards.Length;
    }

    private void RefreshResultPanel(CookingSessionState state)
    {
        _resultPanel.Quality = state.Quality;

        _resultQuality.Text = state.Quality.ToString().ToUpperInvariant();
        _resultQuality.TextColorOverride = state.Quality switch
        {
            CookingQuality.Perfect => new Color(255, 250, 216, 104),
            CookingQuality.Great => new Color(255, 111, 207, 123),
            CookingQuality.Decent => new Color(255, 225, 198, 142),
            _ => new Color(255, 203, 103, 82),
        };

        _resultScore.Text = $"TEAM SCORE   {state.TeamScore}%";
        _resultReward.Text = string.IsNullOrWhiteSpace(state.RewardText)
            ? "No meal reward was delivered."
            : $"REWARD\n{state.RewardText}";

        var professionName = string.IsNullOrWhiteSpace(state.ProfessionName)
            ? "Cooking"
            : state.ProfessionName;
        _resultXp.Text =
            $"+{state.ProfessionExperienceAwarded:N0} {professionName} XP   •   " +
            $"Lv {Math.Max(1, state.ProfessionLevel)}/{Math.Max(1, state.ProfessionMaximumLevel)}   •   " +
            $"{state.ProfessionExperiencePercent}%";

        var actionCount = state.Participants
            .FirstOrDefault(participant => participant.PlayerId == Intersect.Client.General.Globals.Me?.Id)?.Actions ?? 0;

        _resultStats.Text =
            $"Peak Combo x{state.PeakCombo}   •   Mishaps {state.Mishaps}   •   Your actions {actionCount}";

        _resultAgain.IsHidden = !state.IsHost;
        _resultAgain.IsDisabled = !state.IsHost;

        if (state.IsHost)
            _resultTitle.Text = "DINNER IS READY!";
        else
            _resultTitle.Text = "DINNER IS READY! • HOST CONTROLS NEXT ROUND";
    }

    private void StartSolo()
    {
        if (_state?.Recipes is not { Length: > 0 }) return;
        var recipe = _state.Recipes[Math.Clamp(_recipeIndex, 0, _state.Recipes.Length - 1)];
        _send(CookingRequestKind.StartRecipe, recipe.Id, Guid.Empty, false, CookingActionInput.Primary);
    }

    private void StartCoop()
    {
        if (_state?.Recipes is not { Length: > 0 } ||
            _state.PartyCandidates is not { Length: > 0 })
            return;

        var recipe = _state.Recipes[Math.Clamp(_recipeIndex, 0, _state.Recipes.Length - 1)];
        var partner = _state.PartyCandidates[
            Math.Clamp(_partnerIndex, 0, _state.PartyCandidates.Length - 1)
        ];
        _send(CookingRequestKind.StartRecipe, recipe.Id, partner.PlayerId, false, CookingActionInput.Primary);
    }

    private void DoAction(CookingActionInput input) =>
        _send(CookingRequestKind.Action, Guid.Empty, Guid.Empty, false, input);

    private void HandleStateSounds(CookingSessionState state)
    {
        if (!_stateTransitionsInitialized)
        {
            _stateTransitionsInitialized = true;
            _previousSelecting = state.RecipeSelectionRequired;
            _previousWaitingForPartner = state.WaitingForPartner;
            _previousInvitePending = state.InvitePendingForYou;
            _previousComplete = state.Complete;
            _previousPartnerId = state.PartnerId;

            if (state.InvitePendingForYou)
                PlayCookingSound(state.InviteSound);
            else if (!state.RecipeSelectionRequired && !state.WaitingForPartner && !state.Complete)
                PlayCookingSound(state.StartSound);

            return;
        }

        if (!_previousInvitePending && state.InvitePendingForYou)
            PlayCookingSound(state.InviteSound);

        if (_previousWaitingForPartner &&
            !state.WaitingForPartner &&
            state.PartnerId != Guid.Empty)
        {
            PlayCookingSound(state.PartnerJoinedSound);
            PlayCookingSound(state.StartSound);
        }
        else if (_previousSelecting &&
                 !state.RecipeSelectionRequired &&
                 !state.WaitingForPartner)
        {
            PlayCookingSound(state.StartSound);
        }

        if (!_previousComplete && state.Complete)
        {
            PlayCookingSound(state.CompleteSound);
            switch (state.Quality)
            {
                case CookingQuality.Burnt:
                    PlayCookingSound(state.BurntSound);
                    break;
                case CookingQuality.Great:
                    PlayCookingSound(state.GreatSound);
                    break;
                case CookingQuality.Perfect:
                    PlayCookingSound(state.PerfectSoundRecipe);
                    break;
            }
        }

        _previousSelecting = state.RecipeSelectionRequired;
        _previousWaitingForPartner = state.WaitingForPartner;
        _previousInvitePending = state.InvitePendingForYou;
        _previousComplete = state.Complete;
        _previousPartnerId = state.PartnerId;
    }

    private void HandleComicEvent(CookingSessionState state)
    {
        if (state.ComicEventSequence <= _lastComicEventSequence)
            return;

        _lastComicEventSequence = state.ComicEventSequence;
        _comicEventType = state.ComicEventType;
        _comicEventText = state.ComicEventText;
        _comicUntil = Environment.TickCount64 + 1_650;
    }

    private void HandleActionFeedback(CookingSessionState state)
    {
        if (state.ActionSequence <= _lastActionSequence)
            return;

        _lastActionSequence = state.ActionSequence;
        _feedbackScore = state.LastActionScore;
        _feedbackStage = state.StageType;
        _feedbackUntil = Environment.TickCount64 + (_feedbackScore >= 90 || _feedbackScore < 40 ? 1_300 : 750);

        PlayCookingSound(state.ActionSound);
        if (_feedbackScore >= 90)
            PlayCookingSound(state.PerfectSound);
        else if (_feedbackScore < 40)
            PlayCookingSound(state.MishapSound);
    }

    private static void PlayCookingSound(string? file)
    {
        if (!string.IsNullOrWhiteSpace(file))
            Intersect.Client.Core.Audio.AddGameSound(file, false);
    }

    private void ConfigureStageButtons(CookingSessionState state)
    {
        _action.IsHidden = _action.IsHidden;
        _actionSecondary.IsHidden = _actionSecondary.IsHidden;
        _actionTertiary.IsHidden = _actionTertiary.IsHidden;

        switch (state.StageType)
        {
            case CookingStageType.Chop:
                _action.Text = "CHOP!";
                _actionSecondary.IsHidden = true;
                _actionTertiary.IsHidden = true;
                break;

            case CookingStageType.Stir:
                _action.Text = "CLOCKWISE";
                _actionSecondary.Text = "COUNTER";
                _actionSecondary.IsHidden = _action.IsHidden;
                _actionTertiary.IsHidden = true;
                break;

            case CookingStageType.Heat:
                _action.Text = "MORE HEAT";
                _actionSecondary.Text = "LESS HEAT";
                _actionSecondary.IsHidden = _action.IsHidden;
                _actionTertiary.IsHidden = true;
                break;

            case CookingStageType.Flip:
                _action.Text = "FLIP!";
                _actionSecondary.IsHidden = true;
                _actionTertiary.IsHidden = true;
                break;

            case CookingStageType.Season:
                _action.Text = "ADD";
                _actionSecondary.Text = "REMOVE";
                _actionSecondary.IsHidden = _action.IsHidden;
                _actionTertiary.IsHidden = true;
                break;

            case CookingStageType.Knead:
                _action.Text = "LEFT";
                _actionSecondary.Text = "RIGHT";
                _actionSecondary.IsHidden = _action.IsHidden;
                _actionTertiary.IsHidden = true;
                break;

            case CookingStageType.Plate:
                _action.Text = "LEFT";
                _actionSecondary.Text = "CENTER";
                _actionTertiary.Text = "RIGHT";
                _actionSecondary.IsHidden = _action.IsHidden;
                _actionTertiary.IsHidden = _action.IsHidden;
                break;
        }
    }

    protected override void Render(SkinBase skin)
    {
        var renderer = skin.Renderer;
        renderer.DrawColor = new Color(a: 245, r: 20, g: 14, b: 10);
        renderer.DrawFilledRect(new Rectangle(0, 0, Width, Height));

        var scaleX = Width / 1024f;
        var scaleY = Height / 720f;
        var scale = Math.Min(scaleX, scaleY);
        var offsetX = (int)((Width - 1024 * scale) / 2f);
        var offsetY = (int)((Height - 720 * scale) / 2f);

        void Fill(int x, int y, int w, int h, Color color)
        {
            renderer.DrawColor = color;
            renderer.DrawFilledRect(
                new Rectangle(
                    offsetX + (int)(x * scale),
                    offsetY + (int)(y * scale),
                    Math.Max(1, (int)(w * scale)),
                    Math.Max(1, (int)(h * scale))
                )
            );
        }

        void Panel(int x, int y, int w, int h, Color body, Color border)
        {
            Fill(x - 3, y - 3, w + 6, h + 6, new Color(a: 255, r: 49, g: 28, b: 17));
            Fill(x - 2, y - 2, w + 4, h + 4, border);
            Fill(x, y, w, h, body);
            Fill(x + 5, y + 5, w - 10, 2, new Color(a: 115, r: 255, g: 222, b: 153));
        }

        // The mini-game is now staged as a real kitchen first, with the UI sitting on top
        // like Poker's table instead of filling the whole screen with flat panels.
        DrawKitchenEnvironment(Fill, Environment.TickCount64);

        // Carved wooden title sign.
        Panel(
            248,
            10,
            528,
            58,
            new Color(a: 245, r: 73, g: 31, b: 20),
            new Color(a: 255, r: 178, g: 117, b: 52)
        );
        Fill(270, 60, 484, 6, new Color(a: 255, r: 101, g: 54, b: 29));

        // Recipe book / pantry board.
        Panel(
            28,
            82,
            338,
            526,
            new Color(a: 238, r: 46, g: 28, b: 19),
            new Color(a: 255, r: 151, g: 93, b: 46)
        );
        Fill(43, 99, 308, 6, new Color(a: 255, r: 196, g: 137, b: 66));
        Fill(48, 212, 298, 2, new Color(a: 255, r: 94, g: 56, b: 34));
        Fill(48, 437, 298, 2, new Color(a: 255, r: 94, g: 56, b: 34));

        // Parchment-style status / stage board suspended above the work station.
        Panel(
            405,
            90,
            552,
            215,
            new Color(a: 232, r: 45, g: 31, b: 23),
            new Color(a: 255, r: 141, g: 90, b: 45)
        );
        Fill(419, 103, 524, 5, new Color(a: 255, r: 207, g: 158, b: 84));
        Fill(433, 195, 496, 2, new Color(a: 170, r: 116, g: 73, b: 40));

        // XP plaque built into the lower cabinetry.
        Panel(
            40,
            615,
            740,
            84,
            new Color(a: 244, r: 54, g: 31, b: 20),
            new Color(a: 255, r: 140, g: 91, b: 43)
        );

        DrawStageProgress(Fill);

        if (_state is { RecipeSelectionRequired: false, Complete: false, WaitingForPartner: false, StageDurationMs: > 0 } state)
        {
            var nowServer = Environment.TickCount64 + _serverOffset;
            var elapsed = Math.Clamp(nowServer - state.StageStartedUnixMs, 0, state.StageDurationMs);

            const int meterX = 476;
            const int meterY = 488;
            const int meterW = 392;
            const int meterH = 26;

            // A brass-edged control rail mounted to the kitchen counter.
            Fill(meterX - 10, meterY - 8, meterW + 20, meterH + 26, new Color(a: 235, r: 45, g: 29, b: 19));
            Fill(meterX - 7, meterY - 5, meterW + 14, meterH + 20, new Color(a: 255, r: 132, g: 83, b: 39));
            Fill(meterX, meterY, meterW, meterH, new Color(a: 255, r: 24, g: 20, b: 17));

            if (state.StageType == CookingStageType.Plate)
            {
                Fill(meterX, meterY + 3, meterW / 3 - 3, meterH - 6, new Color(a: 255, r: 66, g: 64, b: 55));
                Fill(meterX + meterW / 3 + 2, meterY + 3, meterW / 3 - 4, meterH - 6, new Color(a: 255, r: 74, g: 70, b: 56));
                Fill(meterX + meterW * 2 / 3 + 2, meterY + 3, meterW / 3 - 2, meterH - 6, new Color(a: 255, r: 66, g: 64, b: 55));
                var targetZone = CookingStageRules.PlateZoneFromTarget(state.TargetPermille);
                Fill(
                    meterX + targetZone * meterW / 3 + 4,
                    meterY + 5,
                    meterW / 3 - 8,
                    meterH - 10,
                    new Color(a: 255, r: 80, g: 151, b: 74)
                );
            }
            else
            {
                var targetX = meterX + (int)(meterW * state.TargetPermille / 1000d);
                var toleranceW = Math.Max(8, (int)(meterW * state.TolerancePermille / 1000d));
                Fill(
                    targetX - toleranceW / 2,
                    meterY + 3,
                    toleranceW,
                    meterH - 6,
                    new Color(a: 255, r: 80, g: 151, b: 74)
                );

                var meter = state.StageType is CookingStageType.Heat or CookingStageType.Season
                    ? state.MeterPermille
                    : CookingStageRules.TimingCursorPermille(
                        elapsed,
                        state.StageDurationMs,
                        Math.Max(1, state.StageDifficulty)
                    );
                var cursorX = meterX + (int)(meterW * meter / 1000d);
                Fill(cursorX - 3, meterY - 5, 6, meterH + 10, Color.White);
            }

            var remaining = Math.Max(0, state.StageDurationMs - elapsed);
            var timeWidth = (int)(meterW * remaining / Math.Max(1d, state.StageDurationMs));
            Fill(meterX, meterY + 34, timeWidth, 5, new Color(a: 255, r: 214, g: 169, b: 77));

            DrawStageProp(Fill, state, elapsed);
        }

        DrawProfessionXpBar(Fill);
        DrawActionFeedback(Fill);
        DrawComicEvent(Fill);
        base.Render(skin);
    }

    private void DrawKitchenEnvironment(
        Action<int, int, int, int, Color> fill,
        long now
    )
    {
        // Warm stone/plaster back wall.
        fill(0, 0, 1024, 720, new Color(a: 255, r: 42, g: 29, b: 21));
        fill(0, 74, 1024, 318, new Color(a: 255, r: 78, g: 64, b: 50));

        // Hand-built tile/brick backsplash.
        for (var row = 0; row < 10; ++row)
        {
            var brickY = 80 + row * 31;
            var offset = row % 2 == 0 ? 0 : 23;
            for (var column = -1; column < 24; ++column)
            {
                var brickX = column * 48 + offset;
                var shade = 86 + ((row + column + 12) % 3) * 7;
                fill(
                    brickX,
                    brickY,
                    45,
                    28,
                    new Color(a: 255, r: shade, g: shade - 18, b: shade - 31)
                );
                fill(brickX, brickY + 27, 45, 1, new Color(a: 175, r: 44, g: 35, b: 29));
            }
        }

        // Left stone hearth, deliberately behind the recipe board so it reads as room depth.
        fill(0, 150, 224, 290, new Color(a: 255, r: 65, g: 45, b: 34));
        fill(18, 184, 186, 226, new Color(a: 255, r: 36, g: 24, b: 19));
        fill(28, 200, 166, 200, new Color(a: 255, r: 25, g: 18, b: 15));

        // Animated oven fire.
        for (var flame = 0; flame < 9; ++flame)
        {
            var wave = (int)((now / 85 + flame * 17) % 22);
            var height = 22 + (wave % 14);
            var x = 42 + flame * 16;
            fill(x, 371 - height, 12, height, new Color(a: 220, r: 232, g: 110, b: 39));
            fill(x + 3, 371 - height + 8, 7, Math.Max(5, height - 12), new Color(a: 230, r: 251, g: 182, b: 65));
        }

        // Back counter and lower cabinets.
        fill(0, 390, 1024, 330, new Color(a: 255, r: 49, g: 29, b: 19));
        fill(0, 390, 1024, 20, new Color(a: 255, r: 133, g: 79, b: 40));
        fill(0, 410, 1024, 25, new Color(a: 255, r: 91, g: 52, b: 29));

        for (var cabinet = 0; cabinet < 7; ++cabinet)
        {
            var x = cabinet * 146;
            fill(x + 4, 455, 136, 248, new Color(a: 255, r: 73, g: 41, b: 25));
            fill(x + 10, 466, 124, 225, new Color(a: 255, r: 91, g: 53, b: 30));
            fill(x + 18, 477, 108, 203, new Color(a: 255, r: 61, g: 35, b: 23));
            fill(x + 67, 470, 7, 7, new Color(a: 255, r: 194, g: 133, b: 58));
        }

        // Large center preparation island.
        fill(376, 354, 604, 151, new Color(a: 255, r: 66, g: 38, b: 23));
        fill(386, 362, 584, 133, new Color(a: 255, r: 125, g: 78, b: 42));
        fill(386, 362, 584, 10, new Color(a: 255, r: 197, g: 141, b: 74));
        fill(398, 475, 560, 12, new Color(a: 255, r: 72, g: 43, b: 27));

        // Cutting board.
        fill(446, 390, 222, 76, new Color(a: 255, r: 85, g: 51, b: 29));
        fill(452, 396, 210, 64, new Color(a: 255, r: 162, g: 106, b: 57));
        fill(458, 402, 198, 4, new Color(a: 160, r: 232, g: 178, b: 103));

        // Stove embedded in the island.
        fill(700, 378, 230, 90, new Color(a: 255, r: 54, g: 49, b: 44));
        fill(710, 387, 210, 70, new Color(a: 255, r: 36, g: 32, b: 29));

        for (var burner = 0; burner < 3; ++burner)
        {
            var bx = 728 + burner * 60;
            fill(bx, 404, 42, 42, new Color(a: 255, r: 21, g: 20, b: 19));
            fill(bx + 7, 411, 28, 28, new Color(a: 255, r: 76, g: 61, b: 48));
            fill(bx + 12, 416, 18, 18, new Color(a: 255, r: 29, g: 25, b: 22));
        }

        // Pantry shelves on the right wall.
        fill(838, 104, 160, 220, new Color(a: 255, r: 63, g: 38, b: 24));
        for (var shelf = 0; shelf < 3; ++shelf)
        {
            var sy = 141 + shelf * 63;
            fill(848, sy, 140, 8, new Color(a: 255, r: 136, g: 83, b: 41));

            for (var jar = 0; jar < 5; ++jar)
            {
                var jx = 854 + jar * 27;
                var body = (jar % 3) switch
                {
                    0 => new Color(a: 255, r: 126, g: 78, b: 42),
                    1 => new Color(a: 255, r: 94, g: 108, b: 57),
                    _ => new Color(a: 255, r: 157, g: 118, b: 64),
                };
                fill(jx, sy - 27, 18, 23, body);
                fill(jx + 3, sy - 31, 12, 5, new Color(a: 255, r: 207, g: 178, b: 122));
            }
        }

        // Hanging copper cookware.
        fill(594, 80, 226, 7, new Color(a: 255, r: 91, g: 56, b: 33));
        for (var pan = 0; pan < 5; ++pan)
        {
            var px = 612 + pan * 41;
            var length = 42 + (pan % 3) * 12;
            fill(px + 12, 86, 4, length, new Color(a: 255, r: 102, g: 66, b: 40));
            fill(px, 86 + length, 28, 24, new Color(a: 255, r: 155, g: 83, b: 42));
            fill(px + 5, 91 + length, 18, 14, new Color(a: 255, r: 196, g: 108, b: 50));
        }

        // Small kitchen window with cool outside light to contrast the warm room.
        fill(934, 114, 80, 186, new Color(a: 255, r: 47, g: 34, b: 27));
        fill(942, 123, 64, 166, new Color(a: 255, r: 64, g: 97, b: 107));
        fill(972, 123, 5, 166, new Color(a: 255, r: 58, g: 39, b: 28));
        fill(942, 202, 64, 5, new Color(a: 255, r: 58, g: 39, b: 28));

        // Lanterns and warm light pools.
        for (var lantern = 0; lantern < 2; ++lantern)
        {
            var lx = lantern == 0 ? 112 : 900;
            fill(lx, 12, 34, 42, new Color(a: 255, r: 55, g: 34, b: 23));
            fill(lx + 6, 18, 22, 28, new Color(a: 255, r: 239, g: 163, b: 67));
            fill(lx + 10, 22, 14, 20, new Color(a: 235, r: 255, g: 211, b: 98));
        }

        // Pots / ingredients on the work surface.
        fill(770, 324, 132, 55, new Color(a: 255, r: 79, g: 50, b: 34));
        fill(782, 310, 108, 21, new Color(a: 255, r: 101, g: 61, b: 39));

        for (var item = 0; item < 8; ++item)
        {
            var ix = 402 + item * 32;
            var iy = 345 - (item % 3) * 7;
            var ingredient = (item % 4) switch
            {
                0 => new Color(a: 255, r: 210, g: 95, b: 38),
                1 => new Color(a: 255, r: 99, g: 132, b: 57),
                2 => new Color(a: 255, r: 222, g: 204, b: 151),
                _ => new Color(a: 255, r: 150, g: 54, b: 36),
            };
            fill(ix, iy, 18, 15, ingredient);
            fill(ix + 6, iy - 5, 5, 7, new Color(a: 255, r: 73, g: 114, b: 54));
        }

        // Steam above the main pot.
        for (var puff = 0; puff < 5; ++puff)
        {
            var rise = (int)((now / 28 + puff * 23) % 72);
            var px = 806 + puff * 12 + ((puff % 2 == 0 ? 1 : -1) * rise / 14);
            fill(
                px,
                304 - rise,
                12 + puff % 2 * 5,
                10 + puff % 3 * 5,
                new Color(a: 58, r: 226, g: 222, b: 211)
            );
        }

        // Foreground shadow/vignette anchors the kitchen in the room.
        fill(0, 704, 1024, 16, new Color(a: 190, r: 10, g: 7, b: 5));
        fill(0, 0, 18, 720, new Color(a: 140, r: 8, g: 6, b: 5));
        fill(1006, 0, 18, 720, new Color(a: 140, r: 8, g: 6, b: 5));
    }

    private void DrawStageProgress(Action<int, int, int, int, Color> fill)
    {
        if (_state == null)
            return;

        const int x = 420;
        const int y = 96;
        const int width = 530;
        const int height = 7;

        if (_state.RecipeSelectionRequired || _state.StageCount <= 0)
        {
            fill(x, y, width, height, new Color(a: 255, r: 57, g: 43, b: 32));
            return;
        }

        var count = Math.Clamp(_state.StageCount, 1, 12);
        var gap = 5;
        var segmentWidth = Math.Max(8, (width - gap * (count - 1)) / count);

        for (var index = 0; index < count; ++index)
        {
            var segmentX = x + index * (segmentWidth + gap);
            var color = index < _state.StageIndex
                ? new Color(a: 255, r: 74, g: 157, b: 91)
                : index == _state.StageIndex && !_state.Complete
                    ? new Color(a: 255, r: 231, g: 194, b: 112)
                    : _state.Complete
                        ? new Color(a: 255, r: 74, g: 157, b: 91)
                        : new Color(a: 255, r: 70, g: 52, b: 39);

            fill(segmentX, y, segmentWidth, height, color);

            if (index == _state.StageIndex && !_state.Complete)
                fill(segmentX, y - 2, segmentWidth, 2, new Color(a: 255, r: 255, g: 232, b: 165));
        }
    }

    private void DrawProfessionXpBar(Action<int, int, int, int, Color> fill)
    {
        if (_state == null)
            return;

        int percent;
        bool maximumLevelReached;

        if (_state.RecipeSelectionRequired && _state.Recipes.Length > 0)
        {
            var recipe = _state.Recipes[Math.Clamp(_recipeIndex, 0, _state.Recipes.Length - 1)];
            percent = recipe.ProfessionExperiencePercent;
            maximumLevelReached = recipe.ProfessionMaximumLevelReached;
        }
        else
        {
            percent = _state.ProfessionExperiencePercent;
            maximumLevelReached = _state.ProfessionMaximumLevelReached;
        }

        percent = Math.Clamp(percent, 0, 100);

        const int x = 55;
        const int y = 662;
        const int width = 700;
        const int height = 16;

        fill(x, y, width, height, new Color(a: 255, r: 28, g: 24, b: 20));

        var fillWidth = maximumLevelReached
            ? width
            : (int)Math.Round(width * percent / 100d);

        if (fillWidth > 0)
        {
            fill(
                x + 2,
                y + 2,
                Math.Max(1, fillWidth - 4),
                height - 4,
                new Color(a: 255, r: 190, g: 145, b: 66)
            );
        }
    }

    private static string FormatProfessionProgress(
        string name,
        int level,
        int maximumLevel,
        long experienceIntoLevel,
        long experienceRequiredForLevel,
        long experienceToNextLevel,
        int percent,
        bool maximumLevelReached
    )
    {
        var displayName = string.IsNullOrWhiteSpace(name) ? "Cooking" : name;
        var displayLevel = Math.Max(1, level);
        var displayMaximum = Math.Max(displayLevel, maximumLevel);

        if (maximumLevelReached)
            return $"{displayName} Lv {displayLevel}/{displayMaximum} • MAX LEVEL • 100%";

        return $"{displayName} Lv {displayLevel}/{displayMaximum} • " +
               $"{Math.Clamp(percent, 0, 100)}% • " +
               $"{experienceIntoLevel:N0}/{Math.Max(1L, experienceRequiredForLevel):N0} XP • " +
               $"{experienceToNextLevel:N0} XP to next level";
    }

    private void DrawStageProp(
        Action<int, int, int, int, Color> fill,
        CookingSessionState state,
        long elapsed
    )
    {
        var pulse = (int)((elapsed / 120) % 6);
        switch (state.StageType)
        {
            case CookingStageType.Chop:
                fill(650 + pulse * 4, 380 - pulse * 2, 12, 70, new Color(a: 255, r: 210, g: 210, b: 205));
                fill(615, 445, 160, 14, new Color(a: 255, r: 122, g: 82, b: 48));
                break;

            case CookingStageType.Stir:
                fill(640, 392, 155, 70, new Color(a: 255, r: 113, g: 76, b: 50));
                fill(660 + pulse * 4, 365, 10, 82, new Color(a: 255, r: 195, g: 165, b: 110));
                break;

            case CookingStageType.Heat:
                for (var flame = 0; flame < 5; ++flame)
                {
                    var height = 18 + ((pulse + flame) % 4) * 8;
                    fill(635 + flame * 30, 458 - height, 18, height, new Color(a: 230, r: 219, g: 105, b: 45));
                }
                fill(610, 388, 190, 35, new Color(a: 255, r: 72, g: 66, b: 58));
                break;

            case CookingStageType.Flip:
            {
                var arc = (int)((elapsed / 45) % 110);
                var rise = 55 - Math.Abs(55 - arc);
                fill(690 + arc / 3, 420 - rise, 42, 18, new Color(a: 255, r: 218, g: 169, b: 91));
                fill(620, 445, 175, 20, new Color(a: 255, r: 74, g: 68, b: 60));
                break;
            }

            case CookingStageType.Season:
                fill(705 + pulse * 2, 375, 28, 65, new Color(a: 255, r: 213, g: 203, b: 180));
                for (var grain = 0; grain < 6; ++grain)
                    fill(690 + grain * 16, 440 + ((grain + pulse) % 3) * 6, 4, 4, Color.White);
                break;

            case CookingStageType.Knead:
                fill(640, 415, 160, 48, new Color(a: 255, r: 218, g: 185, b: 137));
                fill(625 + pulse * 8, 390, 60, 24, new Color(a: 255, r: 196, g: 166, b: 128));
                break;

            case CookingStageType.Plate:
                fill(645, 378, 170, 90, new Color(a: 255, r: 224, g: 220, b: 202));
                fill(662, 394, 136, 58, new Color(a: 255, r: 47, g: 58, b: 47));
                break;
        }
    }

    private void DrawComicEvent(Action<int, int, int, int, Color> fill)
    {
        var remaining = _comicUntil - Environment.TickCount64;
        if (remaining <= 0)
            return;

        var age = 1_650 - remaining;
        switch (_comicEventType)
        {
            case CookingComicEventType.PanOverflow:
                for (var drop = 0; drop < 9; ++drop)
                {
                    var x = 610 + ((drop * 41 + (int)(age / 7)) % 300);
                    var y = 360 + ((drop * 29 + (int)(age / 12)) % 120);
                    fill(x, y, 9, 9, new Color(a: 220, r: 211, g: 151, b: 78));
                }
                break;

            case CookingComicEventType.EscapingIngredient:
            {
                var run = (int)Math.Min(360, age / 3);
                fill(540 + run, 420 - (run % 80) / 4, 34, 22, new Color(a: 255, r: 224, g: 138, b: 65));
                break;
            }

            case CookingComicEventType.SauceSplash:
                for (var splash = 0; splash < 11; ++splash)
                {
                    var x = 530 + ((splash * 53) % 390);
                    var y = 120 + ((splash * 71) % 330);
                    var size = 8 + splash % 4 * 4;
                    fill(x, y, size, size, new Color(a: 190, r: 159, g: 54, b: 37));
                }
                break;

            case CookingComicEventType.SmokeCloud:
                for (var cloud = 0; cloud < 10; ++cloud)
                {
                    var drift = (int)(age / 25);
                    fill(
                        560 + cloud * 34 + (cloud % 2 == 0 ? drift : -drift / 2),
                        430 - cloud * 13 - drift,
                        30 + cloud % 3 * 9,
                        30 + cloud % 3 * 9,
                        new Color(a: 115, r: 92, g: 90, b: 84)
                    );
                }
                break;

            case CookingComicEventType.FlyingFood:
            {
                var travel = (int)Math.Min(390, age / 3);
                var rise = 120 - Math.Abs(195 - travel) / 2;
                fill(520 + travel, 420 - Math.Max(0, rise), 42, 22, new Color(a: 255, r: 215, g: 140, b: 67));
                break;
            }

            case CookingComicEventType.WobblyPlate:
            {
                var wobble = ((int)(age / 80) % 2 == 0) ? -12 : 12;
                fill(665 + wobble, 390, 180, 80, new Color(a: 210, r: 232, g: 228, b: 208));
                fill(684 + wobble, 407, 142, 48, new Color(a: 210, r: 48, g: 62, b: 48));
                break;
            }
        }
    }

    private void DrawActionFeedback(Action<int, int, int, int, Color> fill)
    {
        var remaining = _feedbackUntil - Environment.TickCount64;
        if (remaining <= 0)
            return;

        if (_feedbackScore >= 90)
        {
            var pulse = 8 + (int)((remaining / 70) % 8);
            fill(492 - pulse, 60 - pulse, 456 + pulse * 2, 444 + pulse * 2, new Color(a: 28, r: 236, g: 208, b: 113));
            for (var spark = 0; spark < 8; ++spark)
            {
                var x = 540 + ((spark * 61 + (int)(remaining / 12)) % 390);
                var y = 110 + ((spark * 47 + (int)(remaining / 17)) % 340);
                fill(x, y, 7, 7, new Color(a: 230, r: 245, g: 218, b: 123));
            }
            return;
        }

        if (_feedbackScore < 40)
        {
            // Cartoony smoke/splatter: intentionally simple geometric FX so no new art asset is required.
            for (var cloud = 0; cloud < 7; ++cloud)
            {
                var drift = (int)((1_300 - Math.Max(0, remaining)) / 22);
                var x = 610 + cloud * 40 + (cloud % 2 == 0 ? drift : -drift / 2);
                var y = 405 - cloud * 9 - drift;
                var size = 24 + (cloud % 3) * 8;
                fill(x, y, size, size, new Color(a: 125, r: 72, g: 70, b: 66));
            }

            if (_feedbackStage is CookingStageType.Flip or CookingStageType.Chop)
            {
                var fly = (int)((1_300 - Math.Max(0, remaining)) / 8);
                fill(575 + fly, 390 - Math.Min(95, fly / 2), 38, 18, new Color(a: 245, r: 190, g: 120, b: 67));
            }
        }
    }

    private Label MakeLabel(string name, int x, int y, int w, int h, int font)
    {
        var label = new Label(this, name)
        {
            Font = Skin.DefaultFont,
            FontSize = font,
            AutoSizeToContents = false,
            TextColorOverride = Color.White,
            MouseInputEnabled = false,
            KeyboardInputEnabled = false,
            UserData = new Rectangle(x, y, w, h),
        };
        label.SetBounds(x, y, w, h);
        _fontSizes[label] = font;
        return label;
    }

    private Button MakeButton(string name, string text, int x, int y, int w, Action action)
    {
        var button = new Button(this, name)
        {
            Font = Skin.DefaultFont,
            FontSize = 11,
            Text = text,
            UserData = new Rectangle(x, y, w, 36),
        };
        button.SetBounds(x, y, w, 36);
        _fontSizes[button] = 11;
        button.Clicked += (_, _) => action();
        return button;
    }

    private static string StageName(CookingStageType type) => type switch
    {
        CookingStageType.Chop => "CHOP THE INGREDIENTS",
        CookingStageType.Stir => "STIR THE SAUCE",
        CookingStageType.Heat => "CONTROL THE HEAT",
        CookingStageType.Flip => "FLIP THE PAN",
        CookingStageType.Season => "SEASON IT",
        CookingStageType.Knead => "KNEAD THE DOUGH",
        _ => "PLATE THE MEAL",
    };

    private static string ErrorText(string code) => code switch
    {
        "RecipeNotFound" => "That recipe no longer exists.",
        "ProfessionLevelTooLow" => "Your Cooking level is too low.",
        "RecipeEventLocked" => "This recipe must be unlocked by an event.",
        "CoopRequired" => "This recipe requires two cooks.",
        "CoopNotAllowed" => "This recipe is solo only.",
        "PartnerUnavailable" => "Your partner must be in your Party, nearby, and available.",
        "PartnerRecipeLocked" => "Your partner has not unlocked this recipe or lacks the required level.",
        "MissingIngredients" => "The kitchen is missing ingredients.",
        "InventoryChanged" => "The ingredients changed before cooking started.",
        "NotYourStation" => "That is your partner's station right now!",
        "InviteDeclined" => "Your party member declined the cooking invite.",
        _ => code,
    };

    public void Destroy()
    {
        if (_destroyed) return;
        _destroyed = true;
        Hide();
        Parent?.RemoveChild(this, false);
        Dispose();
    }
}


internal sealed class CookingRecipePickerPanel(Base parent) : Base(parent, "CookingRecipePicker")
{
    public int SelectedSlot { get; set; } = -1;

    protected override void Render(SkinBase skin)
    {
        var renderer = skin.Renderer;
        var bounds = RenderBounds;

        renderer.DrawColor = new Color(242, 18, 13, 10);
        renderer.DrawFilledRect(bounds);

        renderer.DrawColor = new Color(255, 148, 101, 54);
        renderer.DrawFilledRect(new Rectangle(bounds.X, bounds.Y, bounds.Width, 2));
        renderer.DrawFilledRect(new Rectangle(bounds.X, bounds.Y + bounds.Height - 2, bounds.Width, 2));
        renderer.DrawFilledRect(new Rectangle(bounds.X, bounds.Y, 2, bounds.Height));
        renderer.DrawFilledRect(new Rectangle(bounds.X + bounds.Width - 2, bounds.Y, 2, bounds.Height));

        int SX(int value) => bounds.X + (int)Math.Round(value * bounds.Width / 550d);
        int SY(int value) => bounds.Y + (int)Math.Round(value * bounds.Height / 390d);
        int SW(int value) => Math.Max(1, (int)Math.Round(value * bounds.Width / 550d));
        int SH(int value) => Math.Max(1, (int)Math.Round(value * bounds.Height / 390d));

        for (var slot = 0; slot < 6; ++slot)
        {
            var column = slot % 2;
            var row = slot / 2;
            var x = SX(17 + column * 258);
            var y = SY(55 + row * 82);
            var w = SW(252);
            var h = SH(76);

            renderer.DrawColor = slot == SelectedSlot
                ? new Color(255, 231, 194, 112)
                : new Color(255, 91, 62, 39);
            renderer.DrawFilledRect(new Rectangle(x, y, w, h));

            renderer.DrawColor = new Color(255, 42, 30, 24);
            renderer.DrawFilledRect(
                new Rectangle(x + SW(2), y + SH(2), Math.Max(1, w - SW(4)), Math.Max(1, h - SH(4)))
            );

            renderer.DrawColor = slot == SelectedSlot
                ? new Color(255, 184, 129, 65)
                : new Color(255, 78, 54, 36);
            renderer.DrawFilledRect(new Rectangle(x + SW(5), y + SH(5), Math.Max(1, w - SW(10)), SH(4)));
        }

        renderer.DrawColor = new Color(255, 90, 62, 39);
        renderer.DrawFilledRect(new Rectangle(SX(18), SY(309), SW(514), SH(2)));
    }
}


internal sealed class CookingResultPanel(Base parent) : Base(parent, "CookingResultPanel")
{
    public CookingQuality Quality { get; set; }

    protected override void Render(SkinBase skin)
    {
        var renderer = skin.Renderer;
        var bounds = RenderBounds;

        var accent = Quality switch
        {
            CookingQuality.Perfect => new Color(255, 238, 201, 88),
            CookingQuality.Great => new Color(255, 86, 169, 100),
            CookingQuality.Decent => new Color(255, 185, 147, 86),
            _ => new Color(255, 158, 67, 53),
        };

        renderer.DrawColor = accent;
        renderer.DrawFilledRect(bounds);

        renderer.DrawColor = new Color(250, 23, 17, 13);
        renderer.DrawFilledRect(
            new Rectangle(bounds.X + 3, bounds.Y + 3, Math.Max(1, bounds.Width - 6), Math.Max(1, bounds.Height - 6))
        );

        int SX(int value) => bounds.X + (int)Math.Round(value * bounds.Width / 520d);
        int SY(int value) => bounds.Y + (int)Math.Round(value * bounds.Height / 410d);
        int SW(int value) => Math.Max(1, (int)Math.Round(value * bounds.Width / 520d));
        int SH(int value) => Math.Max(1, (int)Math.Round(value * bounds.Height / 410d));

        // Decorative plate / meal behind the text, deliberately built from primitives so it
        // matches the rest of the current mini-game without requiring external art.
        renderer.DrawColor = new Color(255, 216, 209, 183);
        renderer.DrawFilledRect(new Rectangle(SX(190), SY(119), SW(140), SH(46)));
        renderer.DrawColor = new Color(255, 57, 63, 43);
        renderer.DrawFilledRect(new Rectangle(SX(211), SY(129), SW(98), SH(26)));

        // Quality pips act like a simple result rating.
        var pips = Quality switch
        {
            CookingQuality.Perfect => 4,
            CookingQuality.Great => 3,
            CookingQuality.Decent => 2,
            _ => 1,
        };

        for (var index = 0; index < 4; ++index)
        {
            renderer.DrawColor = index < pips
                ? accent
                : new Color(255, 66, 49, 38);
            renderer.DrawFilledRect(new Rectangle(SX(204 + index * 30), SY(166), SW(18), SH(5)));
        }

        // Perfect/Great results get a restrained celebratory sparkle field; Burnt gets smoke.
        var now = Environment.TickCount64;
        if (Quality is CookingQuality.Perfect or CookingQuality.Great)
        {
            for (var spark = 0; spark < 10; ++spark)
            {
                var x = 70 + ((spark * 43 + (int)(now / 30)) % 380);
                var y = 48 + ((spark * 61 + (int)(now / 45)) % 225);
                renderer.DrawColor = new Color(
                    a: 190,
                    r: accent.R,
                    g: accent.G,
                    b: accent.B
                );
                renderer.DrawFilledRect(new Rectangle(SX(x), SY(y), SW(4), SH(4)));
            }
        }
        else if (Quality == CookingQuality.Burnt)
        {
            for (var cloud = 0; cloud < 6; ++cloud)
            {
                var drift = (int)((now / 35 + cloud * 17) % 45);
                renderer.DrawColor = new Color(a: 105, r: 90, g: 86, b: 80);
                renderer.DrawFilledRect(
                    new Rectangle(
                        SX(205 + cloud * 26 + drift / 4),
                        SY(125 - drift),
                        SW(20 + cloud % 3 * 6),
                        SH(20 + cloud % 3 * 6)
                    )
                );
            }
        }

        renderer.DrawColor = new Color(255, 84, 59, 39);
        renderer.DrawFilledRect(new Rectangle(SX(28), SY(335), SW(464), SH(2)));
    }
}
