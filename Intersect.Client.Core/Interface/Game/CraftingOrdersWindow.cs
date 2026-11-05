using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets;
using Intersect.Network.Packets.Server;
using Intersect.Utilities;

namespace Intersect.Client.Interface.Game;

/// <summary>
/// Event-opened commissions board. Selecting a recipe never creates an order:
/// posting requires a separate, explicit confirmation button.
/// </summary>
internal sealed class CraftingOrdersWindow : Window
{
    private static readonly Color Gold = new(a: 255, r: 232, g: 192, b: 112);
    private static readonly Color Cream = new(a: 255, r: 250, g: 237, b: 213);
    private static readonly Color Muted = new(a: 255, r: 208, g: 191, b: 170);
    private static readonly Color Good = new(a: 255, r: 152, g: 220, b: 160);
    private static readonly Color Bad = new(a: 255, r: 241, g: 145, b: 129);

    private readonly CraftingOrdersAudio _audio = new();
    private readonly ScrollControl _list;
    private readonly ScrollControl _recipeList;
    private readonly Label _status;
    private readonly Label _balance;
    private readonly Label _recipeCount;
    private readonly Label _selectedName;
    private readonly Label _orderSummary;
    private readonly ImagePanel _selectedIcon;
    private readonly TextBox _search;
    private readonly TextBox _quantity;
    private readonly TextBox _reward;
    private readonly TextBox _hours;
    private readonly Base _postPanel;
    private readonly Button _publish;
    private readonly Button _browseTab;
    private readonly Button _mineTab;
    private readonly Button _postTab;
    private readonly List<MarketplaceWindow.MarketChrome> _tabStyles = [];
    private readonly Dictionary<Guid, MarketplaceWindow.MarketChrome> _recipeStyles = [];
    private CraftingOrdersStatePacket _state = new();
    private Guid _selectedItemId;
    private long _lastPublishAt;
    private int _tab;

    private static string T(string key) =>
        Intersect.Framework.Core.CraftingOrders.CraftingOrdersText.Get(key, ClientLanguages.Current);

    private static string ItemName(Guid itemId) =>
        ClientLanguages.ContentText(ItemDescriptor.GetName(itemId));

    public CraftingOrdersWindow(Canvas parent) : base(parent, T("Crafting Orders"), false, nameof(CraftingOrdersWindow))
    {
        SetSize(940, 670);
        Alignment = [Alignments.Center];
        DeleteOnClose = false;
        DisableResizing();

        Chrome(this, "OuterFrame", MarketplaceWindow.FrameStyle.Window, 11, 27, 917, 627);
        IconAt(this, "CraftingTools", "icon-tools.png", 35, 42, 43, 43);
        LabelAt(this, "Title", T("Crafting Orders"), 88, 45, 515, 38, 21, Gold, bold: true);
        IconAt(this, "AureonsIcon", "icon-aureons.png", 668, 50, 28, 28);
        _balance = LabelAt(this, "Balance", "", 701, 47, 204, 33, 14, Cream, bold: true);

        _browseTab = AddTab("AvailableTab", T("Available Orders"), 36, 100, 230, 41, 0);
        _mineTab = AddTab("MyOrdersTab", T("My Orders"), 274, 100, 220, 41, 1);
        _postTab = AddTab("PublishTab", T("Post an Order"), 502, 100, 253, 41, 2);
        Action(this, "Refresh", T("Refresh"), 765, 100, 141, 41,
            () => { _audio.Play("ui_select"); Networking.PacketSender.RequestCraftingOrders(); },
            MarketplaceWindow.FrameStyle.SecondaryAction, 13);

        Chrome(this, "NoticeRibbon", MarketplaceWindow.FrameStyle.Ribbon, 30, 150, 880, 42);
        _status = LabelAt(this, "Status", T("Commissions are backed by secure Aureons escrow."),
            48, 154, 845, 33, 13, Cream);

        _list = new ScrollControl(this, "ActiveOrders")
        {
            OverflowX = OverflowBehavior.Hidden, OverflowY = OverflowBehavior.Scroll, AutoHideBars = true,
        };
        _list.SetBounds(32, 204, 878, 429);

        _postPanel = new Base(this, "PublishPanel");
        _postPanel.SetBounds(32, 204, 878, 429);
        Chrome(_postPanel, "RecipeSection", MarketplaceWindow.FrameStyle.Section, 0, 0, 409, 424);
        Chrome(_postPanel, "DetailsSection", MarketplaceWindow.FrameStyle.Section, 422, 0, 451, 424);

        IconAt(_postPanel, "RecipeIcon", "icon-scroll.png", 19, 12, 32, 32);
        LabelAt(_postPanel, "RecipeHeading", T("CraftingOrders.SelectRecipe"), 57, 12, 245, 33, 15, Gold, bold: true);
        _recipeCount = LabelAt(_postPanel, "RecipeCount", "", 305, 12, 86, 31, 11, Muted, center: true);
        _search = Input(_postPanel, "RecipeSearch", "", 18, 57, 373, 39);
        _search.TextChanged += (_, _) => { RenderRecipes(); _recipeList.ScrollToTop(); };

        _recipeList = new ScrollControl(_postPanel, "RecipeChoices")
        {
            OverflowX = OverflowBehavior.Hidden, OverflowY = OverflowBehavior.Scroll, AutoHideBars = true,
        };
        _recipeList.SetBounds(17, 109, 379, 297);

        LabelAt(_postPanel, "TermsHeading", T("CraftingOrders.Terms"), 441, 16, 405, 33, 16, Gold, bold: true);
        _selectedIcon = new ImagePanel(_postPanel, "SelectedItemIcon")
        {
            MaintainAspectRatio = true, MouseInputEnabled = false, ShouldDrawBackground = false,
        };
        _selectedIcon.SetBounds(444, 61, 43, 43);
        _selectedName = LabelAt(_postPanel, "SelectedName", T("CraftingOrders.SelectItem"),
            495, 58, 344, 52, 14, Cream, bold: true);

        LabelAt(_postPanel, "QuantityLabel", T("Quantity"), 443, 119, 175, 27, 13, Gold, bold: true);
        _quantity = Input(_postPanel, "Quantity", "1", 444, 148, 165, 37);
        LabelAt(_postPanel, "RewardLabel", T("Reward (Aureons)"), 631, 119, 219, 27, 13, Gold, bold: true);
        _reward = Input(_postPanel, "Reward", "500", 631, 148, 215, 37);

        LabelAt(_postPanel, "HoursLabel", T("CraftingOrders.DeadlineHours"), 443, 204, 179, 27, 13, Gold, bold: true);
        _hours = Input(_postPanel, "Hours", "72", 444, 234, 165, 37);
        LabelAt(_postPanel, "DepositHelp", T("CraftingOrders.EscrowExplanation"),
            630, 204, 214, 81, 12, Muted);

        _orderSummary = LabelAt(_postPanel, "Preview", "", 443, 290, 400, 45, 12, Cream);
        _publish = Action(_postPanel, "Publish", T("CraftingOrders.PublishOrder"), 442, 354, 403, 53,
            Publish, MarketplaceWindow.FrameStyle.PrimaryAction, 16);

        _quantity.TextChanged += (_, _) => UpdateSelection();
        _reward.TextChanged += (_, _) => UpdateSelection();
        _hours.TextChanged += (_, _) => UpdateSelection();

        SelectTab(0);
        Hide();
    }

    protected override void EnsureInitialized() { }

    private static MarketplaceWindow.MarketChrome Chrome(
        Base parent, string id, MarketplaceWindow.FrameStyle style, int x, int y, int width, int height)
    {
        var chrome = new MarketplaceWindow.MarketChrome(parent, id, style);
        chrome.SetBounds(x, y, width, height);
        return chrome;
    }

    private static Label LabelAt(
        Base parent, string id, string text, int x, int y, int width, int height,
        int fontSize, Color color, bool bold = false, bool center = false)
    {
        var label = new Label(parent, id)
        {
            Text = text,
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont(bold ? "sourcesansproblack" : "sourcesanspro")
                   ?? parent.Skin.DefaultFont,
            FontSize = fontSize,
            TextColorOverride = color,
            WrappingBehavior = WrappingBehavior.Wrapped,
            TextAlign = center ? Pos.Center : Pos.Left | Pos.CenterV,
            MouseInputEnabled = false,
        };
        label.SetBounds(x, y, width, height);
        return label;
    }

    private static void IconAt(Base parent, string id, string filename, int x, int y, int width, int height)
    {
        var icon = new ImagePanel(parent, id)
        {
            Texture = GameContentManager.Current.GetTexture(TextureType.Gui, "crafting-orders/" + filename),
            MaintainAspectRatio = true,
            MouseInputEnabled = false,
            ShouldDrawBackground = false,
        };
        icon.SetBounds(x, y, width, height);
    }

    private static TextBox Input(Base parent, string id, string text, int x, int y, int width, int height)
    {
        var box = new TextBox(parent, id)
        {
            Text = text,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? parent.Skin.DefaultFont,
            FontSize = 14,
            TextColorOverride = Cream,
            TextAlign = Pos.Left | Pos.CenterV,
        };
        box.SetBounds(x, y, width, height);
        return box;
    }

    private static Button Action(Base parent, string id, string caption,
        int x, int y, int width, int height, Action callback,
        MarketplaceWindow.FrameStyle style, int fontSize = 14)
    {
        var button = new Button(parent, id) { Text = string.Empty };
        button.SetBounds(x, y, width, height);
        Chrome(button, id + "Chrome", style, 0, 0, width, height);
        LabelAt(button, id + "Caption", caption, 7, 3, width - 14, height - 6,
            fontSize, Cream, bold: true, center: true);
        button.Clicked += (_, _) => callback();
        return button;
    }

    private Button AddTab(string id, string caption, int x, int y, int width, int height, int index)
    {
        var button = new Button(this, id) { Text = string.Empty };
        button.SetBounds(x, y, width, height);
        var frame = Chrome(button, id + "Chrome", MarketplaceWindow.FrameStyle.Tab, 0, 0, width, height);
        _tabStyles.Add(frame);
        LabelAt(button, id + "Caption", caption, 6, 3, width - 12, height - 6,
            14, Cream, bold: true, center: true);
        button.Clicked += (_, _) =>
        {
            _audio.Play("ui_select");
            SelectTab(index);
        };
        return button;
    }

    public void Apply(CraftingOrdersStatePacket packet)
    {
        if (packet.OpenWindow && IsHidden) _audio.Play("ui_open");
        if (!string.IsNullOrWhiteSpace(packet.MessageKey))
        {
            var cue = packet.MessageKey switch
            {
                "Posted" => "order_posted",
                "Accepted" => "order_accepted",
                "Delivered" => "order_delivered",
                "Cancelled" => "order_cancelled",
                "MissingItems" or "InsufficientFunds" or "InvalidTerms" or "InvalidRecipe"
                    or "Unavailable" or "MissingCurrency" => "ui_error",
                _ => "",
            };
            if (cue.Length > 0) _audio.Play(cue);
            if (packet.MessageKey == "Delivered") _audio.Play("reward_aureons");
        }

        _state = packet;
        _balance.Text = $"{packet.Aureons:N0} {T("Aureons")}";
        _status.Text = packet.MessageKey.Length == 0
            ? T("Commissions are backed by secure Aureons escrow.")
            : T("CraftingOrders." + packet.MessageKey);
        _status.TextColorOverride = packet.MessageKey switch
        {
            "Posted" or "Accepted" or "Delivered" or "Cancelled" => Good,
            "MissingItems" or "InsufficientFunds" or "InvalidTerms" or "InvalidRecipe"
                or "Unavailable" or "MissingCurrency" => Bad,
            _ => Cream,
        };

        if (!_state.CraftableItemIds.Contains(_selectedItemId))
            _selectedItemId = _state.CraftableItemIds.FirstOrDefault();
        if (packet.MessageKey is "Posted" or "Accepted") _tab = 1;

        Rebuild();
    }

    private void SelectTab(int index)
    {
        _tab = index;
        _browseTab.IsDisabled = index == 0;
        _mineTab.IsDisabled = index == 1;
        _postTab.IsDisabled = index == 2;
        for (var i = 0; i < _tabStyles.Count; ++i)
            _tabStyles[i].Style = i == index
                ? MarketplaceWindow.FrameStyle.SelectedTab
                : MarketplaceWindow.FrameStyle.Tab;
        _list.SetHidden(index == 2);
        _postPanel.SetHidden(index != 2);
        Rebuild();
    }

    private void Rebuild()
    {
        _browseTab.IsDisabled = _tab == 0;
        _mineTab.IsDisabled = _tab == 1;
        _postTab.IsDisabled = _tab == 2;
        for (var i = 0; i < _tabStyles.Count; ++i)
            _tabStyles[i].Style = i == _tab
                ? MarketplaceWindow.FrameStyle.SelectedTab
                : MarketplaceWindow.FrameStyle.Tab;

        if (_tab == 2)
        {
            _list.Hide();
            _postPanel.Show();
            RenderRecipes();
        }
        else
        {
            _postPanel.Hide();
            _list.Show();
            RenderOrders();
        }
    }

    private void RenderOrders()
    {
        _list.DeleteAll();
        var rows = _state.Orders.Where(order => _tab == 0
            ? order.Status == 0
            : order.IsCustomer || order.IsCrafter).ToArray();

        var y = 4;
        foreach (var order in rows)
        {
            var card = new Base(_list, "Commission_" + order.Id.ToString("N"));
            card.SetBounds(4, y, 830, 92);
            Chrome(card, "OrderFrame", MarketplaceWindow.FrameStyle.Card, 0, 0, 830, 92);
            IconAt(card, "OrderScroll", "icon-scroll.png", 14, 20, 45, 48);
            LabelAt(card, "Item", $"{order.Quantity:N0} x {ItemName(order.ItemId)}",
                69, 11, 300, 30, 14, Cream, bold: true);
            LabelAt(card, "Players", order.Customer + (order.Crafter.Length == 0 ? "" : " / " + order.Crafter),
                69, 49, 295, 27, 12, Muted);
            LabelAt(card, "Gold", $"{order.Reward:N0} {T("Aureons")}",
                381, 10, 207, 30, 14, Gold, bold: true);
            LabelAt(card, "Due", DateTimeOffset.FromUnixTimeMilliseconds(order.DeadlineUtcMs)
                .ToLocalTime().ToString("g"), 381, 49, 207, 27, 12, Muted);

            if (order.Status == 0 && !order.IsCustomer)
                Action(card, "Accept", T("Accept Order"), 610, 25, 200, 44,
                    () => { _audio.Play("ui_select"); Networking.PacketSender.CraftingOrderAction(order.Id, 0); },
                    MarketplaceWindow.FrameStyle.PrimaryAction, 13);
            else if (order.Status == 1 && order.IsCrafter)
                Action(card, "Deliver", T("Deliver Crafted Items"), 610, 25, 200, 44,
                    () => { _audio.Play("craft_hit"); Networking.PacketSender.CraftingOrderAction(order.Id, 1); },
                    MarketplaceWindow.FrameStyle.PrimaryAction, 12);
            else if (order.Status == 0 && order.IsCustomer)
                Action(card, "Cancel", T("Cancel Order"), 610, 25, 200, 44,
                    () => { _audio.Play("ui_select"); Networking.PacketSender.CraftingOrderAction(order.Id, 2); },
                    MarketplaceWindow.FrameStyle.SecondaryAction, 13);
            else
                LabelAt(card, "OrderStatus", T("CraftingOrders.Status." + order.Status),
                    610, 25, 200, 44, 13, Gold, bold: true, center: true);
            y += 98;
        }

        if (rows.Length == 0)
            LabelAt(_list, "EmptyOrders", T("No crafting orders to show."),
                32, 42, 790, 55, 16, Muted, center: true);

        _list.SetInnerSize(845, Math.Max(426, y + 6));
        _list.UpdateScrollBars();
    }

    private void RenderRecipes()
    {
        _recipeStyles.Clear();
        _recipeList.DeleteAll();

        var eligible = _state.CraftableItemIds
            .Where(id => ItemDescriptor.Get(id) != null)
            .Distinct()
            .OrderBy(ItemName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        var query = (_search.Text ?? string.Empty).Trim();
        var filtered = eligible.Where(id =>
            query.Length == 0 || ItemName(id).Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        _recipeCount.Text = string.Format(T("CraftingOrders.RecipesCount"), filtered.Length, eligible.Length);

        var y = 4;
        foreach (var itemId in filtered)
        {
            var id = itemId;
            var row = new Button(_recipeList, "Recipe_" + id.ToString("N")) { Text = string.Empty };
            row.SetBounds(3, y, 349, 58);
            var frame = Chrome(row, "RecipeFrame", MarketplaceWindow.FrameStyle.Card, 0, 0, 349, 58);
            _recipeStyles.Add(id, frame);

            if (ItemDescriptor.Get(id) is { } descriptor)
            {
                var icon = new ImagePanel(row, "ItemIcon")
                {
                    MaintainAspectRatio = true, ShouldDrawBackground = false, MouseInputEnabled = false,
                    Texture = ItemIconTexture.Get(descriptor),
                    RenderColor = descriptor.Color,
                };
                icon.SetBounds(10, 7, 43, 43);
            }
            LabelAt(row, "RecipeName", ItemName(id), 66, 8, 270, 44,
                13, Cream, bold: id == _selectedItemId);
            row.Clicked += (_, _) =>
            {
                _selectedItemId = id;
                _audio.Play("ui_select");
                UpdateSelection();
            };
            y += 64;
        }

        if (filtered.Length == 0)
        {
            LabelAt(_recipeList, "NoRecipes", T(eligible.Length == 0
                    ? "CraftingOrders.NoEligibleRecipes" : "CraftingOrders.NoSearchMatches"),
                16, 26, 321, 150, 14, Muted, center: true);
            y = 216;
        }
        _recipeList.SetInnerSize(358, Math.Max(295, y + 8));
        _recipeList.UpdateScrollBars();
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        foreach (var (id, frame) in _recipeStyles)
            frame.Style = id == _selectedItemId
                ? MarketplaceWindow.FrameStyle.SelectedTab
                : MarketplaceWindow.FrameStyle.Card;

        var selected = ItemDescriptor.Get(_selectedItemId);
        var validSelection = selected != null && _state.CraftableItemIds.Contains(_selectedItemId);
        _selectedName.Text = validSelection ? ItemName(_selectedItemId) : T("CraftingOrders.SelectItem");
        _selectedIcon.Texture = validSelection ? ItemIconTexture.Get(selected!) : null;
        if (validSelection) _selectedIcon.RenderColor = selected!.Color;
        _publish.IsDisabled = !validSelection;

        _orderSummary.Text = validSelection
            ? string.Format(T("CraftingOrders.Summary"), _quantity.Text,
                ItemName(_selectedItemId), _reward.Text, _hours.Text)
            : T("CraftingOrders.SelectBeforePublishing");
    }

    private void Publish()
    {
        if (!_state.CraftableItemIds.Contains(_selectedItemId) || ItemDescriptor.Get(_selectedItemId) == null)
        {
            SetStatus("CraftingOrders.SelectBeforePublishing");
            return;
        }

        if (!int.TryParse(_quantity.Text, out var quantity) || quantity is < 1 or > 100 ||
            !int.TryParse(_reward.Text, out var reward) || reward is < 1 or > 100000000 ||
            !int.TryParse(_hours.Text, out var hours) || hours is < 1 or > 720)
        {
            SetStatus("CraftingOrders.InvalidTerms");
            return;
        }

        if (reward > _state.Aureons)
        {
            SetStatus("CraftingOrders.InsufficientFunds");
            return;
        }

        if (Environment.TickCount64 - _lastPublishAt < 1200)
            return;

        _lastPublishAt = Environment.TickCount64;
        _audio.Play("ui_select");
        _status.Text = T("CraftingOrders.Sending");
        _status.TextColorOverride = Cream;
        Networking.PacketSender.PostCraftingOrder(_selectedItemId, quantity, reward, hours);
    }

    private void SetStatus(string key)
    {
        _status.Text = T(key);
        _status.TextColorOverride = Bad;
        _audio.Play("ui_error");
    }
}
