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

        _recipe = MakeLabel("CookingRecipe", 62, 115, 286, 92, 15);
        _ingredients = MakeLabel("CookingIngredients", 62, 228, 286, 198, 11);
        _partner = MakeLabel("CookingPartner", 62, 448, 286, 78, 11);

        _stage = MakeLabel("CookingStage", 420, 112, 530, 86, 18);
        _stage.TextAlign = Pos.Center;
        _status = MakeLabel("CookingStatus", 420, 210, 530, 92, 12);
        _status.TextAlign = Pos.Center;
        _score = MakeLabel("CookingScore", 420, 314, 530, 60, 15);
        _score.TextAlign = Pos.Center;
        _players = MakeLabel("CookingPlayers", 420, 390, 530, 72, 11);
        _players.TextAlign = Pos.Center;

        _professionXp = MakeLabel("CookingProfessionXp", 55, 626, 700, 28, 11);
        _professionXp.TextColorOverride = new Color(255, 236, 210, 117);

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
        _solo = MakeButton("CookingSolo", "COOK SOLO", 253, 538, 100, StartSolo);

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
        _coop = MakeButton("CookingCoop", "COOK TOGETHER", 253, 578, 100, StartCoop);

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

        }
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
        _previousRecipe.IsHidden = _nextRecipe.IsHidden = _solo.IsHidden = _coop.IsHidden = !selecting;
        _previousPartner.IsHidden = _nextPartner.IsHidden = !selecting;
        var hideActions = selecting || state.WaitingForPartner || state.InvitePendingForYou || state.Complete;
        _action.IsHidden = hideActions;
        _actionSecondary.IsHidden = hideActions;
        _actionTertiary.IsHidden = hideActions;
        _accept.IsHidden = _decline.IsHidden = !state.InvitePendingForYou;

        ConfigureStageButtons(state);
        _action.IsDisabled = model.Pending || !state.YourTurn;
        _actionSecondary.IsDisabled = model.Pending || !state.YourTurn;
        _actionTertiary.IsDisabled = model.Pending || !state.YourTurn;
        _solo.IsDisabled = model.Pending;
        _coop.IsDisabled = model.Pending;

        RefreshSelection();

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
        _stage.Text = "Choose a recipe.";
        _status.Text = "Cook solo, or invite a nearby member of your Party.";
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
        renderer.DrawColor = new Color(a: 245, r: 31, g: 23, b: 18);
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

        Fill(15, 8, 970, 695, new Color(a: 255, r: 63, g: 38, b: 25));
        Fill(24, 18, 952, 675, new Color(a: 255, r: 25, g: 55, b: 37));
        Fill(480, 58, 480, 445, new Color(a: 255, r: 38, g: 30, b: 23));

        if (_state is { RecipeSelectionRequired: false, Complete: false, WaitingForPartner: false, StageDurationMs: > 0 } state)
        {
            var nowServer = Environment.TickCount64 + _serverOffset;
            var elapsed = Math.Clamp(nowServer - state.StageStartedUnixMs, 0, state.StageDurationMs);

            const int meterX = 535;
            const int meterY = 470;
            const int meterW = 370;
            const int meterH = 28;

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
                    new Color(a: 255, r: 78, g: 145, b: 72)
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
                    new Color(a: 255, r: 78, g: 145, b: 72)
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
            Fill(meterX, meterY + 36, timeWidth, 5, new Color(a: 255, r: 194, g: 164, b: 91));

            DrawStageProp(Fill, state, elapsed);
        }

        DrawProfessionXpBar(Fill);
        DrawActionFeedback(Fill);
        DrawComicEvent(Fill);
        base.Render(skin);
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

        const int x = 30;
        const int y = 676;
        const int width = 740;
        const int height = 14;

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
