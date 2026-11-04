using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

/// <summary>Independent event-opened crafting commissions panel.</summary>
internal sealed class CraftingOrdersWindow : Window
{
    private static readonly Color Gold = new(a: 255, r: 220, g: 188, b: 111);
    private static readonly Color Cream = new(a: 255, r: 241, g: 225, b: 195);
    private static readonly Color Muted = new(a: 255, r: 184, g: 171, b: 151);
    private readonly ScrollControl _list;
    private readonly Label _status;
    private readonly Label _balance;
    private readonly TextBox _reward;
    private readonly TextBox _quantity;
    private readonly TextBox _hours;
    private readonly Base _postPanel;
    private readonly Base _postOptions;
    private readonly Button _browseTab;
    private readonly Button _mineTab;
    private readonly Button _postTab;
    private CraftingOrdersStatePacket _state = new();
    private int _tab;

    private static string T(string key) => Intersect.Framework.Core.CraftingOrders.CraftingOrdersText.Get(key, Intersect.Client.Localization.ClientLanguages.Current);

    public CraftingOrdersWindow(Canvas parent) : base(parent, "Crafting Orders", false, nameof(CraftingOrdersWindow))
    {
        SetSize(900, 680);
        Alignment = [Alignments.Center];
        DeleteOnClose = false;
        DisableResizing();

        var frame = new MarketplaceWindow.MarketChrome(this, "CraftingOrderFrame", MarketplaceWindow.FrameStyle.Window);
        frame.SetBounds(12, 28, 875, 630);
        LabelAt(this, "Title", T("Crafting Orders"), 36, 46, 620, 36, 22, Gold);
        _balance = LabelAt(this, "Balance", "", 630, 49, 215, 30, 12, Gold);

        _browseTab = Action(this, "Browse", T("Available Orders"), 38, 102, 233, 39, () => SelectTab(0));
        _mineTab = Action(this, "Mine", T("My Orders"), 280, 102, 233, 39, () => SelectTab(1));
        _postTab = Action(this, "Post", T("Post an Order"), 522, 102, 233, 39, () => SelectTab(2));
        Action(this, "Refresh", T("Refresh"), 756, 102, 106, 39, Networking.PacketSender.RequestCraftingOrders);

        _status = LabelAt(this, "Status", "", 41, 154, 810, 34, 11, Cream);
        _list = new ScrollControl(this, "OrdersList")
        {
            OverflowX = OverflowBehavior.Hidden, OverflowY = OverflowBehavior.Scroll, AutoHideBars = true,
        };
        _list.SetBounds(37, 198, 827, 430);
        _postPanel = new Base(this, "PostOrderPanel");
        _postPanel.SetBounds(37, 198, 827, 430);
        LabelAt(_postPanel, "PostHelp", T("Choose a craftable recipe output to request."), 12, 9, 790, 30, 12, Cream);
        LabelAt(_postPanel, "QuantityLabel", T("Quantity"), 12, 54, 145, 29, 11, Gold);
        _quantity = Input(_postPanel, "Quantity", "1", 169, 54, 115);
        LabelAt(_postPanel, "RewardLabel", T("Reward (Aureons)"), 304, 54, 194, 29, 11, Gold);
        _reward = Input(_postPanel, "Reward", "500", 498, 54, 110);
        LabelAt(_postPanel, "HoursLabel", T("Hours"), 621, 54, 83, 29, 11, Gold);
        _hours = Input(_postPanel, "Hours", "72", 705, 54, 97);
        _postOptions = new Base(_postPanel, "PostOptions");
        _postOptions.SetBounds(0, 100, 816, 310);
        SelectTab(0);
        Hide();
    }

    protected override void EnsureInitialized() { }

    private static Label LabelAt(Base parent, string id, string text, int x, int y, int width, int height, int fontSize, Color color)
    {
        var label = new Label(parent, id)
        {
            Text = text, AutoSizeToContents = false, FontSize = fontSize, TextColorOverride = color,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? parent.Skin.DefaultFont,
            WrappingBehavior = WrappingBehavior.Wrapped,
        };
        label.SetBounds(x, y, width, height);
        return label;
    }

    private static TextBox Input(Base parent, string id, string text, int x, int y, int width)
    {
        var box = new TextBox(parent, id) { Text = text };
        box.SetBounds(x, y, width, 32);
        return box;
    }

    private static Button Action(Base parent, string id, string caption, int x, int y, int width, int height, Action onClick)
    {
        var button = new Button(parent, id) { Text = caption, FontSize = 11, TextColorOverride = Cream };
        button.SetBounds(x, y, width, height);
        var chrome = new MarketplaceWindow.MarketChrome(button, id + "Frame", MarketplaceWindow.FrameStyle.PrimaryAction);
        chrome.SetBounds(0, 0, width, height);
        button.Clicked += (_, _) => onClick();
        return button;
    }

    public void Apply(CraftingOrdersStatePacket packet)
    {
        _state = packet;
        _status.Text = packet.MessageKey.Length == 0 ? T("Commissions are backed by secure Aureons escrow.") : T("CraftingOrders." + packet.MessageKey);
        _balance.Text = $"{packet.Aureons:N0} {T("Aureons")}";
        Rebuild();
    }

    private void SelectTab(int index)
    {
        _tab = index;
        _browseTab.IsDisabled = index == 0;
        _mineTab.IsDisabled = index == 1;
        _postTab.IsDisabled = index == 2;
        Rebuild();
    }

    private void Rebuild()
    {
        if (_tab == 2) { _postPanel.Show(); _list.Hide(); }
        else { _postPanel.Hide(); _list.Show(); }
        if (_tab == 2) { RenderPost(); return; }
        _list.DeleteAll();
        var rows = _state.Orders.Where(order => _tab == 0
            ? order.Status == 0
            : order.IsCustomer || order.IsCrafter).ToArray();
        var y = 3;
        foreach (var order in rows)
        {
            var card = new Base(_list, "Commission_" + order.Id.ToString("N"));
            card.SetBounds(4, y, 785, 84);
            var chrome = new MarketplaceWindow.MarketChrome(card, "Card", MarketplaceWindow.FrameStyle.Card);
            chrome.SetBounds(0, 0, 785, 84);
            LabelAt(card, "Item", $"{order.Quantity} x {ItemDescriptor.GetName(order.ItemId)}", 17, 9, 340, 28, 12, Cream);
            LabelAt(card, "Person", order.Customer + (order.Crafter.Length > 0 ? " / " + order.Crafter : ""), 17, 41, 340, 29, 10, Muted);
            LabelAt(card, "Gold", $"{order.Reward:N0} {T("Aureons")}", 375, 9, 167, 28, 12, Gold);
            LabelAt(card, "Time", DateTimeOffset.FromUnixTimeMilliseconds(order.DeadlineUtcMs).ToLocalTime().ToString("g"),
                375, 42, 170, 28, 10, Muted);
            if (order.Status == 0 && !order.IsCustomer)
                Action(card, "Accept", T("Accept Order"), 561, 23, 206, 38, () => Networking.PacketSender.CraftingOrderAction(order.Id, 0));
            else if (order.Status == 1 && order.IsCrafter)
                Action(card, "Deliver", T("Deliver Crafted Items"), 561, 23, 206, 38, () => Networking.PacketSender.CraftingOrderAction(order.Id, 1));
            else if (order.Status == 0 && order.IsCustomer)
                Action(card, "Cancel", T("Cancel Order"), 561, 23, 206, 38, () => Networking.PacketSender.CraftingOrderAction(order.Id, 2));
            else LabelAt(card, "OrderStatus", T("CraftingOrders.Status." + order.Status), 563, 24, 200, 36, 11, Gold);
            y += 92;
        }
        if (rows.Length == 0) LabelAt(_list, "Empty", T("No crafting orders to show."), 26, 35, 700, 35, 13, Muted);
        _list.SetInnerSize(797, Math.Max(430, y + 10));
        _list.UpdateScrollBars();
    }

    private void RenderPost()
    {
        // Preserve terms while rebuilding the list of eligible recipe outputs.
        _postOptions.DeleteAll();
        var y = 106;
        foreach (var itemId in _state.CraftableItemIds.Take(16))
        {
            var id = itemId;
            Action(_postOptions, "Craftable_" + id.ToString("N"), ItemDescriptor.GetName(id),
                12 + ((y - 106) / 40 % 2) * 401, ((y - 106) / 80) * 46, 390, 37, () =>
                {
                    if (!int.TryParse(_quantity.Text, out var qty) ||
                        !int.TryParse(_reward.Text, out var reward) ||
                        !int.TryParse(_hours.Text, out var hours) ||
                        qty < 1 || reward < 1 || hours < 1)
                    {
                        _status.Text = T("Enter valid order terms.");
                        return;
                    }
                    Networking.PacketSender.PostCraftingOrder(id, qty, reward, hours);
                });
            y += 40;
        }
    }
}
