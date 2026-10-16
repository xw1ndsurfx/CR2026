using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Input;
using Intersect.Client.MiniGames;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.MiniGames.Cooking;
using Intersect.Network.Packets.MiniGames;
using RendererBase = Intersect.Client.Framework.Gwen.Renderer.Base;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

internal sealed partial class CookingWindow : Base
{
    private readonly Canvas _canvas;
    private readonly Action<CookingRequestKind, Guid, Guid, bool, CookingActionInput> _send;
    private readonly Dictionary<Base, Rectangle> _layout = [];
    private readonly Label _recipe, _step, _help, _progress, _feedback, _partner, _result, _profession;
    private readonly Button _solo, _coop, _partnerNext, _accept, _decline, _again, _previous, _next;
    private readonly Button[] _cards = new Button[6];
    private readonly Label[] _cardLabels = new Label[6];
    private readonly ImagePanel[] _cardImages = new ImagePanel[6];
    private readonly ScrollControl _ingredientScroll;
    private readonly IngredientList _ingredients;
    private readonly CookingWorktop _worktop;
    private CookingSessionState? _state;
    private CookingClientModel? _model;
    private int _selected, _page, _partnerIndex;
    private long _actionSequence = -1, _feedbackUntil;
    private long _serverOffset;
    private long _packetSequence = -1, _comicSequence = -1;
    private readonly Dictionary<Label, int> _fonts = [];
    private bool _destroyed;
    public bool ExitRequested { get; private set; }

    public CookingWindow(Canvas canvas, Action<CookingRequestKind, Guid, Guid, bool, CookingActionInput> send)
        : base(canvas, nameof(CookingWindow))
    {
        _canvas = canvas; _send = send; MouseInputEnabled = true;
        Text("KitchenHeading", "ROYAL KITCHEN", 35, 22, 920, 40, 22);
        Text("RecipeBookHeading", "RECIPE BOOK", 35, 95, 255, 28, 13);
        _recipe = Text("RecipeName", "Choose your dish", 35, 128, 255, 65, 16);
        _ingredientScroll = Place(new ScrollControl(this, "RecipeIngredients"), 35, 207, 255, 298);
        _ingredientScroll.EnableScroll(false, true);
        _ingredientScroll.MouseInputEnabled = true;
        _ingredients = new IngredientList(_ingredientScroll);
        _partner = Text("KitchenPartner", "", 35, 511, 255, 42, 10);
        _partnerNext = Button("PartnerNext", "Choose partner >", 35, 560, 255, () => { ++_partnerIndex; });
        _solo = Button("StartSolo", "COOK SOLO", 35, 603, 122, () => Start(false));
        _coop = Button("StartTogether", "COOK TOGETHER", 165, 603, 125, () => Start(true));
        _step = Text("KitchenStep", "Choose a recipe", 330, 98, 650, 34, 18);
        _help = Text("KitchenHelp", "Pick a dish, check the ingredients, then start cooking.", 330, 138, 650, 65, 12);
        _worktop = Place(new CookingWorktop(this, Submit), 330, 207, 650, 350);
        _progress = Text("KitchenProgress", "", 330, 566, 650, 30, 11);
        _feedback = Text("KitchenFeedback", "", 330, 603, 650, 30, 15);
        _result = Text("KitchenResult", "", 350, 250, 610, 220, 16);
        _accept = Button("AcceptKitchenInvite", "ACCEPT", 420, 491, 180,
            () => _send(CookingRequestKind.RespondInvite, Guid.Empty, Guid.Empty, true, CookingActionInput.Primary));
        _decline = Button("DeclineKitchenInvite", "DECLINE", 620, 491, 180,
            () => _send(CookingRequestKind.RespondInvite, Guid.Empty, Guid.Empty, false, CookingActionInput.Primary));
        _again = Button("CookAnotherDish", "CHOOSE ANOTHER RECIPE", 450, 494, 400,
            () => _send(CookingRequestKind.ReturnToRecipes, Guid.Empty, Guid.Empty, false, CookingActionInput.Primary));
        for (var index = 0; index < _cards.Length; ++index)
        {
            var slot = index;
            var card = Button("RecipeCard" + index, "", 330 + index % 2 * 330, 210 + index / 2 * 108, 320,
                () => { _selected = _page * 6 + slot; });
            _layout[card] = new Rectangle(330 + index % 2 * 330, 210 + index / 2 * 108, 320, 96);
            _cards[index] = card;
            _cardImages[index] = new ImagePanel(card) { MaintainAspectRatio = true, MouseInputEnabled = false };
            _cardLabels[index] = new Label(card)
            {
                AutoSizeToContents = false, Font = Skin.DefaultFont, FontSize = 11, MouseInputEnabled = false,
                WrappingBehavior = WrappingBehavior.Wrapped, TextColorOverride = Color.White,
            };
        }
        _previous = Button("PreviousRecipes", "<", 330, 566, 52, () => _page = Math.Max(0, _page - 1));
        _next = Button("NextRecipes", ">", 928, 566, 52, () => ++_page);
        _profession = Text("KitchenProfession", "", 35, 663, 720, 26, 11);
        Button("LeaveKitchen", "LEAVE KITCHEN", 790, 661, 190, () => ExitRequested = true);
        ResizeToCanvas();
    }

    private T Place<T>(T control, int x, int y, int width, int height) where T : Base
    { _layout[control] = new Rectangle(x, y, width, height); return control; }

    private Label Text(string name, string text, int x, int y, int width, int height, int size)
    {
        var label = Place(new Label(this, name)
        {
            Text = text, Font = Skin.DefaultFont, FontSize = size, AutoSizeToContents = false,
            WrappingBehavior = WrappingBehavior.Wrapped, TextColorOverride = Color.White, MouseInputEnabled = false,
        }, x, y, width, height);
        _fonts[label] = size;
        return label;
    }

    private Button Button(string name, string text, int x, int y, int width, Action action)
    {
        var button = Place(new Button(this, name) { Text = text, Font = Skin.DefaultFont, FontSize = 11 }, x, y, width, 36);
        button.Clicked += (_, _) => action(); return button;
    }

    public void ResizeToCanvas()
    {
        SetBounds(0, 0, _canvas.Width, _canvas.Height);
        var scale = Math.Min(Width / 1024f, Height / 720f);
        var left = (Width - 1024 * scale) / 2; var top = (Height - 720 * scale) / 2;
        foreach (var (control, rectangle) in _layout)
            control.SetBounds((int)(left + rectangle.X * scale), (int)(top + rectangle.Y * scale),
                Math.Max(1, (int)(rectangle.Width * scale)), Math.Max(1, (int)(rectangle.Height * scale)));
        foreach (var (label, size) in _fonts) label.FontSize = Math.Max(8, (int)(size * scale));
        foreach (var control in _layout.Keys.OfType<Button>()) control.FontSize = Math.Max(8, (int)(11 * scale));
        foreach (var label in _cardLabels) label.FontSize = Math.Max(8, (int)(11 * scale));
        foreach (var (card, index) in _cards.Select((card, index) => (card, index)))
        {
            _cardImages[index].SetBounds(8, 12, Math.Max(20, card.Height - 24), Math.Max(20, card.Height - 24));
            _cardLabels[index].SetBounds(card.Height, 8, Math.Max(1, card.Width - card.Height - 8), card.Height - 16);
        }
    }

    public void Update(CookingClientModel model)
    {
        ResizeToCanvas(); _model = model;
        if (model.Current is not { State: { } state } packet) return;
        var previous = _state; _state = state;
        if (_packetSequence != packet.Sequence)
        {
            _packetSequence = packet.Sequence;
            _serverOffset = packet.ServerUnixMs - Environment.TickCount64;
        }
        if (previous?.RecipeId != state.RecipeId && state.RecipeId != Guid.Empty)
            Play(state.StartSound);
        if (state.Complete && previous?.Complete != true)
            Play(state.Quality switch { CookingQuality.Perfect => state.PerfectSoundRecipe,
                CookingQuality.Great => state.GreatSound, CookingQuality.Burnt => state.BurntSound, _ => state.CompleteSound });
        if (state.ActionSequence != _actionSequence)
        {
            if (_actionSequence >= 0)
            {
                _feedback.Text = state.LastActionScore >= 90 ? "PERFECT!" : state.LastActionScore >= 70 ? "NICELY DONE!" : "Keep practicing!";
                _feedback.TextColorOverride = state.LastActionScore >= 70 ? new Color(110, 230, 135) : new Color(245, 160, 90);
                _feedbackUntil = Environment.TickCount64 + 1100;
                Play(state.LastActionScore >= 90 ? state.PerfectSound : state.LastActionScore < 40 ? state.MishapSound : state.ActionSound);
            }
            _actionSequence = state.ActionSequence;
        }
        if (_comicSequence != state.ComicEventSequence)
        {
            if (_comicSequence >= 0 && !string.IsNullOrWhiteSpace(state.ComicEventText))
            { _feedback.Text = state.ComicEventText; _feedbackUntil = Environment.TickCount64 + 1700; }
            _comicSequence = state.ComicEventSequence;
        }
        if (previous?.InvitePendingForYou != true && state.InvitePendingForYou) Play(state.InviteSound);
        if (previous?.PartnerId != state.PartnerId && state.PartnerId != Guid.Empty) Play(state.PartnerJoinedSound);
        var selecting = state.RecipeSelectionRequired;
        _selected = Math.Clamp(_selected, 0, Math.Max(0, state.Recipes.Length - 1));
        _page = Math.Clamp(_page, 0, Math.Max(0, (state.Recipes.Length - 1) / 6));
        _partnerIndex %= Math.Max(1, state.PartyCandidates.Length);
        var recipe = selecting ? state.Recipes.ElementAtOrDefault(_selected) : null;
        _recipe.Text = recipe?.Name ?? state.RecipeName;
        _ingredients.Items = recipe?.Ingredients ?? state.RecipeIngredients;
        _ingredients.SetSize(230, Math.Max(1, _ingredients.Items.Length * 48));
        _ingredientScroll.SetInnerSize(230, _ingredients.Height);
        _partner.Text = !selecting ? state.PartnerId == Guid.Empty ? "Cooking solo" : "Cooking with " + state.PartnerName
            : state.PartyCandidates.ElementAtOrDefault(_partnerIndex)?.Name is { } partner ? "Partner: " + partner : "No party partner available";
        _profession.Text = selecting && recipe != null
            ? $"{recipe.ProfessionName} level {recipe.ProfessionLevel} / {recipe.ProfessionMaximumLevel}  -  {recipe.ProfessionExperiencePercent}% XP"
            : $"{state.ProfessionName} level {state.ProfessionLevel} / {state.ProfessionMaximumLevel}  -  {state.ProfessionExperiencePercent}% XP";
        _solo.IsHidden = _coop.IsHidden = _partnerNext.IsHidden = !selecting;
        _solo.IsDisabled = model.Pending || recipe == null || !recipe.Unlocked || !recipe.AllowSolo || recipe.RequireCoop;
        _coop.IsDisabled = model.Pending || recipe == null || !recipe.Unlocked || !recipe.AllowCoop || state.PartyCandidates.Length == 0;
        for (var index = 0; index < _cards.Length; ++index)
        {
            var choice = state.Recipes.ElementAtOrDefault(_page * 6 + index);
            _cards[index].IsHidden = !selecting || choice == null;
            if (choice == null) continue;
            _cardLabels[index].Text = (choice.Id == recipe?.Id ? "> " : "") + choice.Name + "\nLevel " + choice.RequiredLevel +
                (choice.Unlocked ? "" : "\n" + choice.LockedReason);
            _cardImages[index].Texture = CookingArt.ItemTexture(choice.Ingredients.FirstOrDefault()?.ItemId ?? Guid.Empty);
        }
        _previous.IsHidden = _next.IsHidden = !selecting;
        _previous.IsDisabled = _page == 0; _next.IsDisabled = (_page + 1) * 6 >= state.Recipes.Length;
        _result.IsHidden = !state.Complete;
        _again.IsHidden = !state.Complete || !state.IsHost;
        _accept.IsHidden = _decline.IsHidden = !state.InvitePendingForYou;
        _accept.IsDisabled = _decline.IsDisabled = model.Pending;
        var playing = !selecting && !state.Complete && !state.WaitingForPartner && !state.InvitePendingForYou;
        _worktop.IsHidden = !playing;
        _worktop.Configure(state, playing && state.YourTurn && !model.Pending, _serverOffset);
        _step.Text = selecting ? "CHOOSE YOUR DISH" : state.Complete ? "DINNER IS READY!" : state.WaitingForPartner || state.InvitePendingForYou
            ? "COOK TOGETHER" : $"{state.StageIndex + 1}/{state.StageCount}  {state.StageType.ToString().ToUpperInvariant()}";
        _help.Text = selecting ? recipe?.LockedReason is { Length: > 0 } reason ? reason : "Choose a recipe on the right. Check your ingredients, then cook solo or with a partner."
            : state.Complete ? "Your dish is ready. Try another recipe to improve your technique."
            : state.InvitePendingForYou ? state.HostName + " invites you to cook " + state.RecipeName
            : state.WaitingForPartner ? "Waiting for your partner to accept..."
            : !state.YourTurn ? "Your partner is working at this station. Watch them prepare the dish."
            : _worktop.Instruction;
        var remaining = Math.Max(0, state.StageDurationMs - (Environment.TickCount64 + _serverOffset - state.StageStartedUnixMs));
        _progress.Text = selecting ? $"Recipe page {_page + 1} / {Math.Max(1, (state.Recipes.Length + 5) / 6)}" : playing
            ? $"{state.CompletedActions} / {state.RequiredActions} actions   |   {(remaining + 999) / 1000}s   |   Combo x{state.Combo}"
            : state.Status;
        _feedback.IsHidden = Environment.TickCount64 >= _feedbackUntil || !playing;
        _result.Text = state.Complete ? $"{state.Quality.ToString().ToUpperInvariant()}\n\n{state.RewardText}\n\n+{state.ProfessionExperienceAwarded} {state.ProfessionName} XP\nTeam score: {state.TeamScore}" : "";
        if (model.ErrorCode.Length > 0) _progress.Text = ErrorText(model.ErrorCode);
    }

    private void Start(bool coop)
    {
        if (_state?.Recipes.ElementAtOrDefault(_selected) is not { Unlocked: true } recipe || _model?.Pending == true) return;
        _send(CookingRequestKind.StartRecipe, recipe.Id,
            coop ? _state.PartyCandidates.ElementAtOrDefault(_partnerIndex)?.PlayerId ?? Guid.Empty : Guid.Empty,
            false, CookingActionInput.Primary);
    }

    private void Submit(CookingActionInput input)
    {
        if (_model?.Pending == true || _state?.YourTurn != true) return;
        _send(CookingRequestKind.Action, Guid.Empty, Guid.Empty, false, input);
    }
    private static void Play(string? sound) { if (!string.IsNullOrWhiteSpace(sound)) Intersect.Client.Core.Audio.AddGameSound(sound, false); }
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

    public void Destroy() { if (_destroyed) return; _destroyed = true; Parent?.RemoveChild(this, false); Dispose(); }

    protected override void Render(SkinBase skin)
    {
        CookingArt.Fill(skin.Renderer, new Color(245, 20, 16, 15), 0, 0, Width, Height);
        var scale = Math.Min(Width / 1024f, Height / 720f);
        var x = (int)((Width - 1024 * scale) / 2); var y = (int)((Height - 720 * scale) / 2);
        void Fill(int bx, int by, int width, int height, Color color) => CookingArt.Fill(skin.Renderer, color,
            x + (int)(bx * scale), y + (int)(by * scale), (int)(width * scale), (int)(height * scale));
        DrawKitchenEnvironment(Fill, Environment.TickCount64);
        Fill(20, 85, 285, 560, new Color(246, 58, 39, 29));
        Fill(318, 85, 674, 560, new Color(242, 35, 28, 22));
    }

    private sealed class IngredientList(Base parent) : Base(parent)
    {
        internal CookingIngredientState[] Items = [];
        protected override void Render(SkinBase skin)
        {
            for (var i = 0; i < Items.Length; ++i)
            {
                var item = Items[i];
                CookingArt.DrawItem(skin.Renderer, item.ItemId, new Rectangle(4, i * 48 + 5, 36, 36), item.Name, skin);
                skin.Renderer.DrawColor = item.Available >= item.Needed ? Color.White : new Color(245, 135, 100);
                skin.Renderer.RenderText(Skin.DefaultFont,9,new Point(48, i * 48 + 4),item.Name.Length > 23 ? item.Name[..22] + "..." : item.Name);
                skin.Renderer.RenderText(Skin.DefaultFont,9,new Point(48, i * 48 + 22),$"{item.Available} / {item.Needed}");
            }
        }
    }
}

internal static class CookingArt
{
    internal static IGameTexture? ItemTexture(Guid id) => ItemDescriptor.TryGet(id, out var item) && !string.IsNullOrWhiteSpace(item.Icon)
        ? GameContentManager.Current.GetTexture(TextureType.Item, item.Icon) : null;
    internal static void Fill(RendererBase renderer, Color color, int x, int y, int w, int h)
    { if (w <= 0 || h <= 0) return; renderer.DrawColor = color; renderer.DrawFilledRect(new Rectangle(x, y, w, h)); }
    internal static void Disc(RendererBase renderer, Color color, int x, int y, int radius)
    {
        for (var row = -radius; row <= radius; ++row)
        {
            var half = (int)Math.Sqrt(radius * radius - row * row);
            Fill(renderer, color, x - half, y + row, half * 2 + 1, 1);
        }
    }
    internal static void DrawItem(RendererBase renderer, Guid id, Rectangle bounds, string name, SkinBase skin)
    {
        if (ItemTexture(id) is { } texture)
        {
            var scale = Math.Min(bounds.Width / (float)texture.Width, bounds.Height / (float)texture.Height);
            var w = Math.Max(1, (int)(texture.Width * scale)); var h = Math.Max(1, (int)(texture.Height * scale));
            renderer.DrawColor = Color.White;
            renderer.DrawTexturedRect(texture, new Rectangle(bounds.X + (bounds.Width - w) / 2, bounds.Y + (bounds.Height - h) / 2, w, h), Color.White);
        }
        else
        {
            Disc(renderer, new Color(220, 162, 77), bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2, Math.Max(3, bounds.Width / 3));
            renderer.DrawColor = Color.Black;
            renderer.RenderText(skin.DefaultFont,10,new Point(bounds.X + bounds.Width / 2 - 3, bounds.Y + bounds.Height / 2 - 6),name.Length > 0 ? name[..1] : "?");
        }
    }
}

internal sealed class CookingWorktop(Base parent, Action<CookingActionInput> submit) : Base(parent, "CookingWorktop")
{
    private CookingSessionState? _state;
    private bool _enabled, _placed, _dragging, _locked;
    private int _stage = -2;
    private Point _point, _start, _last;
    private double _rotation, _lastAngle;
    private int _distance;
    private long _offset, _flash;
    private Rectangle Source => new(12, Height / 2 - 40, 80, 80);
    private Rectangle Station => new(Width / 4, 24, Width * 3 / 4 - 20, Height - 85);
    private Point Center => new(Station.X + Station.Width / 2, Station.Y + Station.Height / 2);
    internal string Instruction => !_placed && _state?.StageType != CookingStageType.Plate
        ? "Drag the ingredient from the tray onto the highlighted work surface."
        : _state?.StageType switch
        {
            CookingStageType.Chop => "Click the food to chop when the knife marker enters the green zone.",
            CookingStageType.Stir => "Hold the spoon and draw circles in the bowl. Work with the green rhythm.",
            CookingStageType.Heat => "Use the mouse wheel or - / + on the stove. Match the heat to the green zone.",
            CookingStageType.Flip => "Grab the food, flick it upward, then release when the marker is green.",
            CookingStageType.Season => "Drag the shaker over the dish to add seasoning. Right-click to remove some.",
            CookingStageType.Knead => _state.CompletedActions % 2 == 0
                ? "Hold the dough and drag LEFT when the marker enters the green zone."
                : "Now knead RIGHT when the marker enters the green zone.",
            CookingStageType.Plate => "Drag the ingredient onto the highlighted plate.",
            _ => "Prepare the dish.",
        };

    internal void Configure(CookingSessionState state, bool enabled, long offset)
    {
        if (_stage != state.StageIndex || _state?.RecipeId != state.RecipeId)
        {
            _stage = state.StageIndex; _dragging = false; _rotation = 0; _distance = 0;
            _placed = state.StageType is CookingStageType.Heat or CookingStageType.Season;
        }
        if (_state != null && _state.ActionSequence != state.ActionSequence && state.CompletedActions > 0) _flash = Environment.TickCount64 + 350;
        if (state.CompletedActions > 0) _placed = true;
        _state = state; _enabled = enabled; _locked = !enabled; _offset = offset;
        if (!state.YourTurn || state.Complete) _dragging = false;
    }
    private static bool Contains(Rectangle rect, Point p) => p.X >= rect.X && p.Y >= rect.Y && p.X < rect.X + rect.Width && p.Y < rect.Y + rect.Height;
    private void Send(CookingActionInput input)
    {
        if (!_enabled || _locked) return;
        _locked = true; _flash = Environment.TickCount64 + 350; _distance = 0; _rotation = 0; submit(input);
    }
    protected override void OnMouseDown(MouseButton button, Point mousePosition, bool userAction = true)
    {
        base.OnMouseDown(button, mousePosition, userAction);
        if (!_enabled || _locked || _state == null) return;
        _point = _start = _last = CanvasPosToLocal(mousePosition);
        if (button == MouseButton.Right && _state.StageType == CookingStageType.Season) { Send(CookingActionInput.Secondary); return; }
        if (button != MouseButton.Left) return;
        if (!_placed || _state.StageType == CookingStageType.Plate)
        { _dragging = Contains(Source, _point); return; }
        if (!Contains(Station, _point))
        {
            if (_state.StageType == CookingStageType.Heat && _point.Y >= Height - 70)
                Send(_point.X < Station.X + Station.Width / 2 ? CookingActionInput.Secondary : CookingActionInput.Primary);
            return;
        }
        if (_state.StageType == CookingStageType.Chop) { Send(CookingActionInput.Primary); return; }
        _dragging = true; _lastAngle = Math.Atan2(_point.Y - Center.Y, _point.X - Center.X);
    }
    protected override void OnMouseMoved(int x, int y, int dx, int dy)
    {
        base.OnMouseMoved(x, y, dx, dy);
        _point = CanvasPosToLocal(new Point(x, y));
        if (!_dragging || !_placed || _state == null || _locked) { _last = _point; return; }
        if (_state.StageType == CookingStageType.Stir)
        {
            var angle = Math.Atan2(_point.Y - Center.Y, _point.X - Center.X);
            var change = Math.IEEERemainder(angle - _lastAngle, Math.PI * 2);
            if (Math.Abs(change) < Math.PI / 2) _rotation += Math.Abs(change);
            _lastAngle = angle;
            if (_rotation >= Math.PI / 2) Send(_state.CompletedActions % 2 == 0 ? CookingActionInput.Primary : CookingActionInput.Secondary);
        }
        else if (_state.StageType is CookingStageType.Knead or CookingStageType.Season)
        {
            _distance += _point.X - _last.X;
            var threshold = Math.Max(20, Width / 10);
            if (_state.StageType == CookingStageType.Season && Math.Abs(_distance) >= threshold) Send(CookingActionInput.Primary);
            else if (_state.StageType == CookingStageType.Knead && Math.Abs(_distance) >= threshold)
                Send(_distance < 0 ? CookingActionInput.Primary : CookingActionInput.Secondary);
        }
        _last = _point;
    }
    protected override void OnMouseUp(MouseButton button, Point mousePosition, bool userAction = true)
    {
        base.OnMouseUp(button, mousePosition, userAction);
        if (button != MouseButton.Left) return;
        var point = CanvasPosToLocal(mousePosition);
        if (_dragging && _enabled && !_locked && _state != null)
        {
            if (_state.StageType == CookingStageType.Plate && Contains(Station, point))
            {
                var zone = Math.Clamp((point.X - Station.X) * 3 / Math.Max(1, Station.Width), 0, 2);
                Send((CookingActionInput)zone);
            }
            else if (!_placed) _placed = Contains(Station, point);
            else if (_state.StageType == CookingStageType.Flip && _start.Y - point.Y >= Math.Max(25, Height / 6)) Send(CookingActionInput.Primary);
        }
        _dragging = false; _distance = 0; _rotation = 0;
    }
    protected override bool OnMouseWheeled(int delta)
    {
        if (_state?.StageType != CookingStageType.Heat || delta == 0) return false;
        Send(delta > 0 ? CookingActionInput.Primary : CookingActionInput.Secondary); return true;
    }
    protected override void Render(SkinBase skin)
    {
        if (_state is not { } state) return;
        var renderer = skin.Renderer;
        CookingArt.Fill(renderer, new Color(83, 55, 35), 0, 0, Width, Height);
        for (var y = 5; y < Height; y += 28) CookingArt.Fill(renderer, new Color(96, 64, 41), 0, y, Width, 2);
        CookingArt.Fill(renderer, new Color(45, 33, 26), 6, 12, 98, Height - 75);
        var ingredientIds = state.StageIngredientItemIds.Length > 0 ? state.StageIngredientItemIds : state.RecipeIngredients.Select(item => item.ItemId).ToArray();
        var id = ingredientIds.ElementAtOrDefault(state.CompletedActions % Math.Max(1, ingredientIds.Length));
        var ingredientName = state.RecipeIngredients.FirstOrDefault(item => item.ItemId == id)?.Name ?? "Ingredient";
        CookingArt.DrawItem(renderer, id, Source, ingredientName, skin);
        renderer.DrawColor = Color.White; renderer.RenderText(Skin.DefaultFont,9,new Point(12, 20),Intersect.Client.Localization.ClientLanguages.Text("INGREDIENTS"));
        var center = Center;
        var radius = Math.Max(15, Math.Min(Station.Width, Station.Height) / 2 - 12);
        if (state.StageType is CookingStageType.Chop or CookingStageType.Knead)
        {
            CookingArt.Fill(renderer, new Color(188, 141, 85), Station.X, Station.Y, Station.Width, Station.Height);
            for (var y = Station.Y + 12; y < Station.Y + Station.Height; y += 24)
                CookingArt.Fill(renderer, new Color(168, 119, 70), Station.X + 8, y, Station.Width - 16, 1);
        }
        else if (state.StageType == CookingStageType.Plate)
        {
            var target = CookingStageRules.PlateZoneFromTarget(state.TargetPermille);
            for (var zone = 0; zone < 3; ++zone)
            {
                var px = Station.X + (zone * 2 + 1) * Station.Width / 6;
                var r = Math.Max(10, Math.Min(Station.Width / 7, Station.Height / 3));
                CookingArt.Disc(renderer, zone == target ? new Color(95, 220, 130) : new Color(175, 163, 145), px, center.Y, r + 4);
                CookingArt.Disc(renderer, new Color(230, 223, 206), px, center.Y, r);
                if (state.CompletedActions > 0 && zone == CookingStageRules.PlateZoneFromTarget(state.MeterPermille)) CookingArt.DrawItem(renderer, ingredientIds.ElementAtOrDefault((state.CompletedActions - 1) % Math.Max(1, ingredientIds.Length)), new Rectangle(px - 20, center.Y - 20, 40, 40), ingredientName, skin);
            }
        }
        else
        {
            CookingArt.Disc(renderer, new Color(136, 142, 145), center.X, center.Y, radius + 6);
            CookingArt.Disc(renderer, new Color(39, 42, 43), center.X, center.Y, radius);
            CookingArt.Fill(renderer, new Color(35, 34, 31), center.X + radius - 4, center.Y - 10, 45, 20);
            if (state.StageType is CookingStageType.Heat or CookingStageType.Flip)
                for (var flame = 0; flame < 7; ++flame)
                {
                    var h = 10 + (int)(8 * (1 + Math.Sin(Environment.TickCount64 / 120.0 + flame)));
                    CookingArt.Fill(renderer, new Color(244, 130 + flame * 8, 30), center.X - 65 + flame * 20, center.Y + radius - 8, 10, h);
                }
        }
        if (_placed && state.StageType != CookingStageType.Plate)
        {
            var foodY = center.Y - 35;
            if (state.StageType == CookingStageType.Flip)
            {
                if (_dragging) foodY += Math.Clamp(_point.Y - _start.Y, -100, 0);
                else if (_flash > Environment.TickCount64) foodY -= (int)(Math.Sin(Math.PI * (350 - (_flash - Environment.TickCount64)) / 350d) * 80);
            }
            CookingArt.DrawItem(renderer, id, new Rectangle(center.X - 35, foodY, 70, 70), ingredientName, skin);
            if (state.StageType == CookingStageType.Chop)
            {
                for (var piece = 0; piece < Math.Min(6, state.CompletedActions); ++piece)
                    CookingArt.DrawItem(renderer, ingredientIds.ElementAtOrDefault(piece % Math.Max(1, ingredientIds.Length)), new Rectangle(center.X - 75 + piece * 25, center.Y + 35, 24, 24), ingredientName, skin);
                var knifeX = center.X + 38; var knifeY = center.Y - 60 + (_flash > Environment.TickCount64 ? 40 : 0);
                CookingArt.Fill(renderer, new Color(225, 231, 230), knifeX, knifeY, 9, 65);
                CookingArt.Fill(renderer, new Color(65, 38, 24), knifeX, knifeY - 24, 10, 24);
            }
            else if (state.StageType == CookingStageType.Stir)
            {
                var a = _dragging ? Math.Atan2(_point.Y - center.Y, _point.X - center.X) : Environment.TickCount64 / 900d;
                var px = center.X + (int)(Math.Cos(a) * radius * .55); var py = center.Y + (int)(Math.Sin(a) * radius * .55);
                renderer.DrawColor = new Color(204, 173, 120); renderer.DrawLine(px, py, px + 30, py - 45);
                CookingArt.Disc(renderer, new Color(204, 173, 120), px, py, 9);
            }
            if (state.StageType is CookingStageType.Heat or CookingStageType.Stir)
                for (var steam = 0; steam < 4; ++steam)
                {
                    var travel = (Environment.TickCount64 / 35 + steam * 12) % 45;
                    CookingArt.Disc(renderer, new Color(100, 220, 220, 215), center.X - 35 + steam * 22, center.Y - 48 - (int)travel, 3);
                }
        }
        if (_placed && state.StageType == CookingStageType.Knead && _dragging)
        {
            CookingArt.Disc(renderer, new Color(218, 171, 128), center.X - 45 + (_point.X - _start.X) / 5, center.Y, 15);
            CookingArt.Disc(renderer, new Color(218, 171, 128), center.X + 45 + (_point.X - _start.X) / 5, center.Y, 15);
        }
        if (_placed && state.StageType == CookingStageType.Season && (_dragging || _flash > Environment.TickCount64))
        {
            CookingArt.DrawItem(renderer, id, new Rectangle(_point.X - 24, _point.Y - 50, 48, 48), ingredientName, skin);
            for (var grain = 0; grain < 7; ++grain)
                CookingArt.Fill(renderer, new Color(234, 209, 142), center.X - 25 + grain * 8,
                    center.Y - 48 + (int)((Environment.TickCount64 / 12 + grain * 7) % 50), 3, 3);
        }
        if (_dragging && (!_placed || state.StageType == CookingStageType.Plate))
            CookingArt.DrawItem(renderer, id, new Rectangle(_point.X - 26, _point.Y - 26, 52, 52), ingredientName, skin);
        if (!_placed && state.StageType != CookingStageType.Plate)
        {
            CookingArt.Fill(renderer, new Color(100, 100, 230, 130), Station.X, Station.Y, Station.Width, 4);
            renderer.DrawColor = Color.White;
            renderer.RenderText(Skin.DefaultFont,11,new Point(center.X - 50, center.Y + 50),Intersect.Client.Localization.ClientLanguages.Text("DROP HERE"));
        }
        var meter = state.StageType is CookingStageType.Heat or CookingStageType.Season ? state.MeterPermille
            : CookingStageRules.TimingCursorPermille(Environment.TickCount64 + _offset - state.StageStartedUnixMs, state.StageDurationMs, state.StageDifficulty);
        var barX = Station.X; var barY = Height - 36; var barWidth = Station.Width;
        CookingArt.Fill(renderer, new Color(30, 25, 21), barX, barY, barWidth, 18);
        var tolerance = CookingStageRules.TargetTolerance(state.StageDifficulty);
        var targetLeft = Math.Max(0, state.TargetPermille - tolerance / 2);
        var targetRight = Math.Min(1000, state.TargetPermille + tolerance / 2);
        CookingArt.Fill(renderer, new Color(83, 181, 100), barX + targetLeft * barWidth / 1000, barY, Math.Max(1, (targetRight - targetLeft) * barWidth / 1000), 18);
        CookingArt.Fill(renderer, Color.White, barX + meter * (barWidth - 3) / 1000, barY - 3, 3, 24);
        if (state.StageType == CookingStageType.Heat)
        {
            renderer.DrawColor = Color.White;
            renderer.RenderText(Skin.DefaultFont,14,new Point(Station.X + 20, Height - 61),Intersect.Client.Localization.ClientLanguages.Text("-  LOWER HEAT"));
            renderer.RenderText(Skin.DefaultFont,14,new Point(Width / 2 + 20, Height - 61),Intersect.Client.Localization.ClientLanguages.Text("+  RAISE HEAT"));
        }
    }
}
