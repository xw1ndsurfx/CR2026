using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.Control.EventArguments;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Interface.Shared;
using Intersect.Client.Localization;
using Intersect.Client.Networking;
using UiRectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

/// <summary>
/// A readable friend entry with explicit Profile / Message / Remove actions.
/// Player profiles are currently served for online characters only.
/// </summary>
public partial class FriendsRow
{
    public const int RowWidth = 610;
    public const int RowHeight = 80;

    private static readonly Color Gold = new(a: 255, r: 229, g: 192, b: 116);
    private static readonly Color Cream = new(a: 255, r: 242, g: 229, b: 205);
    private static readonly Color Muted = new(a: 255, r: 180, g: 169, b: 157);
    private static readonly Color OnlineGreen = new(a: 255, r: 134, g: 217, b: 147);

    private readonly Base _parent;
    private readonly Base _row;
    private readonly string _friendName;
    private readonly bool _online;

    public FriendsRow(Base parent, string name, string location, bool online)
    {
        _parent = parent;
        _friendName = name ?? string.Empty;
        _online = online;

        _row = new Base(parent, "FriendRow")
        {
            MouseInputEnabled = false,
        };
        _row.SetBounds(0, 0, RowWidth, RowHeight);

        var frame = new FriendCardChrome(_row, "FriendRowFrame", online);
        frame.SetBounds(0, 0, RowWidth, RowHeight);
        frame.SendToBack();

        var displayName = _friendName.Length > 22
            ? _friendName[..19] + "..." : _friendName;
        var nameLabel = AddText(_row, "FriendDisplayName", displayName,
            36, 9, 200, 26, 14, online ? Cream : Muted, bold: true);
        nameLabel.SetToolTipText(_friendName);
        nameLabel.MouseInputEnabled = online;
        if (online)
        {
            nameLabel.SetToolTipText(Intersect.Client.Localization.ClientLanguages.Text("View Character Information: ") + _friendName);
            nameLabel.Clicked += OpenProfile;
        }

        AddText(_row, "FriendPresence", online ? "ONLINE" : "OFFLINE",
            240, 13, 78, 20, 9, online ? OnlineGreen : Muted, bold: true);

        var safeLocation = (location ?? string.Empty)
            .Replace("\r", " ").Replace("\n", " ").Trim();
        if (safeLocation.Length > 32)
            safeLocation = safeLocation[..29] + "...";
        AddText(_row, "FriendLocation",
            online ? (safeLocation.Length > 0 ? safeLocation : "Location unavailable") : "Not connected",
            36, 43, 280, 19, 10, Muted);

        var profile = AddAction(_row, "FriendViewProfile", "PROFILE", 326, 22, 88, 35,
            online, isPrimary: true);
        profile.SetToolTipText(online
            ? "View Character Information for " + _friendName
            : "Character Information is available when this friend is online");
        profile.Clicked += OpenProfile;

        var message = AddAction(_row, "FriendSendMessage", "MESSAGE", 420, 22, 96, 35,
            online, isPrimary: false);
        message.SetToolTipText(online ? "Send a private message" : "Friend is offline");
        message.Clicked += SendMessage;

        var remove = AddAction(_row, "FriendRemove", "REMOVE", 522, 22, 78, 35,
            true, isPrimary: false);
        remove.SetToolTipText(Intersect.Client.Localization.ClientLanguages.Text("Remove ") + _friendName + " from friends");
        remove.Clicked += RemoveFriend;
    }

    public Rectangle Bounds => _row.Bounds;

    public void SetPosition(float x, float y) => _row.SetPosition(x, y);

    public void Dispose() => _parent.RemoveChild(_row, true);

    private void OpenProfile(Base sender, MouseButtonState args)
    {
        // Never request an unavailable offline character profile.
        if (_online && !string.IsNullOrWhiteSpace(_friendName))
            PacketSender.SendRequestPlayerProfile(_friendName, openWindow: true);
    }

    private void SendMessage(Base sender, MouseButtonState args)
    {
        if (_online && !string.IsNullOrWhiteSpace(_friendName))
            Interface.GameUi.SetChatboxText($"/pm {_friendName} ");
    }

    private void RemoveFriend(Base sender, MouseButtonState args)
    {
        _ = new InputBox(
            title: Strings.Friends.RemoveFriend,
            prompt: Strings.Friends.RemoveFriendPrompt.ToString(_friendName),
            inputType: InputType.YesNo,
            onSubmit: (control, submission) => PacketSender.SendRemoveFriend(_friendName)
        );
    }

    private static Label AddText(Base parent, string id, string value,
        int x, int y, int width, int height, int fontSize, Color color, bool bold = false)
    {
        var label = new Label(parent, id)
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont(
                bold ? "sourcesansproblack" : "sourcesanspro") ?? parent.Skin.DefaultFont,
            FontSize = fontSize,
            TextAlign = Pos.Left | Pos.CenterV,
            TextColorOverride = color,
            Text = value,
            MouseInputEnabled = false,
        };
        label.SetBounds(x, y, width, height);
        return label;
    }

    private static Button AddAction(Base parent, string id, string text,
        int x, int y, int width, int height, bool enabled, bool isPrimary)
    {
        var button = new Button(parent, id)
        {
            Text = string.Empty,
            IsDisabled = !enabled,
        };
        button.SetBounds(x, y, width, height);

        var frame = new FriendButtonChrome(button, id + "Frame", enabled, isPrimary);
        frame.SetBounds(0, 0, width, height);
        frame.SendToBack();

        var label = AddText(button, id + "Caption", text, 4, 1, width - 8, height - 2,
            9, !enabled ? Muted : isPrimary ? Gold : Cream, bold: true);
        label.TextAlign = Pos.Center;
        label.BringToFront();
        return button;
    }

    private sealed class FriendCardChrome : Base
    {
        private readonly bool _online;

        public FriendCardChrome(Base parent, string name, bool online) : base(parent, name)
        {
            _online = online;
            MouseInputEnabled = false;
        }

        protected override void Render(SkinBase skin)
        {
            var r = RenderBounds;
            var border = _online
                ? new Color(a: 255, r: 144, g: 107, b: 62)
                : new Color(a: 255, r: 88, g: 75, b: 69);
            var fill = _online
                ? new Color(a: 255, r: 46, g: 31, b: 27)
                : new Color(a: 255, r: 34, g: 27, b: 27);

            Fill(skin, r.X, r.Y, r.Width, r.Height, fill);
            Fill(skin, r.X, r.Y, r.Width, 2, border);
            Fill(skin, r.X, r.Bottom - 2, r.Width, 2, border);
            Fill(skin, r.X, r.Y, 2, r.Height, border);
            Fill(skin, r.Right - 2, r.Y, 2, r.Height, border);
            Fill(skin, r.X + 11, r.Y + 19, 13, 13, border);
            Fill(skin, r.X + 14, r.Y + 22, 7, 7,
                _online ? OnlineGreen : Muted);
        }
    }

    private sealed class FriendButtonChrome : Base
    {
        private readonly bool _enabled;
        private readonly bool _primary;

        public FriendButtonChrome(Base parent, string name, bool enabled, bool primary)
            : base(parent, name)
        {
            _enabled = enabled;
            _primary = primary;
            MouseInputEnabled = false;
        }

        protected override void Render(SkinBase skin)
        {
            var r = RenderBounds;
            var border = !_enabled
                ? new Color(a: 255, r: 81, g: 72, b: 66)
                : _primary ? new Color(a: 255, r: 204, g: 157, b: 87)
                    : new Color(a: 255, r: 133, g: 99, b: 70);
            var fill = !_enabled
                ? new Color(a: 255, r: 36, g: 33, b: 33)
                : _primary ? new Color(a: 255, r: 78, g: 54, b: 35)
                    : new Color(a: 255, r: 57, g: 40, b: 34);

            Fill(skin, r.X, r.Y, r.Width, r.Height, fill);
            Fill(skin, r.X, r.Y, r.Width, 2, border);
            Fill(skin, r.X, r.Bottom - 2, r.Width, 2, border);
            Fill(skin, r.X, r.Y, 2, r.Height, border);
            Fill(skin, r.Right - 2, r.Y, 2, r.Height, border);
        }
    }

    private static void Fill(SkinBase skin, int x, int y, int width, int height, Color color)
    {
        if (width <= 0 || height <= 0)
            return;

        skin.Renderer.DrawColor = color;
        skin.Renderer.DrawFilledRect(new UiRectangle(x, y, width, height));
    }
}
