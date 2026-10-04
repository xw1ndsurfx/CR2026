using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets.Server;
using Intersect.Utilities;
using Rectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

internal sealed class DungeonRewardNotificationWindow : Base
{
    private readonly Queue<DungeonRewardNotificationPacket> _pending = [];
    private readonly ImagePanel _icon;
    private readonly Label _title;
    private readonly Label _primary;
    private readonly Label _secondary;

    private DungeonRewardNotificationPacket? _current;
    private long _hideAt;

    public DungeonRewardNotificationWindow(Canvas parent) : base(parent, nameof(DungeonRewardNotificationWindow))
    {
        SetSize(460, 118);
        MouseInputEnabled = false;
        KeyboardInputEnabled = false;

        _icon = new ImagePanel(this, "DungeonRewardIcon")
        {
            MaintainAspectRatio = true,
            MouseInputEnabled = false,
        };
        _icon.SetBounds(20, 39, 58, 58);

        _title = new Label(this, "DungeonRewardTitle")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 11,
            TextAlign = Pos.Center,
            TextColorOverride = new Color(a: 255, r: 220, g: 196, b: 135),
            MouseInputEnabled = false,
        };
        _title.SetBounds(12, 8, 436, 24);

        _primary = new Label(this, "DungeonRewardPrimary")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 12,
            TextAlign = Pos.Left | Pos.CenterV,
            TextColorOverride = Color.White,
            MouseInputEnabled = false,
        };
        _primary.SetBounds(94, 39, 342, 28);

        _secondary = new Label(this, "DungeonRewardSecondary")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 11,
            TextAlign = Pos.Left | Pos.CenterV,
            TextColorOverride = new Color(a: 255, r: 115, g: 215, b: 125),
            MouseInputEnabled = false,
        };
        _secondary.SetBounds(94, 69, 342, 28);

        Hide();
    }

    public void Enqueue(DungeonRewardNotificationPacket packet)
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

    private void ShowPacket(DungeonRewardNotificationPacket packet)
    {
        _current = packet;
        _hideAt = Timing.Global.Milliseconds + 4_500;

        _title.Text = $"DUNGEON CLEARED • {packet.DungeonName}";

        var item = packet.ItemId == Guid.Empty ? null : ItemDescriptor.Get(packet.ItemId);
        _icon.Texture = null;
        _icon.Hide();

        if (item != null && !string.IsNullOrWhiteSpace(item.Icon))
        {
            var texture = GameContentManager.Current.GetTexture(TextureType.Item, item.Icon);
            if (texture != null)
            {
                _icon.Texture = texture;
                _icon.RenderColor = item.Color;
                _icon.Show();
            }
        }

        if (item != null && packet.ItemQuantity > 0)
        {
            _primary.Text = $"{item.Name} x{packet.ItemQuantity:N0}";
            _secondary.Text = packet.Experience > 0
                ? $"+{packet.Experience:N0} EXP"
                : "Reward received";
        }
        else
        {
            _primary.Text = packet.Experience > 0
                ? $"+{packet.Experience:N0} EXP"
                : "Dungeon completed";
            _secondary.Text = "Rewards delivered";
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

        renderer.DrawColor = new Color(a: 238, r: 37, g: 24, b: 20);
        renderer.DrawFilledRect(bounds);

        renderer.DrawColor = new Color(a: 255, r: 95, g: 175, b: 95);
        renderer.DrawFilledRect(new Rectangle(bounds.X, bounds.Y, bounds.Width, 2));
        renderer.DrawFilledRect(new Rectangle(bounds.X, bounds.Bottom - 2, bounds.Width, 2));
        renderer.DrawFilledRect(new Rectangle(bounds.X, bounds.Y, 2, bounds.Height));
        renderer.DrawFilledRect(new Rectangle(bounds.Right - 2, bounds.Y, 2, bounds.Height));
    }
}
