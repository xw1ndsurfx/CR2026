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
/// Royal crafting commissions. The supplied 1195/1196 artwork decorates the
/// live Gwen controls; item icons, translations and order data stay dynamic.
/// The event command is still the only entry point.
/// </summary>
internal sealed class CraftingOrdersWindow : Window
{
    private static readonly Color Gold = new(a: 255, r: 238, g: 200, b: 113);
    private static readonly Color Cream = new(a: 255, r: 249, g: 236, b: 205);
    private static readonly Color Muted = new(a: 255, r: 202, g: 178, b: 151);
    private static readonly Color Ink = new(a: 255, r: 67, g: 39, b: 24);
    private static readonly Color Good = new(a: 255, r: 153, g: 229, b: 154);
    private static readonly Color Bad = new(a: 255, r: 237, g: 143, b: 126);

    private readonly CraftingOrdersAudio _audio = new();
    private readonly Base _browsePanel;
    private readonly Base _postPanel;
    private readonly ScrollControl _orders;
    private readonly ScrollControl _recipes;
    private readonly TextBox _orderSearch;
    private readonly TextBox _recipeSearch;
    private readonly TextBox _quantity;
    private readonly TextBox _reward;
    private readonly TextBox _hours;
    private readonly Label _balance;
    private readonly Label _message;
    private readonly Label _count;
    private readonly Label _recipeCount;
    private readonly Label _urgentLabel;
    private readonly Label _detailHeading;
    private readonly Label _detailCustomer;
    private readonly Label _detailReward;
    private readonly Label _detailDeadline;
    private readonly Label _detailStatus;
    private readonly Label _detailActionLabel;
    private readonly Label _chosenName;
    private readonly Label _postSummary;
    private readonly ImagePanel _detailIcon;
    private readonly ImagePanel _chosenIcon;
    private readonly Button _detailAction;
    private readonly Button _postAction;
    private readonly Button[] _tabs = new Button[3];
    private readonly MarketplaceWindow.MarketChrome[] _tabFrames = new MarketplaceWindow.MarketChrome[3];
    private readonly Dictionary<Guid, MarketplaceWindow.MarketChrome> _recipeFrames = [];
    private CraftingOrdersStatePacket _state = new();
    private Guid _selectedOrderId;
    private Guid _chosenItemId;
    private bool _urgent;
    private int _tab;
    private long _postedAt;

    private static string T(string text) =>
        Intersect.Framework.Core.CraftingOrders.CraftingOrdersText.Get(text, ClientLanguages.Current);
    private static string F(string text, params object[] values) => string.Format(T(text), values);
    private static string ItemName(Guid itemId) => ClientLanguages.ContentText(ItemDescriptor.GetName(itemId));

    public CraftingOrdersWindow(Canvas parent)
        : base(parent, T("Crafting Orders"), false, nameof(CraftingOrdersWindow))
    {
        SetSize(1120, 765);
        Alignment = [Alignments.Center];
        DeleteOnClose = false;
        DisableResizing();

        // Actual fragments from 1195.png: ornate window corners, crowned crest,
        // and parchment. The original baked-in French text is NOT used as UI.
        Chrome(this, "RoyalOuterFrame", MarketplaceWindow.FrameStyle.Window, 11, 27, 1097, 727);
        Art(this, "TopLeftCorner", 171, 0, 48, 48, 12, 35, 65, 65);
        Art(this, "TopRightCorner", 222, 0, 48, 48, 1040, 35, 65, 65);
        Art(this, "BottomLeftCorner", 273, 0, 48, 48, 12, 689, 65, 65);
        Art(this, "BottomRightCorner", 324, 0, 48, 48, 1040, 689, 65, 65);
        Art(this, "RoyalCrest", 0, 0, 170, 70, 443, 29, 233, 83);
        Art(this, "CraftingTools", 224, 190, 46, 46, 333, 111, 41, 41);
        LabelAt(this, "BoardTitle", T("Crafting Orders"), 382, 110, 370, 43, 22, Gold, bold: true, center: true);
        LabelAt(this, "BoardSubtitle", T("CraftingOrders.Subtitle"),
            255, 149, 610, 27, 13, Cream, center: true);
        Art(this, "GoldCoins", 338, 132, 49, 48, 857, 120, 36, 33);
        _balance = LabelAt(this, "AureonBalance", "", 897, 123, 185, 30, 14, Gold, bold: true);

        var tabNames = new[] { "Available Orders", "My Orders", "CraftingOrders.MyCrafting" };
        var tabIcons = new[] { (192, 132, 43, 52), (179, 191, 40, 43), (286, 132, 48, 47) };
        for (var i = 0; i < _tabs.Length; ++i)
        {
            var index = i;
            var x = 39 + i * 252;
            _tabs[i] = ButtonAt(this, "Tab_" + i, T(tabNames[i]),
                x, 184, 241, 47, () => { _audio.Play("ui_select"); SetTab(index); },
                MarketplaceWindow.FrameStyle.Tab, 13, out var tabFrame);
            _tabFrames[i] = tabFrame;
            var icon = tabIcons[i];
            Art(_tabs[i], "TabIcon_" + i, icon.Item1, icon.Item2, icon.Item3, icon.Item4, 11, 7, 31, 31);
        }

        ButtonAt(this, "OpenComposer", T("Post an Order"), 820, 184, 264, 47,
            () => { _audio.Play("ui_select"); SetTab(3); },
            MarketplaceWindow.FrameStyle.PrimaryAction, 14, out _);

        _message = LabelAt(this, "BoardMessage", T("Commissions are backed by secure Aureons escrow."),
            47, 237, 1016, 30, 12, Cream);

        _browsePanel = new Base(this, "BrowsePanel");
        _browsePanel.SetBounds(35, 276, 1049, 445);
        Chrome(_browsePanel, "FiltersFrame", MarketplaceWindow.FrameStyle.Section, 0, 0, 229, 433);
        Chrome(_browsePanel, "ListFrame", MarketplaceWindow.FrameStyle.Section, 237, 0, 512, 433);
        Chrome(_browsePanel, "DetailsFrame", MarketplaceWindow.FrameStyle.Section, 758, 0, 290, 433);

        LabelAt(_browsePanel, "FiltersHeading", T("CraftingOrders.Filters"), 17, 13, 190, 28, 16, Gold, bold: true);
        LabelAt(_browsePanel, "BrowseSearchLabel", T("CraftingOrders.SearchOrders"), 17, 49, 191, 25, 12, Muted);
        _orderSearch = Input(_browsePanel, "BrowseSearch", "", 16, 77, 197, 37);
        _orderSearch.TextChanged += (_, _) => { RenderOrders(); _orders.ScrollToTop(); };
        ButtonAt(_browsePanel, "Urgent", T("CraftingOrders.UrgentOff"),
            17, 129, 194, 41, () =>
            {
                _urgent = !_urgent;
                _urgentLabel.Text = T(_urgent ? "CraftingOrders.UrgentOn" : "CraftingOrders.UrgentOff");
                _audio.Play("ui_select");
                RenderOrders();
            }, MarketplaceWindow.FrameStyle.SecondaryAction, 12, out _, out _urgentLabel);
        ButtonAt(_browsePanel, "RefreshOrders", T("Refresh"),
            17, 369, 194, 45, () => { _audio.Play("ui_select"); Networking.PacketSender.RequestCraftingOrders(); },
            MarketplaceWindow.FrameStyle.SecondaryAction, 14, out _);

        LabelAt(_browsePanel, "BrowseListTitle", T("CraftingOrders.Commissions"), 255, 13, 330, 30, 16, Gold, bold: true);
        _count = LabelAt(_browsePanel, "Count", "", 603, 14, 132, 28, 12, Muted, center: true);
        _orders = new ScrollControl(_browsePanel, "OrderCards")
        {
            OverflowX = OverflowBehavior.Hidden, OverflowY = OverflowBehavior.Scroll, AutoHideBars = true,
        };
        _orders.SetBounds(251, 49, 482, 368);

        // Parchment artwork from 1195.png, not a screenshot mockup.
        Art(_browsePanel, "Parchment", 0, 73, 135, 164, 765, 4, 278, 424);
        Art(_browsePanel, "DetailBanner", 138, 73, 190, 52, 765, 6, 278, 62);
        _detailHeading = LabelAt(_browsePanel, "DetailHeading", T("CraftingOrders.OrderDetails"),
            785, 16, 241, 39, 15, Cream, bold: true, center: true);
        _detailIcon = new ImagePanel(_browsePanel, "DetailIcon")
        {
            MaintainAspectRatio = true, MouseInputEnabled = false, ShouldDrawBackground = false,
        };
        _detailIcon.SetBounds(778, 84, 52, 52);
        _detailStatus = LabelAt(_browsePanel, "DetailStatus", "",
            837, 78, 188, 56, 13, Ink, bold: true);
        _detailCustomer = LabelAt(_browsePanel, "DetailCustomer", "",
            781, 148, 240, 61, 13, Ink);
        _detailReward = LabelAt(_browsePanel, "DetailReward", "",
            779, 229, 241, 54, 14, Ink, bold: true);
        _detailDeadline = LabelAt(_browsePanel, "DetailDue", "",
            779, 285, 241, 45, 12, Ink);
        _detailAction = ButtonAt(_browsePanel, "OrderAction", "",
            776, 351, 255, 59, PerformOrderAction,
            MarketplaceWindow.FrameStyle.PrimaryAction, 13, out _, out _detailActionLabel);

        _postPanel = new Base(this, "PostPanel");
        _postPanel.SetBounds(35, 276, 1049, 445);
        Chrome(_postPanel, "PostRecipesFrame", MarketplaceWindow.FrameStyle.Section, 0, 0, 519, 433);
        Chrome(_postPanel, "PostTermsFrame", MarketplaceWindow.FrameStyle.Section, 528, 0, 519, 433);
        Art(_postPanel, "PostScroll", 192, 132, 43, 52, 16, 7, 34, 41);
        LabelAt(_postPanel, "RecipeHeading", T("CraftingOrders.SelectRecipe"),
            57, 9, 349, 35, 16, Gold, bold: true);
        _recipeCount = LabelAt(_postPanel, "RecipeCount", "", 408, 13, 92, 30, 12, Muted, center: true);
        LabelAt(_postPanel, "RecipeSearchTitle", T("CraftingOrders.Search"),
            16, 47, 468, 24, 12, Muted);
        _recipeSearch = Input(_postPanel, "RecipeSearch", "", 17, 76, 484, 37);
        _recipeSearch.TextChanged += (_, _) => { RenderRecipes(); _recipes.ScrollToTop(); };
        _recipes = new ScrollControl(_postPanel, "RecipeCards")
        {
            OverflowX = OverflowBehavior.Hidden, OverflowY = OverflowBehavior.Scroll, AutoHideBars = true,
        };
        _recipes.SetBounds(16, 120, 486, 294);

        Art(_postPanel, "TermsParchment", 0, 73, 135, 164, 534, 5, 507, 422);
        Art(_postPanel, "TermsBanner", 138, 73, 190, 52, 544, 7, 487, 56);
        LabelAt(_postPanel, "TermsTitle", T("CraftingOrders.Terms"),
            570, 16, 438, 34, 15, Cream, bold: true, center: true);
        _chosenIcon = new ImagePanel(_postPanel, "ChosenIcon")
        {
            MaintainAspectRatio = true, MouseInputEnabled = false, ShouldDrawBackground = false,
        };
        _chosenIcon.SetBounds(558, 73, 55, 55);
        _chosenName = LabelAt(_postPanel, "ChosenName", T("CraftingOrders.SelectItem"),
            623, 76, 393, 52, 14, Ink, bold: true);
        LabelAt(_postPanel, "QuantityLabel", T("Quantity"), 555, 142, 189, 25, 12, Ink, bold: true);
        _quantity = Input(_postPanel, "Quantity", "1", 555, 170, 167, 36);
        LabelAt(_postPanel, "RewardLabel", T("Reward (Aureons)"), 750, 142, 255, 25, 12, Ink, bold: true);
        _reward = Input(_postPanel, "Reward", "500", 749, 170, 253, 36);
        LabelAt(_postPanel, "DeadlineLabel", T("CraftingOrders.DeadlineHours"), 555, 218, 200, 24, 12, Ink, bold: true);
        _hours = Input(_postPanel, "Deadline", "72", 555, 245, 168, 36);
        LabelAt(_postPanel, "EscrowHint", T("CraftingOrders.EscrowExplanation"),
            745, 219, 261, 70, 11, Ink);
        _postSummary = LabelAt(_postPanel, "PostSummary", "",
            554, 296, 451, 43, 12, Ink);
        _postAction = ButtonAt(_postPanel, "PublishOrder", T("CraftingOrders.PublishOrder"),
            551, 350, 457, 59, PublishOrder,
            MarketplaceWindow.FrameStyle.PrimaryAction, 16, out _);
        _quantity.TextChanged += (_, _) => RefreshRecipeSelection();
        _reward.TextChanged += (_, _) => RefreshRecipeSelection();
        _hours.TextChanged += (_, _) => RefreshRecipeSelection();

        SetTab(0);
        Hide();
    }

    protected override void EnsureInitialized() { }

    private static MarketplaceWindow.MarketChrome Chrome(Base parent, string name,
        MarketplaceWindow.FrameStyle style, int x, int y, int width, int height)
    {
        var frame = new MarketplaceWindow.MarketChrome(parent, name, style);
        frame.SetBounds(x, y, width, height);
        return frame;
    }

    private static void Art(Base parent, string name, int sx, int sy, int sw, int sh,
        int x, int y, int width, int height)
    {
        var image = new ImagePanel(parent, name)
        {
            Texture = GameContentManager.Current.GetTexture(TextureType.Gui, "crafting-orders/royal-crafting-atlas.png"),
            MaintainAspectRatio = false,
            MouseInputEnabled = false,
            ShouldDrawBackground = false,
        };
        if (image.Texture != null) image.SetTextureRect(sx, sy, sw, sh);
        image.SetBounds(x, y, width, height);
    }

    private static Label LabelAt(Base parent, string name, string text,
        int x, int y, int width, int height, int fontSize, Color color,
        bool bold = false, bool center = false)
    {
        var label = new Label(parent, name)
        {
            Text = text, AutoSizeToContents = false, FontSize = fontSize,
            Font = GameContentManager.Current.GetFont(bold ? "sourcesansproblack" : "sourcesanspro")
                   ?? parent.Skin.DefaultFont,
            TextColorOverride = color,
            WrappingBehavior = WrappingBehavior.Wrapped,
            TextAlign = center ? Pos.Center : Pos.Left | Pos.CenterV,
            MouseInputEnabled = false,
        };
        label.SetBounds(x, y, width, height);
        return label;
    }

    private static TextBox Input(Base parent, string name, string value,
        int x, int y, int width, int height)
    {
        var box = new TextBox(parent, name)
        {
            Text = value, FontSize = 14,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? parent.Skin.DefaultFont,
            TextColorOverride = Cream,
            TextAlign = Pos.Left | Pos.CenterV,
        };
        box.SetBounds(x, y, width, height);
        return box;
    }

    private static Button ButtonAt(Base parent, string name, string caption,
        int x, int y, int width, int height, Action callback,
        MarketplaceWindow.FrameStyle style, int fontSize,
        out MarketplaceWindow.MarketChrome chrome)
    {
        return ButtonAt(parent, name, caption, x, y, width, height, callback,
            style, fontSize, out chrome, out _);
    }

    private static Button ButtonAt(Base parent, string name, string caption,
        int x, int y, int width, int height, Action callback,
        MarketplaceWindow.FrameStyle style, int fontSize,
        out MarketplaceWindow.MarketChrome chrome, out Label label)
    {
        var button = new Button(parent, name) { Text = string.Empty };
        button.SetBounds(x, y, width, height);
        chrome = Chrome(button, name + "Frame", style, 0, 0, width, height);
        label = LabelAt(button, name + "Caption", caption, 8, 3, width - 16, height - 6,
            fontSize, Cream, bold: true, center: true);
        button.Clicked += (_, _) => callback();
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
        _message.Text = packet.MessageKey.Length == 0
            ? T("Commissions are backed by secure Aureons escrow.")
            : T("CraftingOrders." + packet.MessageKey);
        _message.TextColorOverride = packet.MessageKey switch
        {
            "Posted" or "Accepted" or "Delivered" or "Cancelled" => Good,
            "MissingItems" or "InsufficientFunds" or "InvalidTerms" or "InvalidRecipe"
                or "Unavailable" or "MissingCurrency" => Bad,
            _ => Cream,
        };

        if (!packet.CraftableItemIds.Contains(_chosenItemId))
            _chosenItemId = packet.CraftableItemIds.FirstOrDefault();
        if (packet.MessageKey == "Posted") _tab = 1;
        if (packet.MessageKey == "Accepted") _tab = 2;
        RefreshTab();
    }

    private void SetTab(int index)
    {
        _tab = index;
        RefreshTab();
    }

    private void RefreshTab()
    {
        for (var i = 0; i < 3; ++i)
        {
            _tabs[i].IsDisabled = _tab == i;
            _tabFrames[i].Style = _tab == i
                ? MarketplaceWindow.FrameStyle.SelectedTab
                : MarketplaceWindow.FrameStyle.Tab;
        }

        if (_tab == 3)
        {
            _browsePanel.Hide();
            _postPanel.Show();
            RenderRecipes();
        }
        else
        {
            _postPanel.Hide();
            _browsePanel.Show();
            RenderOrders();
        }
    }

    private CraftingOrderView[] FilterOrders()
    {
        var query = (_orderSearch.Text ?? "").Trim();
        return _state.Orders
            .Where(order => _tab switch
            {
                0 => order.Status == 0,
                1 => order.IsCustomer,
                2 => order.IsCrafter,
                _ => false,
            })
            .Where(order => !_urgent || _tab != 0 ||
                            order.DeadlineUtcMs <= DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeMilliseconds())
            .Where(order => query.Length == 0 ||
                ItemName(order.ItemId).Contains(query, StringComparison.OrdinalIgnoreCase) ||
                order.Customer.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                order.Crafter.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(order => order.Status)
            .ThenBy(order => order.DeadlineUtcMs)
            .ToArray();
    }

    private void RenderOrders()
    {
        var rows = FilterOrders();
        _count.Text = F("CraftingOrders.Count", rows.Length);
        if (!rows.Any(order => order.Id == _selectedOrderId))
            _selectedOrderId = rows.FirstOrDefault()?.Id ?? Guid.Empty;

        _orders.DeleteAll();
        var y = 4;
        foreach (var order in rows)
        {
            var currentOrder = order;
            var row = new Button(_orders, "Order_" + order.Id.ToString("N")) { Text = string.Empty };
            row.SetBounds(4, y, 454, 86);
            Chrome(row, "RowFrame",
                order.Id == _selectedOrderId ? MarketplaceWindow.FrameStyle.SelectedTab
                                             : MarketplaceWindow.FrameStyle.Card, 0, 0, 454, 86);

            if (ItemDescriptor.Get(order.ItemId) is { } descriptor)
            {
                var icon = new ImagePanel(row, "OrderIcon")
                {
                    Texture = ItemIconTexture.Get(descriptor),
                    RenderColor = descriptor.Color,
                    MaintainAspectRatio = true,
                    MouseInputEnabled = false,
                    ShouldDrawBackground = false,
                };
                icon.SetBounds(12, 14, 58, 58);
            }
            LabelAt(row, "Name", $"{order.Quantity:N0} × {ItemName(order.ItemId)}",
                79, 9, 256, 28, 13, Cream, bold: true);
            LabelAt(row, "Customer", order.Customer,
                81, 39, 204, 23, 11, Muted);
            LabelAt(row, "Reward", F("CraftingOrders.CoinAmount", order.Reward),
                291, 37, 155, 24, 12, Gold, bold: true);
            LabelAt(row, "Deadline", DateTimeOffset.FromUnixTimeMilliseconds(order.DeadlineUtcMs)
                .ToLocalTime().ToString("g"), 281, 60, 163, 22, 10, Muted);
            row.Clicked += (_, _) =>
            {
                _selectedOrderId = currentOrder.Id;
                _audio.Play("ui_select");
                RenderOrders();
            };
            y += 91;
        }

        if (rows.Length == 0)
            LabelAt(_orders, "Empty", T("No crafting orders to show."),
                21, 60, 413, 75, 14, Muted, center: true);
        _orders.SetInnerSize(464, Math.Max(364, y + 9));
        _orders.UpdateScrollBars();
        RenderDetails(rows.FirstOrDefault(row => row.Id == _selectedOrderId));
    }

    private void RenderDetails(CraftingOrderView? order)
    {
        if (order == null)
        {
            _detailIcon.Texture = null;
            _detailStatus.Text = T("CraftingOrders.SelectOrder");
            _detailCustomer.Text = "";
            _detailReward.Text = "";
            _detailDeadline.Text = "";
            _detailAction.Hide();
            return;
        }

        if (ItemDescriptor.Get(order.ItemId) is { } item)
        {
            _detailIcon.Texture = ItemIconTexture.Get(item);
            _detailIcon.RenderColor = item.Color;
        }
        else _detailIcon.Texture = null;
        _detailStatus.Text = $"{order.Quantity:N0} × {ItemName(order.ItemId)}";
        _detailCustomer.Text = F("CraftingOrders.Customer", order.Customer)
            + "\n" + T("CraftingOrders.Status." + order.Status);
        _detailReward.Text = F("CraftingOrders.Reward", order.Reward);
        _detailDeadline.Text = F("CraftingOrders.Due",
            DateTimeOffset.FromUnixTimeMilliseconds(order.DeadlineUtcMs).ToLocalTime().ToString("g"));

        var action = order.Status switch
        {
            0 when !order.IsCustomer => "Accept Order",
            0 when order.IsCustomer => "Cancel Order",
            1 when order.IsCrafter => "Deliver Crafted Items",
            _ => "",
        };
        if (action.Length == 0) _detailAction.Hide();
        else
        {
            _detailAction.Show();
            _detailActionLabel.Text = T(action);
        }
    }

    private void PerformOrderAction()
    {
        var order = _state.Orders.FirstOrDefault(item => item.Id == _selectedOrderId);
        if (order == null) return;
        byte? action = order.Status switch
        {
            0 when !order.IsCustomer => 0,
            0 when order.IsCustomer => 2,
            1 when order.IsCrafter => 1,
            _ => null,
        };
        if (!action.HasValue) return;
        _audio.Play(action.Value == 1 ? "craft_hit" : "ui_select");
        Networking.PacketSender.CraftingOrderAction(order.Id, action.Value);
    }

    private void RenderRecipes()
    {
        _recipeFrames.Clear();
        _recipes.DeleteAll();
        var eligible = _state.CraftableItemIds.Where(id => ItemDescriptor.Get(id) != null)
            .Distinct().OrderBy(ItemName, StringComparer.CurrentCultureIgnoreCase).ToArray();
        var query = (_recipeSearch.Text ?? "").Trim();
        var filtered = eligible.Where(id => query.Length == 0 ||
            ItemName(id).Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        _recipeCount.Text = F("CraftingOrders.RecipesCount", filtered.Length, eligible.Length);

        var y = 4;
        foreach (var id in filtered)
        {
            var itemId = id;
            var row = new Button(_recipes, "Recipe_" + id.ToString("N")) { Text = string.Empty };
            row.SetBounds(4, y, 462, 62);
            var frame = Chrome(row, "RecipeFrame",
                id == _chosenItemId ? MarketplaceWindow.FrameStyle.SelectedTab
                                    : MarketplaceWindow.FrameStyle.Card, 0, 0, 462, 62);
            _recipeFrames[id] = frame;
            if (ItemDescriptor.Get(id) is { } item)
            {
                var icon = new ImagePanel(row, "ItemIcon")
                {
                    Texture = ItemIconTexture.Get(item),
                    RenderColor = item.Color,
                    MaintainAspectRatio = true,
                    ShouldDrawBackground = false,
                    MouseInputEnabled = false,
                };
                icon.SetBounds(11, 9, 45, 45);
            }
            LabelAt(row, "ItemName", ItemName(id), 68, 10, 376, 42, 14, Cream, bold: true);
            row.Clicked += (_, _) =>
            {
                _chosenItemId = itemId;
                _audio.Play("ui_select");
                RefreshRecipeSelection();
            };
            y += 68;
        }

        if (filtered.Length == 0)
        {
            LabelAt(_recipes, "EmptyRecipes", T(eligible.Length == 0
                ? "CraftingOrders.NoEligibleRecipes" : "CraftingOrders.NoSearchMatches"),
                21, 27, 415, 119, 13, Muted, center: true);
            y = 180;
        }
        _recipes.SetInnerSize(468, Math.Max(287, y + 6));
        _recipes.UpdateScrollBars();
        RefreshRecipeSelection();
    }

    private void RefreshRecipeSelection()
    {
        foreach (var (id, frame) in _recipeFrames)
            frame.Style = id == _chosenItemId
                ? MarketplaceWindow.FrameStyle.SelectedTab : MarketplaceWindow.FrameStyle.Card;

        var item = ItemDescriptor.Get(_chosenItemId);
        var valid = item != null && _state.CraftableItemIds.Contains(_chosenItemId);
        _chosenIcon.Texture = valid ? ItemIconTexture.Get(item!) : null;
        if (valid) _chosenIcon.RenderColor = item!.Color;
        _chosenName.Text = valid ? ItemName(_chosenItemId) : T("CraftingOrders.SelectItem");
        _postAction.IsDisabled = !valid;
        _postSummary.Text = valid
            ? F("CraftingOrders.Summary", _quantity.Text, ItemName(_chosenItemId), _reward.Text, _hours.Text)
            : T("CraftingOrders.SelectBeforePublishing");
    }

    private void PublishOrder()
    {
        if (!_state.CraftableItemIds.Contains(_chosenItemId) || ItemDescriptor.Get(_chosenItemId) == null)
        {
            Error("CraftingOrders.SelectBeforePublishing");
            return;
        }
        if (!int.TryParse(_quantity.Text, out var quantity) || quantity is < 1 or > 100 ||
            !int.TryParse(_reward.Text, out var reward) || reward is < 1 or > 100000000 ||
            !int.TryParse(_hours.Text, out var hours) || hours is < 1 or > 720)
        {
            Error("CraftingOrders.InvalidTerms");
            return;
        }
        if (reward > _state.Aureons)
        {
            Error("CraftingOrders.InsufficientFunds");
            return;
        }
        if (Environment.TickCount64 - _postedAt < 1200) return;
        _postedAt = Environment.TickCount64;
        _message.Text = T("CraftingOrders.Sending");
        _message.TextColorOverride = Cream;
        _audio.Play("ui_select");
        Networking.PacketSender.PostCraftingOrder(_chosenItemId, quantity, reward, hours);
    }

    private void Error(string key)
    {
        _message.Text = T(key);
        _message.TextColorOverride = Bad;
        _audio.Play("ui_error");
    }
}
