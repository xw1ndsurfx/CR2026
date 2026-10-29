using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Framework.Core.RoyalContracts;
using Intersect.Network.Packets.Client;
using UiRectangle = Intersect.Client.Framework.GenericClasses.Rectangle;

namespace Intersect.Client.Interface.Game;

internal sealed class RoyalContractsWindow : Window
{
    private static readonly Color Ink = new(a: 255, r: 58, g: 34, b: 25);
    private static readonly Color Gold = new(a: 255, r: 236, g: 203, b: 129);
    private readonly Base _cards;
    private readonly Label _seals, _message, _pageLabel, _detailTitle, _heading;
    private readonly ScrollControl _details;
    private readonly RichLabel _detailText;
    private readonly Button _action, _daily, _weekly, _previous, _next, _refresh, _close;
    private RoyalContractBoardState _state = new();
    private RoyalContractPeriod _period;
    private Guid _selected;
    private int _page;
    private long _receivedAt, _refreshAt, _redrawAt, _pendingAt;
    private bool _pending;
    private string _language = "";
    private string _lastDetail = "";
    private readonly List<(Label Label, RoyalContract Contract)> _timers = [];
    private static string T(string value) => RoyalContractsText.Get(value, ClientLanguages.Current);
    private static string F(string value, params object[] args) => string.Format(T(value), args);
    private static string Content(string value)
    {
        var translated = ClientLanguages.ContentText(value);
        return translated != value ? translated : T(value);
    }

    public RoyalContractsWindow(Canvas parent) : base(parent, T("Royal Contracts"), false, nameof(RoyalContractsWindow))
    {
        SetSize(980, 596); MinimumSize = new Point(980, 596); Alignment = [Alignments.Center];
        InnerPanelPadding = new Padding(0); DeleteOnClose = false; DisableResizing();
        Art(this, "Board", "board.png", 0, 0, 980, 552);
        _heading = TextLabel(this, "Heading", T("Royal Contracts"), 26, 18, 365, 36, Gold, 20);
        _daily = Action("DailyTab", T("Daily"), 26, 64, 140, 32, () => ChangePeriod(RoyalContractPeriod.Daily));
        _weekly = Action("WeeklyTab", T("Weekly"), 180, 64, 140, 32, () => ChangePeriod(RoyalContractPeriod.Weekly));
        Art(this, "DetailsPaper", "parchment.png", 24, 110, 302, 336, paper: true);
        _detailTitle = TextLabel(this, "DetailTitle", "", 44, 126, 264, 50, Ink, 14);
        _details = new ScrollControl(this, "ContractDetails") { ShouldDrawBackground = false };
        _details.SetBounds(40, 181, 270, 206);
        _detailText = EventPanelText.CreateBody(_details, "DetailText", 12);
        _action = Action("ContractAction", "", 43, 402, 264, 32, Execute);
        _cards = new Base(this, "ContractCards") { ShouldDrawBackground = false };
        _cards.SetBounds(454, 156, 440, 286);
        _seals = TextLabel(this, "Seals", "", 26, 457, 365, 25, Gold, 12);
        _message = TextLabel(this, "Message", "", 26, 486, 390, 56, Gold, 10);
        _previous = Action("PreviousPage", T("Previous"), 457, 463, 110, 30, () => { _page--; Rebuild(); });
        _pageLabel = TextLabel(this, "PageNumber", "", 572, 465, 190, 26, Gold, 11);
        _pageLabel.TextAlign = Pos.Center;
        _next = Action("NextPage", T("Next"), 772, 463, 110, 30, () => { _page++; Rebuild(); });
        _refresh = Action("RefreshBoard", T("Refresh"), 458, 504, 160, 30, () => Request(RoyalContractAction.Refresh));
        _close = Action("CloseBoard", T("Close"), 723, 504, 160, 30, Hide);
        Hide();
    }

    protected override void EnsureInitialized() { }

    private static ImagePanel Art(Base parent, string name, string file, int x, int y, int width, int height, bool paper = false)
    {
        var image = new ImagePanel(parent, name)
        {
            Texture = GameContentManager.Current.GetTexture(TextureType.Gui, "royal-contracts/" + file),
            MouseInputEnabled = false, ShouldDrawBackground = false, MaintainAspectRatio = false,
        };
        // Sample inside the supplied parchment's white surround; no alteration of the original asset.
        if (paper && image.Texture is { } texture) image.SetTextureRect(100, 35, texture.Width - 200, texture.Height - 70);
        image.SetBounds(x, y, width, height); return image;
    }

    private static Label TextLabel(Base parent, string name, string text, int x, int y, int width, int height, Color color, int size)
    {
        var label = new Label(parent, name)
        {
            Text = text, AutoSizeToContents = false, TextColorOverride = color,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? parent.Skin.DefaultFont,
            FontSize = size, WrappingBehavior = WrappingBehavior.Wrapped, MouseInputEnabled = false,
        };
        label.SetBounds(x, y, width, height); return label;
    }

    private Button Action(string name, string caption, int x, int y, int width, int height, System.Action action)
    {
        var button = new RoyalButton(this, name) { Text = caption, Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont, FontSize = 11, TextColorOverride = Gold };
        button.SetBounds(x, y, width, height); button.Clicked += (_, _) => action(); return button;
    }

    private sealed class RoyalButton(Base parent, string name) : Button(parent, name)
    {
        protected override void Render(Intersect.Client.Framework.Gwen.Skin.Base skin)
        {
            ShouldDrawBackground = false;
            var r = RenderBounds;
            skin.Renderer.DrawColor = IsDisabled ? new Color(90, 80, 68) : Gold;
            skin.Renderer.DrawFilledRect(r);
            skin.Renderer.DrawColor = new Color(43, 25, 48);
            skin.Renderer.DrawFilledRect(new UiRectangle(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2));
            base.Render(skin);
        }
    }

    private sealed class PaperCard(Base parent, string name, bool selected) : Button(parent, name)
    {
        protected override void Render(Intersect.Client.Framework.Gwen.Skin.Base skin)
        {
            ShouldDrawBackground = false;
            skin.Renderer.DrawColor = selected ? Gold : new Color(94, 56, 32);
            skin.Renderer.DrawFilledRect(RenderBounds);
            base.Render(skin);
        }
    }

    public void Apply(RoyalContractBoardState state)
    {
        _state = state; _receivedAt = Environment.TickCount64; _pending = false;
        _refreshAt = _receivedAt + 6000; Rebuild();
    }

    private void ChangePeriod(RoyalContractPeriod period) { _period = period; _page = 0; _selected = Guid.Empty; Rebuild(); }
    private List<RoyalContractView> Filtered() => _state.Contracts.Where(v => v.Contract.Template.Period == _period).ToList();

    private void Rebuild()
    {
        _language = ClientLanguages.Current; Title = T("Royal Contracts");
        _heading.Text = T("Royal Contracts"); _refresh.Text = T("Refresh"); _close.Text = T("Close");
        _seals.Text = F("Royal seals: {0}", _state.Seals);
        _message.Text = T(_state.Message.Length > 0 ? _state.Message : !_state.Enabled ? "The board is paused." :
            _state.Contracts.Count == 0 ? "No contracts are posted. Ask the royal steward to configure the board." : "Choose a contract on the board.");
        _daily.Text = T("Daily"); _weekly.Text = T("Weekly");
        _daily.IsDisabled = _period == RoyalContractPeriod.Daily; _weekly.IsDisabled = _period == RoyalContractPeriod.Weekly;
        var entries = Filtered(); var pages = Math.Max(1, (entries.Count + 3) / 4);
        _page = Math.Clamp(_page, 0, pages - 1);
        var visible = entries.Skip(_page * 4).Take(4).ToArray();
        if (!visible.Any(v => v.Contract.Id == _selected)) _selected = visible.FirstOrDefault()?.Contract.Id ?? Guid.Empty;
        _cards.DeleteAllChildren(); _timers.Clear();
        for (var i = 0; i < visible.Length; i++)
        {
            var entry = visible[i]; var contract = entry.Contract;
            var card = new PaperCard(_cards, "Contract" + i, contract.Id == _selected);
            card.SetBounds(i % 2 * 220, i / 2 * 146, 210, 138);
            Art(card, "Paper", "parchment.png", 3, 3, 204, 132, paper: true);
            card.Clicked += (_, _) => { _selected = contract.Id; Rebuild(); };
            TextLabel(card, "Kind", T(contract.Template.Collective ? "Collective" : "Personal"), 12, 9, 186, 18, Ink, 10);
            var title = TextLabel(card, "Title", "", 12, 29, 186, 44, Ink, 12);
            title.WrappingBehavior = WrappingBehavior.NoWrap;
            EventPanelText.Fit(title, Content(contract.Template.Name), 14, 10);
            card.TooltipText = Content(contract.Template.Name);
            TextLabel(card, "Progress", entry.Claimed ? T("Claimed") : !entry.Accepted ? T("Accept contract") : F("Progress: {0} / {1}", entry.Progress, contract.Quantity), 12, 78, 186, 22, Ink, 11);
            var timer = TextLabel(card, "Expiry", "", 12, 106, 186, 20, Ink, 10); _timers.Add((timer, contract));
        }
        _previous.IsDisabled = _page == 0; _next.IsDisabled = _page == pages - 1;
        _previous.Text = T("Previous"); _next.Text = T("Next"); _pageLabel.Text = F("Page {0} / {1}", _page + 1, pages);
        ShowDetails(); UpdateTimers();
    }

    private void ShowDetails()
    {
        var entry = _state.Contracts.FirstOrDefault(v => v.Contract.Id == _selected);
        _action.IsDisabled = true; _action.Text = "";
        _detailTitle.WrappingBehavior = WrappingBehavior.NoWrap;
        EventPanelText.Fit(_detailTitle, entry == null ? T("Royal Contracts") : Content(entry.Contract.Template.Name), 14, 10);
        if (entry == null) { _detailText.ClearText(); _lastDetail = ""; return; }
        var t = entry.Contract.Template;
        var objective = t.Objective switch { RoyalContractObjective.NpcKills => "Eliminate enemies", RoyalContractObjective.DeliverItems => "Deliver resources", _ => "Explore distinct maps" };
        var body = Content(t.Name) + "\n" + Content(t.Description) + "\n\n" + T(objective) + "\n" + Content(entry.TargetName) + "\n" + F("Progress: {0} / {1}", entry.Progress, entry.Contract.Quantity);
        if (t.Collective) body += "\n" + F("Your contribution: {0}", entry.Contribution);
        body += "\n\n" + F("Reward: {0} seals", t.Seals);
        if (t.RewardQuantity > 0) body += "\n" + t.RewardQuantity + " × " + Content(entry.RewardName);
        if (!entry.Eligible) body += "\n\n" + F("Level {0} required", t.MinimumLevel);
        if (t.Collective && entry.Contribution == 0) body += "\n\n" + T("Contribute to this collective contract to earn its reward.");
        if (_lastDetail != body)
        {
            _lastDetail = body; _detailText.ClearText();
            _detailText.SetSize(Math.Max(1, _details.Width - 22), 20); _detailText.AddText(body, Ink); _detailText.ForceImmediateRebuild(); _details.UpdateScrollBars();
        }
        var ready = entry.Accepted && entry.Progress >= entry.Contract.Quantity && entry.Contribution > 0;
        _action.Text = T(entry.Claimed ? "Claimed" : !entry.Accepted ? "Accept contract" : ready ? "Claim reward" : t.Objective == RoyalContractObjective.DeliverItems ? "Deliver resources" : "In progress");
        _action.IsDisabled = _pending || !_state.Enabled || !entry.Eligible || entry.Claimed ||
            (entry.Accepted && !ready && (t.Objective != RoyalContractObjective.DeliverItems || entry.Contribution >= entry.Contract.Quantity)) || ServerNow() >= entry.Contract.ExpiresAt;
    }

    private DateTimeOffset ServerNow() => _state.ServerTime.AddMilliseconds(Math.Max(0, Environment.TickCount64 - _receivedAt));
    private void UpdateTimers()
    {
        foreach (var (label, contract) in _timers)
        {
            var remaining = contract.ExpiresAt - ServerNow();
            label.Text = F("Expires in {0}h {1}m", Math.Max(0, (int)remaining.TotalHours), Math.Max(0, remaining.Minutes));
        }
    }
    private void Execute()
    {
        var entry = _state.Contracts.FirstOrDefault(v => v.Contract.Id == _selected); if (entry == null || _action.IsDisabled) return;
        Request(!entry.Accepted ? RoyalContractAction.Accept : entry.Progress >= entry.Contract.Quantity && entry.Contribution > 0 ? RoyalContractAction.Claim : RoyalContractAction.Deliver);
    }
    private void Request(RoyalContractAction action)
    {
        if (_pending) return; _pending = true; _pendingAt = Environment.TickCount64; _action.IsDisabled = true;
        Intersect.Client.Networking.Network.SendPacket(new RoyalContractRequestPacket { Action = action, ContractId = _selected });
    }
    public void Update()
    {
        if (IsHidden) return;
        var now = Environment.TickCount64;
        if (_pending && now - _pendingAt > 4000) { _pending = false; ShowDetails(); }
        if (_language != ClientLanguages.Current) Rebuild();
        if (now >= _redrawAt)
        {
            UpdateTimers();
            if (_state.Contracts.FirstOrDefault(v => v.Contract.Id == _selected) is { } selected && ServerNow() >= selected.Contract.ExpiresAt) _action.IsDisabled = true;
            _redrawAt = now + 1000;
        }
        if (now >= _refreshAt) { _refreshAt = now + 6000; Request(RoyalContractAction.Refresh); }
    }
}
