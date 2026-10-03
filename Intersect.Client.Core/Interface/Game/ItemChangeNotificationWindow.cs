using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Framework.Core;
using Intersect.GameObjects;
using Intersect.Network.Packets.Server;
using Intersect.Utilities;
using Rectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

internal sealed class ItemChangeNotificationWindow : Base
{
    private readonly Queue<ItemChangeNotificationPacket> _pending = [];
    private readonly ImagePanel _icon;
    private readonly Label _name;
    private readonly Label _quantity;
    private ItemChangeNotificationPacket? _current;
    private long _hideAt;

    public ItemChangeNotificationWindow(Canvas parent) : base(parent, nameof(ItemChangeNotificationWindow))
    {
        SetSize(420, 92);
        MouseInputEnabled = false;
        KeyboardInputEnabled = false;

        _icon = new ImagePanel(this, "ItemChangeIcon")
        {
            MaintainAspectRatio = true,
            MouseInputEnabled = false,
        };
        _icon.SetBounds(20, 18, 56, 56);

        _name = new Label(this, "ItemChangeName")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 12,
            TextAlign = Pos.Left | Pos.CenterV,
            TextColorOverride = Color.White,
            MouseInputEnabled = false,
        };
        _name.SetBounds(92, 18, 300, 26);

        _quantity = new Label(this, "ItemChangeQuantity")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 16,
            TextAlign = Pos.Left | Pos.CenterV,
            MouseInputEnabled = false,
        };
        _quantity.SetBounds(92, 46, 300, 28);

        Hide();
    }

    public void Enqueue(ItemChangeNotificationPacket packet)
    {
        if (_current == null)
        {
            ShowPacket(packet);
            return;
        }

        _pending.Enqueue(packet);
    }

    public void Update()
    {
        if (_current == null || Timing.Global.Milliseconds < _hideAt)
            return;

        if (_pending.Count > 0)
        {
            ShowPacket(_pending.Dequeue());
            return;
        }

        _current = null;
        Hide();
    }

    private void ShowPacket(ItemChangeNotificationPacket packet)
    {
        _current = packet;
        _hideAt = Timing.Global.Milliseconds + 2_800;

        var descriptor = ItemDescriptor.Get(packet.ItemId);
        _name.Text = descriptor?.Name ?? "Item";

        var sign = packet.SignedQuantity > 0 ? "+" : string.Empty;
        _quantity.Text = $"{sign}{packet.SignedQuantity:N0}";
        _quantity.TextColorOverride = packet.SignedQuantity >= 0
            ? new Color(a: 255, r: 115, g: 215, b: 125)
            : new Color(a: 255, r: 225, g: 95, b: 85);

        _icon.Texture = null;
        _icon.Hide();
        if (descriptor != null && !string.IsNullOrWhiteSpace(descriptor.Icon))
        {
            var texture = GameContentManager.Current.GetTexture(TextureType.Item, descriptor.Icon);
            if (texture != null)
            {
                _icon.Texture = texture;
                _icon.RenderColor = descriptor.Color;
                _icon.Show();
            }
        }

        PositionWindow();
        Show();
        BringToFront();
    }

    private void PositionWindow()
    {
        X = Math.Max(8, (Parent?.Width ?? Width) / 2 - Width / 2);
        Y = 258;
    }

    protected override void Render(SkinBase skin)
    {
        if (_current == null)
            return;

        var renderer = skin.Renderer;
        var bounds = RenderBounds;

        renderer.DrawColor = new Color(a: 235, r: 37, g: 24, b: 20);
        renderer.DrawFilledRect(bounds);

        renderer.DrawColor = _current.SignedQuantity >= 0
            ? new Color(a: 255, r: 95, g: 175, b: 95)
            : new Color(a: 255, r: 195, g: 75, b: 65);

        renderer.DrawFilledRect(new Rectangle(bounds.X, bounds.Y, bounds.Width, 2));
        renderer.DrawFilledRect(new Rectangle(bounds.X, bounds.Bottom - 2, bounds.Width, 2));
        renderer.DrawFilledRect(new Rectangle(bounds.X, bounds.Y, 2, bounds.Height));
        renderer.DrawFilledRect(new Rectangle(bounds.Right - 2, bounds.Y, 2, bounds.Height));
    }
}
