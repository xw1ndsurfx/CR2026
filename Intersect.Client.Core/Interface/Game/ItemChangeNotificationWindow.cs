using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.GameObjects;
using Intersect.Network.Packets.Server;
using Intersect.Utilities;
using Intersect.Client.Interface.Game.DescriptionWindows;
using Rectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

internal sealed class ItemChangeNotificationWindow : Base
{
    private readonly Queue<ItemChangeNotificationPacket> _pending = [];
    private readonly ImagePanel _icon;
    private readonly Label _name;
    private readonly Label _quantity;
    private readonly Label _heading;
    private Color? _rarityColor;
    private bool _celebrating;
    private ItemChangeNotificationPacket? _current;
    private long _hideAt;

    public ItemChangeNotificationWindow(Canvas parent) : base(parent, nameof(ItemChangeNotificationWindow))
    {
        SetSize(420, 92);
        MouseInputEnabled = false;
        KeyboardInputEnabled = false;

        _heading = new Label(this, "LootCongratulations")
        {
            AutoSizeToContents = false, Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 13, Text = "Félicitations, vous avez obtenu :", MouseInputEnabled = false,
            TextAlign = Pos.Center, TextColorOverride = Color.White, IsHidden = true,
        };

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
        if (packet.IsLoot && (packet.SignedQuantity <= 0 ||
            ItemDescriptor.Get(packet.ItemId) is not { } item ||
            ItemRarityGlow.ResolveColor(item.Rarity, Options.Instance.Items.RarityTiers, CustomColors.Items.Rarities) == null)) return;
        if (_current == null)
        {
            ShowPacket(packet);
            return;
        }

        _pending.Enqueue(packet);
    }

    public void Update()
    {
        if (_current != null) PositionWindow();
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
        var descriptor = ItemDescriptor.Get(packet.ItemId);
        _rarityColor = descriptor == null ? null : ItemRarityGlow.ResolveColor(
            descriptor.Rarity, Options.Instance.Items.RarityTiers, CustomColors.Items.Rarities);
        _celebrating = packet.IsLoot && _rarityColor != null;
        _hideAt = Timing.Global.Milliseconds + (_celebrating ? 5_000 : 2_800);
        SetSize(Math.Min(_celebrating ? 480 : 420, Math.Max(200, (Parent?.Width ?? 640) - 64)), _celebrating ? 150 : 92);
        _heading.IsHidden = !_celebrating;
        _heading.SetBounds(12, 12, Width - 24, 26);
        _icon.SetBounds(20, _celebrating ? 56 : 18, 56, 56);
        _name.SetBounds(92, _celebrating ? 52 : 18, Width - 112, 30);
        _quantity.SetBounds(92, _celebrating ? 88 : 46, Width - 112, 32);
        _name.FontSize = _celebrating ? 16 : 12;
        _quantity.FontSize = _celebrating ? 12 : 16;
        _name.TextColorOverride = _celebrating ? _rarityColor! : Color.White;
        _heading.TextColorOverride = _celebrating ? _rarityColor! : Color.White;
        _name.Text = descriptor?.Name ?? "Item";

        var sign = packet.SignedQuantity > 0 ? "+" : string.Empty;
        _quantity.Text = $"{sign}{packet.SignedQuantity:N0}";
        _quantity.TextColorOverride = packet.SignedQuantity >= 0
            ? new Color(a: 255, r: 115, g: 215, b: 125)
            : new Color(a: 255, r: 225, g: 95, b: 85);
        if (_celebrating && descriptor != null)
        {
            var rarity = Options.Instance.Items.TryGetRarityName(descriptor.Rarity, out var rarityName) ? rarityName : "Rare";
            if (Intersect.Client.Localization.Strings.ItemDescription.Rarity.TryGetValue(rarity, out var localized)) rarity = localized;
            _quantity.Text = $"Rareté : {rarity} - x{packet.SignedQuantity:N0}";
            _quantity.TextColorOverride = _rarityColor!;
        }

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
        Y = Math.Max(24, Math.Min(258, (Parent?.Height ?? 720) - Height - 32));
    }

    protected override void RenderUnder(SkinBase skin)
    {
        base.RenderUnder(skin);
        if (_celebrating && _rarityColor is { } color)
            ItemRarityGlow.Draw(skin.Renderer, Width, Height, color, Environment.TickCount64);
    }

    protected override void Render(SkinBase skin)
    {
        if (_current == null)
            return;

        var renderer = skin.Renderer;
        var bounds = RenderBounds;

        renderer.DrawColor = new Color(a: 235, r: 37, g: 24, b: 20);
        renderer.DrawFilledRect(bounds);

        if (_celebrating && _rarityColor is { } tint)
        {
            renderer.DrawColor = new Color(28, tint.R, tint.G, tint.B);
            renderer.DrawFilledRect(new Rectangle(bounds.X + 2, bounds.Y + 2, bounds.Width - 4, 38));
        }

        renderer.DrawColor = _celebrating ? _rarityColor! : _current.SignedQuantity >= 0
            ? new Color(a: 255, r: 95, g: 175, b: 95)
            : new Color(a: 255, r: 195, g: 75, b: 65);

        renderer.DrawFilledRect(new Rectangle(bounds.X, bounds.Y, bounds.Width, 2));
        renderer.DrawFilledRect(new Rectangle(bounds.X, bounds.Bottom - 2, bounds.Width, 2));
        renderer.DrawFilledRect(new Rectangle(bounds.X, bounds.Y, 2, bounds.Height));
        renderer.DrawFilledRect(new Rectangle(bounds.Right - 2, bounds.Y, 2, bounds.Height));
    }
}
