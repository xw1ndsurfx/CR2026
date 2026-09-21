using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Framework.Core.GameObjects.Items;
using Rectangle = Intersect.Client.Framework.GenericClasses.Rectangle;

namespace Intersect.Client.Interface.Game.Shop;

/// <summary>A readable merchant row with explicit price and action.</summary>
internal sealed class ShopProductRow : Base
{
    private readonly ItemDescriptor _item;
    private readonly ImagePanel _icon;
    private readonly Label _name;
    private readonly Label _detail;
    private readonly Label _price;
    private readonly Button _action;

    private ShopProductRow(
        Base parent,
        string name,
        ItemDescriptor item,
        string detail,
        string price,
        string action,
        bool enabled,
        Action onAction
    ) : base(parent, name)
    {
        _item = item;
        Height = 70;
        MinimumSize = new Point(320, 70);
        Margin = new Margin(0, 0, 0, 6);
        ShouldDrawBackground = false;

        _icon = new ImagePanel(this, "Icon")
        {
            ShouldDrawBackground = false,
            MouseInputEnabled = true,
        };
        _icon.SetBounds(10, 11, 48, 48);
        _icon.Texture = Globals.ContentManager?.GetTexture(Framework.Content.TextureType.Item, item.Icon);
        _icon.RenderColor = item.Color;
        _icon.HoverEnter += (_, _) => Interface.GameUi.ItemDescriptionWindow?.Show(item, 1);
        _icon.HoverLeave += (_, _) => Interface.GameUi.ItemDescriptionWindow?.Hide();

        _name = Label("Name", 70, 8, 154, 23, 12, Color.White);
        _name.Text = item.Name;

        _detail = Label("Detail", 70, 31, 154, 19, 9, new Color(180, 180, 180));
        _detail.Text = detail;

        _price = Label("Price", 70, 50, 154, 18, 10, new Color(231, 194, 112));
        _price.Text = price;

        _action = new Button(this, "Action")
        {
            Font = Skin.DefaultFont,
            FontSize = 11,
            Text = action,
            IsDisabled = !enabled,
        };
        _action.SetBounds(232, 19, 100, 32);
        _action.Clicked += (_, _) => onAction();
    }

    public static ShopProductRow Buy(
        Base parent,
        int shopSlot,
        ItemDescriptor item,
        int price,
        string currency,
        bool canAfford,
        Action action
    ) => new(
        parent,
        "ShopBuy" + shopSlot,
        item,
        canAfford ? Strings.Shop.Available.ToString() : Strings.Shop.NotEnoughCurrency.ToString(),
        Price(price, currency),
        Strings.Shop.BuyItem.ToString(),
        canAfford,
        action
    );

    public static ShopProductRow Sell(
        Base parent,
        int inventorySlot,
        ItemDescriptor item,
        int owned,
        int price,
        string currency,
        Action action
    ) => new(
        parent,
        "ShopSell" + inventorySlot,
        item,
        Strings.Shop.Owned.ToString(owned),
        Price(price, currency),
        Strings.Shop.SellItem.ToString(),
        true,
        action
    );

    private static string Price(int amount, string currency) => amount <= 0 ? Strings.Shop.Free.ToString() : $"{amount} {currency}";

    private Label Label(string name, int x, int y, int width, int height, int size, Color color)
    {
        var label = new Label(this, name)
        {
            AutoSizeToContents = false,
            Font = Skin.DefaultFont,
            FontSize = size,
            TextColorOverride = color,
            MouseInputEnabled = false,
        };
        label.SetBounds(x, y, width, height);
        return label;
    }

    protected override void Render(SkinBase skin)
    {
        skin.Renderer.DrawColor = new Color(34, 38, 42);
        skin.Renderer.DrawFilledRect(new Rectangle(0, 0, Width, Height - 1));
        skin.Renderer.DrawColor = new Color(70, 74, 79);
        skin.Renderer.DrawFilledRect(new Rectangle(0, Height - 1, Width, 1));
    }
}
