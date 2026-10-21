using Intersect.Client.Framework.Gwen;
using Intersect.Client.Interface.Game;
using Intersect.Client.Framework.Gwen.Control;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;
using Intersect.Client.General;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Localization;
using Intersect.Framework.Core.GameObjects.Items;
using Rectangle = Intersect.Client.Framework.GenericClasses.Rectangle;

namespace Intersect.Client.Interface.Game.Shop;

/// <summary>A readable merchant row with explicit price and action.</summary>
internal sealed class ShopProductRow : Base
{
    private readonly ItemDescriptor? _item;
    private readonly ImagePanel _icon;
    private readonly Label _name;
    private readonly Label _detail;
    private readonly Label _price;
    private readonly Button _action;
    private readonly bool _admurinArt;

    private ShopProductRow(
        Base parent,
        string name,
        ItemDescriptor? item,
        string detail,
        string price,
        string action,
        bool enabled,
        Action onAction,
        string? serviceName=null,string? serviceIcon=null,bool nativeButton=false
    ) : base(parent, name)
    {
        _item = item;
        Height = 75;
        MinimumSize = new Point(320, 75);
        Margin = new Margin(0, 0, 0, 1);
        ShouldDrawBackground = false;

        // The original card is 185x54. Full-image scaling distorted the
        // square item recess and extended 13px underneath the native scroll
        // bar (350px viewport minus its 17px gutter). Nine-slice keeps the
        // pixel-art corners/recess sharp inside the 333px content column.
        _admurinArt = AdmurinGuiArt.AddNineSlice(
            this, "AdmurinMerchantOffer", "shop_1",
            0, 0, 332, 74,
            185, 54, 50, 5, 7, 5, 75, 7, 9, 7) != null;

        _icon = new ImagePanel(this, "Icon")
        {
            ShouldDrawBackground = false,
            MouseInputEnabled = true,
        };
        _icon.SetBounds(15, 12, 52, 52);
        _icon.Texture = Globals.ContentManager?.GetTexture(item==null?Framework.Content.TextureType.Spell:Framework.Content.TextureType.Item, item?.Icon??serviceIcon??"");
        _icon.RenderColor = item?.Color??Color.White;
        if(item!=null)_icon.HoverEnter += (_, _) => Interface.GameUi.ItemDescriptionWindow?.Show(item, 1);
        _icon.HoverLeave += (_, _) => Interface.GameUi.ItemDescriptionWindow?.Hide();

        _name = Label("Name", 91, 5, 137, 24, 12, new Color(a: 255, r: 245, g: 231, b: 210));
        _name.Text = Shorten(serviceName??item?.Name, 19);

        _detail = Label("Detail", 91, 29, 137, 18, 9, new Color(a: 255, r: 191, g: 174, b: 152));
        _detail.Text = Shorten(detail, 24);

        _price = Label("Price", 91, 49, 137, 20, 10, new Color(a: 255, r: 231, g: 194, b: 112));
        _price.Text = Shorten(price, 21);
        SetToolTipText((serviceName??item?.Name) + "\n" + detail + "\n" + price);

        _action = new Button(this, "Action")
        {
            Font = Skin.DefaultFont,
            FontSize = 11,
            Text = action,
            IsDisabled = !enabled,
        };
        _action.SetBounds(232, 19, 95, 36);
        if(nativeButton)NavalManagementWindow.StyleButton(_action);
        var buttonArt = nativeButton?null:AdmurinGuiArt.Add(_action, "AdmurinMerchantButton", "shop_2",
            0, 0, 95, 36,
            enabled ? 0 : 56, enabled ? (action == Strings.Shop.SellItem.ToString() ? 54 : 0) : 54,
            56, 54);
        if (buttonArt != null)
        {
            _action.ShouldDrawBackground = false;
            // Gwen buttons paint their own label before child sprites. Draw a
            // separate caption above the sprite so BUY / SELL stays legible.
            _action.Text = string.Empty;
            var caption = new Label(_action, "MerchantButtonCaption")
            {
                Text = action,
                Font = Skin.DefaultFont,
                FontSize = 11,
                TextColorOverride = enabled ? Color.White :
                    new Color(a: 255, r: 180, g: 162, b: 150),
                TextAlign = Pos.Center,
                AutoSizeToContents = false,
                MouseInputEnabled = false,
            };
            caption.SetBounds(0, 0, 95, 36);
        }
        _action.Clicked += (_, _) =>
        {
            if (!_action.IsDisabled)
                onAction();
        };
    }

    internal void ShowShipPreview(string graphic,int frames)
    {
        var texture=GameContentManager.Current.GetTexture(Framework.Content.TextureType.Ocean,graphic);
        var atlas=texture==null?null:Naval.NavalSpriteCatalog.Get(graphic,texture.Width,texture.Height,frames);
        if(atlas==null)return;
        var frame=atlas.Frame(180,0,180);_icon.Texture=texture;_icon.MaintainAspectRatio=true;
        _icon.SetTextureRect(frame.X,frame.Y,frame.Width,frame.Height);
    }
    internal static ShopProductRow Service(Base parent,string id,string name,string detail,string price,string caption,bool enabled,Action action,string icon="")=>
        new(parent,id,null,detail,price,caption,enabled,action,name,icon,true);

    public static ShopProductRow Buy(
        Base parent,
        int shopSlot,
        ItemDescriptor item,
        int price,
        string currency,
        bool canAfford,
        Action action,bool nativeButton=false
    ) => new(
        parent,
        "ShopBuy" + shopSlot,
        item,
        canAfford ? Strings.Shop.Available.ToString() : Strings.Shop.NotEnoughCurrency.ToString(),
        Price(price, currency),
        Strings.Shop.BuyItem.ToString(),
        canAfford,
        action,nativeButton:nativeButton
    );

    public static ShopProductRow Sell(
        Base parent,
        int inventorySlot,
        ItemDescriptor item,
        int owned,
        int price,
        string currency,
        Action action,bool nativeButton=false
    ) => new(
        parent,
        "ShopSell" + inventorySlot,
        item,
        Strings.Shop.Owned.ToString(owned),
        Price(price, currency),
        Strings.Shop.SellItem.ToString(),
        true,
        action,nativeButton:nativeButton
    );

    private static string Shorten(string? value, int maximum)
    {
        var text = (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
        return text.Length <= maximum ? text : text[..Math.Max(0, maximum - 3)] + "...";
    }

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
        if (_admurinArt)
            return;

        skin.Renderer.DrawColor = new Color(34, 38, 42);
        skin.Renderer.DrawFilledRect(new Rectangle(0, 0, Width, Height - 1));
        skin.Renderer.DrawColor = new Color(70, 74, 79);
        skin.Renderer.DrawFilledRect(new Rectangle(0, Height - 1, Width, 1));
    }
}
