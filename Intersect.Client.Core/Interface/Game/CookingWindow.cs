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
    private readonly Action<CookingRequestKind, Guid, Guid, bool> _send;
    private readonly Label _title;
    private readonly Label _recipe;
    private readonly Label _ingredients;
    private readonly Label _partner;
    private readonly Label _stage;
    private readonly Label _status;
    private readonly Label _score;
    private readonly Label _players;
    private readonly Button _previousRecipe;
    private readonly Button _nextRecipe;
    private readonly Button _solo;
    private readonly Button _coop;
    private readonly Button _previousPartner;
    private readonly Button _nextPartner;
    private readonly Button _action;
    private readonly Button _accept;
    private readonly Button _decline;
    private readonly Button _leave;

    private CookingSessionState? _state;
    private long _serverOffset;
    private int _recipeIndex;
    private int _partnerIndex;
    private bool _destroyed;

    public bool ExitRequested { get; private set; }

    public CookingWindow(
        Canvas canvas,
        Action<CookingRequestKind, Guid, Guid, bool> send
    ) : base(canvas, nameof(CookingWindow))
    {
        _canvas = canvas;
        _send = send;
        ShouldDrawBackground = false;
        MouseInputEnabled = true;

        _title = MakeLabel("CookingTitle", 20, 12, 800, 42, 22);
        _title.Text = "ROYAL KITCHEN";

        _recipe = MakeLabel("CookingRecipe", 30, 70, 430, 120, 14);
        _ingredients = MakeLabel("CookingIngredients", 30, 200, 430, 220, 11);
        _partner = MakeLabel("CookingPartner", 30, 430, 430, 70, 12);
        _stage = MakeLabel("CookingStage", 500, 80, 470, 120, 18);
        _status = MakeLabel("CookingStatus", 500, 210, 470, 100, 12);
        _score = MakeLabel("CookingScore", 500, 320, 470, 80, 15);
        _players = MakeLabel("CookingPlayers", 500, 410, 470, 100, 11);

        _previousRecipe = MakeButton("CookingPreviousRecipe", "< Recipe", 30, 520, 130, () =>
        {
            _recipeIndex = Math.Max(0, _recipeIndex - 1);
            RefreshSelection();
        });
        _nextRecipe = MakeButton("CookingNextRecipe", "Recipe >", 170, 520, 130, () =>
        {
            if (_state != null)
                _recipeIndex = Math.Min(Math.Max(0, _state.Recipes.Length - 1), _recipeIndex + 1);
            RefreshSelection();
        });
        _solo = MakeButton("CookingSolo", "COOK SOLO", 310, 520, 150, StartSolo);

        _previousPartner = MakeButton("CookingPreviousPartner", "< Friend", 30, 570, 130, () =>
        {
            _partnerIndex = Math.Max(0, _partnerIndex - 1);
            RefreshSelection();
        });
        _nextPartner = MakeButton("CookingNextPartner", "Friend >", 170, 570, 130, () =>
        {
            if (_state != null)
                _partnerIndex = Math.Min(Math.Max(0, _state.PartyCandidates.Length - 1), _partnerIndex + 1);
            RefreshSelection();
        });
        _coop = MakeButton("CookingCoop", "COOK TOGETHER", 310, 570, 150, StartCoop);

        _action = MakeButton("CookingAction", "DO IT!", 595, 540, 260, DoAction);
        _accept = MakeButton("CookingAccept", "ACCEPT COOKING INVITE", 540, 540, 220, () =>
            _send(CookingRequestKind.RespondInvite, Guid.Empty, Guid.Empty, true)
        );
        _decline = MakeButton("CookingDecline", "DECLINE", 770, 540, 140, () =>
            _send(CookingRequestKind.RespondInvite, Guid.Empty, Guid.Empty, false)
        );
        _leave = MakeButton("CookingLeave", "Leave Kitchen", 820, 650, 150, () => ExitRequested = true);

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

        if (_recipeIndex >= state.Recipes.Length) _recipeIndex = Math.Max(0, state.Recipes.Length - 1);
        if (_partnerIndex >= state.PartyCandidates.Length) _partnerIndex = Math.Max(0, state.PartyCandidates.Length - 1);

        var selecting = state.RecipeSelectionRequired;
        _previousRecipe.IsHidden = _nextRecipe.IsHidden = _solo.IsHidden = _coop.IsHidden = !selecting;
        _previousPartner.IsHidden = _nextPartner.IsHidden = !selecting;
        _action.IsHidden = selecting || state.WaitingForPartner || state.InvitePendingForYou || state.Complete;
        _accept.IsHidden = _decline.IsHidden = !state.InvitePendingForYou;

        _action.IsDisabled = model.Pending || !state.YourTurn;
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

            _status.Text = string.IsNullOrWhiteSpace(model.ErrorCode)
                ? state.Status
                : ErrorText(model.ErrorCode);

            _score.Text = state.Complete
                ? $"TEAM SCORE: {state.TeamScore}%\n{state.Quality}\n{state.RewardText}"
                : $"Stage score: {state.StageScore}%";

            _players.Text = string.Join(
                "\n",
                state.Participants.Select(player =>
                    $"{player.Name}: {player.Actions} action(s) • {player.Score}%"
                )
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
            $"{recipe.Name}\nCooking level {recipe.RequiredLevel} • {recipe.Experience:N0} base XP\n{lockText}";

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
    }

    private void StartSolo()
    {
        if (_state?.Recipes is not { Length: > 0 }) return;
        var recipe = _state.Recipes[Math.Clamp(_recipeIndex, 0, _state.Recipes.Length - 1)];
        _send(CookingRequestKind.StartRecipe, recipe.Id, Guid.Empty, false);
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
        _send(CookingRequestKind.StartRecipe, recipe.Id, partner.PlayerId, false);
    }

    private void DoAction() =>
        _send(CookingRequestKind.Action, Guid.Empty, Guid.Empty, false);

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
            var normalized = elapsed / (double)Math.Max(1, state.StageDurationMs);
            var difficultyCycles = 3.5d;
            var phase = normalized * difficultyCycles;
            var fraction = phase - Math.Floor(phase);
            var cursor = fraction <= 0.5d ? fraction * 2d : (1d - fraction) * 2d;

            const int meterX = 535;
            const int meterY = 470;
            const int meterW = 370;
            const int meterH = 28;

            Fill(meterX, meterY, meterW, meterH, new Color(a: 255, r: 24, g: 20, b: 17));

            var targetX = meterX + (int)(meterW * state.TargetPermille / 1000d);
            var toleranceW = Math.Max(8, (int)(meterW * state.TolerancePermille / 1000d));
            Fill(
                targetX - toleranceW / 2,
                meterY + 3,
                toleranceW,
                meterH - 6,
                new Color(a: 255, r: 78, g: 145, b: 72)
            );

            var cursorX = meterX + (int)(meterW * cursor);
            Fill(cursorX - 3, meterY - 5, 6, meterH + 10, Color.White);
        }

        base.Render(skin);
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
