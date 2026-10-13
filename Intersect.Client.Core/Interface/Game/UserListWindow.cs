using Intersect.Client.Core;
using Intersect.Client.Framework.Entities;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.Control.EventArguments.InputSubmissionEvent;
using Intersect.Client.General;
using Intersect.Client.Interface.Shared;
using Intersect.Client.Localization;
using Intersect.Client.Networking;
using Intersect.Utilities;
using UiRectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;
using Label = Intersect.Client.Framework.Gwen.Control.Label;

namespace Intersect.Client.Interface.Game;

/// <summary>
/// Admurin's unified User List with Friends, Blocked, Guild and Party pages.
/// Existing game packets remain the source of truth: do not invent blocked
/// users or guild applications where the server has no such protocol.
/// </summary>
internal sealed partial class UserListWindow : Window
{
    internal enum Page
    {
        Friends,
        Blocked,
        Guild,
        Party,
    }

    private static readonly Color Gold = new(a: 255, r: 231, g: 199, b: 135);
    private static readonly Color Cream = new(a: 255, r: 246, g: 235, b: 211);
    private static readonly Color Muted = new(a: 255, r: 162, g: 150, b: 141);
    private static readonly Color Online = new(a: 255, r: 126, g: 215, b: 137);
    private static readonly Color Offline = new(a: 255, r: 136, g: 132, b: 129);

    private readonly ScrollControl _body;
    private readonly Base _footer;
    private readonly Action _openGuildManagement;
    private readonly Dictionary<Page, Label> _tabCaptions = [];
    private Page _currentPage = Page.Friends;
    private string _lastSnapshot = string.Empty;
    private Intersect.Network.Packets.Server.BlockedUserEntry[] _blockedUsers = [];
    private long _nextUpdateTime;

    public bool IsVisible => !IsHidden;

    public bool IsShowing(Page page) => !IsHidden && _currentPage == page;

    public UserListWindow(Canvas canvas, Action openGuildManagement)
        : base(canvas, "User List", false, nameof(UserListWindow))
    {
        _openGuildManagement = openGuildManagement;
        SetSize(550, 624);
        Alignment = [Alignments.Center];
        DeleteOnClose = false;
        DisableResizing();

        // Window already supplies the title bar and frame. Keep artist icons,
        // but do not place a second complete window inside the native frame.

        string[] names = ["Friends", "Blocked", "Guild", "Party"];
        for (var i = 0; i < names.Length; i++)
        {
            var page = (Page)i;
            var tab = new Button(this, "UserListTab" + page) { Text = string.Empty };
            tab.SetBounds(39 + i * 118, 12, 117, 33);
            var caption = AddLabel(tab, "Caption", names[i], 0, 1, 116, 30, 13, Muted, true);
            _tabCaptions[page] = caption;
            tab.Clicked += (_, _) => OpenPage(page);
        }

        _body = new ScrollControl(this, "UserListBody")
        {
            OverflowX = OverflowBehavior.Hidden,
            OverflowY = OverflowBehavior.Scroll,
            AutoHideBars = true,
        };
        _body.SetBounds(31, 56, 491, 465);

        _footer = new Base(this, "UserListActions");
        _footer.SetBounds(32, 526, 486, 71);
        Hide();
    }

    protected override void EnsureInitialized() { }

    public void OpenPage(Page page)
    {
        var changedPage = page != _currentPage;
        _currentPage = page;
        _lastSnapshot = string.Empty;
        Refresh(resetScroll: changedPage);
        Show();
        BringToFront();

        // The existing roster packets are still authoritative.
        if (page == Page.Friends)
            PacketSender.SendRequestFriends();
        if (page == Page.Blocked)
            PacketSender.SendRequestBlockedUsers();
        if (page == Page.Guild)
        {
            if (!string.IsNullOrWhiteSpace(Globals.Me?.Guild))
                PacketSender.SendRequestGuild();
            else if (_guildDirectoryOpen)
                RequestGuildDirectory();
        }
    }

    public void RefreshIfShowing(Page page)
    {
        if (IsShowing(page))
            Refresh();
    }

    public void Update()
    {
        PumpGuildDirectory();
        if (!IsVisible || Environment.TickCount64 < _nextUpdateTime)
            return;

        _nextUpdateTime = Environment.TickCount64 + 800;
        var snapshot = Snapshot();
        if (snapshot != _lastSnapshot)
            Refresh();

        if (_currentPage == Page.Party)
            UpdatePartyVitals();
    }

    private string Snapshot()
    {
        var player = Globals.Me;
        if (player == null)
            return "disconnected";

        return _currentPage switch
        {
            Page.Friends => "friends:" + string.Join("|",
                (player.Friends ?? [])
                    .Select(friend => friend.Name + ":" + friend.Online + ":" + friend.Map)),
            Page.Guild => "guild:" + player.Guild + ":" + string.Join("|",
                (player.GuildMembers ?? [])
                    .Select(member => member.Id + ":" + member.Level + ":" + member.Rank + ":" + member.Online)),
            // Stable member/level changes rebuild the roster. Vital changes
            // update the already-painted gauge controls without recreating
            // their click targets during combat.
            Page.Party => "party:" + string.Join("|",
                (player.Party ?? [])
                    .Select(member => member.Id + ":" + member.Name + ":" + member.Level)),
            Page.Blocked => "blocked:" + string.Join("|", _blockedUsers.Select(entry => entry.Id + ":" + entry.Name)),
            _ => string.Empty,
        };
    }

    private void Refresh(bool resetScroll = false)
    {
        // Party vitals and friend presence can update while the player is
        // scrolling. Rebuilding the rows must not jump them back to the top.
        // Tab changes intentionally start at the beginning of the new list.
        var savedScroll = resetScroll ? 0f : _body.VerticalScrollBar.ScrollAmount;
        _body.DeleteAll();
        _footer.DeleteAllChildren();

        foreach (var (page, caption) in _tabCaptions)
            caption.TextColorOverride = page == _currentPage ? Gold : Muted;

        switch (_currentPage)
        {
            case Page.Friends:
                DrawFriends();
                break;
            case Page.Blocked:
                DrawBlocked();
                break;
            case Page.Guild:
                DrawGuild();
                break;
            case Page.Party:
                DrawParty();
                break;
        }

        _body.UpdateScrollBars();
        _body.VerticalScrollBar.SetScrollAmount(savedScroll, forceUpdate: true);
        _lastSnapshot = Snapshot();
    }

    private void DrawFriends()
    {
        IFriendInstance[] friends = Globals.Me?.Friends?.ToArray() ?? [];
        var online = friends.Where(friend => friend.Online)
            .OrderBy(friend => friend.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var offline = friends.Where(friend => !friend.Online)
            .OrderBy(friend => friend.Name, StringComparer.OrdinalIgnoreCase).ToArray();

        AddLabel(_body, "OnlineHeading", "ONLINE  " + online.Length,
            9, 7, 216, 23, 12, Gold, true);
        AddLabel(_body, "OfflineHeading", "OFFLINE  " + offline.Length,
            247, 7, 216, 23, 12, Gold, true);
        var divider = new VerticalDivider(_body, "FriendColumnDivider");
        divider.IsHidden = friends.Length == 0;
        var count = Math.Max(online.Length, offline.Length);
        var listHeight = Math.Max(420, 41 + count * 41);
        divider.SetBounds(233, 31, 2, listHeight - 38);

        for (var index = 0; index < online.Length; index++)
            DrawFriend(online[index], 6, 36 + index * 41);

        for (var index = 0; index < offline.Length; index++)
            DrawFriend(offline[index], 245, 36 + index * 41);

        if (friends.Length == 0)
            AddLabel(_body, "FriendsEmpty", "No friends yet. Use ADD FRIEND below.",
                18, 167, 431, 36, 11, Muted, true);

        _body.SetInnerSize(470, listHeight);

        AddAction(_footer, "AddFriend", "ADD FRIEND", 98, 9, 140, 46, AddFriend);
        AddAction(_footer, "BlockUser", "BLOCK USER", 248, 9, 140, 46, BlockUser);
    }

    private void DrawFriend(IFriendInstance friend, int x, int y)
    {
        var panel = new Base(_body, "Friend" + x + y);
        panel.SetBounds(x, y, 221, 35);
        var online = friend.Online;

        AdmurinGuiArt.Add(panel, "PlayerIcon", "userlist_2",
            3, 8, 22, 21, online ? 0 : 16, 0, 16, 16);

        var name = Shorten(friend.Name, 15);
        var nameLabel = AddLabel(panel, "FriendName", name,
            29, 4, 124, 27, 12, online ? Cream : Muted);
        if (online)
        {
            nameLabel.MouseInputEnabled = true;
            nameLabel.SetToolTipText("Whisper to " + friend.Name);
            nameLabel.Clicked += (_, _) => Interface.GameUi.SetChatboxText("/pm " + friend.Name + " ");
        }
        else
        {
            nameLabel.SetToolTipText("Offline: " + friend.Name);
        }

        var profile = new Button(panel, "ViewFriendInfo") { Text = string.Empty, IsDisabled = !online };
        profile.SetBounds(161, 6, 25, 25);
        AdmurinGuiArt.Add(profile, "InfoIcon", "userlist_1", 0, 0, 25, 25,
            online ? 0 : 20, 0, 20, 20);
        profile.SetToolTipText(online
            ? "View Character Information: " + friend.Name
            : "Character Information is available while the friend is online.");
        profile.Clicked += (_, _) => PacketSender.SendRequestPlayerProfile(friend.Name, openWindow: true);

        var remove = new Button(panel, "RemoveFriend") { Text = string.Empty };
        remove.SetBounds(190, 6, 27, 25);
        AddLabel(remove, "RemoveCaption", "X", 0, 0, 27, 25, 14,
            new Color(a: 255, r: 248, g: 117, b: 112), true);
        remove.SetToolTipText("Remove friend " + friend.Name);
        remove.Clicked += (_, _) => ConfirmRemoveFriend(friend.Name);
    }

    public void ApplyBlockedUsers(Intersect.Network.Packets.Server.BlockedUsersPacket packet)
    {
        _blockedUsers = packet.Users ?? [];
        RefreshIfShowing(Page.Blocked);
    }

    private void DrawBlocked()
    {
        AddLabel(_body, "BlockedHeading", "BLOCKED USERS",
            0, 8, 462, 33, 16, Gold, true);

        var users = _blockedUsers.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        if (users.Length == 0)
            AddLabel(_body, "NoBlockedUsers", "No blocked characters.", 0, 171, 459, 31, 12, Muted, true);

        for (var i = 0; i < users.Length; i++)
        {
            var entry = users[i];
            var y = 45 + i * 38;
            AdmurinGuiArt.Add(_body, "BlockedIcon" + i, "userlist_2",
                12, y + 4, 23, 22, 32, 0, 16, 16);
            AddLabel(_body, "BlockedName" + i, Shorten(entry.Name, 32),
                45, y, 351, 29, 12, Muted);
            var unblock = new Button(_body, "Unblock" + i) { Text = string.Empty };
            unblock.SetBounds(405, y + 2, 31, 27);
            AddLabel(unblock, "UnblockCaption", "X", 0, 0, 31, 25, 14,
                new Color(a: 255, r: 245, g: 113, b: 105), true);
            unblock.SetToolTipText("Unblock " + entry.Name);
            unblock.Clicked += (_, _) => ConfirmUnblock(entry.Id, entry.Name);
        }

        _body.SetInnerSize(468, Math.Max(426, 56 + users.Length * 38));
        AddAction(_footer, "BlockAnotherPlayer", "BLOCK USER", 167, 9, 149, 46, BlockUser);
    }

    private static void BlockUser()
    {
        _ = new InputBox(
            title: "Block Character",
            prompt: "Enter the character name to block.",
            inputType: InputType.TextInput,
            onSubmit: (control, args) =>
            {
                if (control is not InputBox || args.Value is not StringSubmissionValue value)
                    return;
                var name = value.Value?.Trim();
                if (name is { Length: >= 3 })
                    PacketSender.SendUpdateBlockedUser(true, name);
            });
    }

    private static void ConfirmUnblock(Guid id, string name)
    {
        _ = new InputBox(
            title: "Unblock Character",
            prompt: "Unblock " + name + "?",
            inputType: InputType.YesNo,
            onSubmit: (_, _) => PacketSender.SendUpdateBlockedUser(false, string.Empty, id));
    }

    private void AddFriend()
    {
        _ = new InputBox(
            title: Strings.Friends.AddFriend,
            prompt: Strings.Friends.AddFriendPrompt,
            inputType: InputType.TextInput,
            onSubmit: (control, args) =>
            {
                if (control is not InputBox ||
                    args.Value is not StringSubmissionValue value)
                    return;
                var name = value.Value?.Trim();
                if (name is { Length: >= 3 })
                    PacketSender.SendAddFriend(name);
            });
    }

    private static void ConfirmRemoveFriend(string name)
    {
        _ = new InputBox(
            title: Strings.Friends.RemoveFriend,
            prompt: Strings.Friends.RemoveFriendPrompt.ToString(name),
            inputType: InputType.YesNo,
            onSubmit: (_, _) => PacketSender.SendRemoveFriend(name));
    }

    private static Label AddLabel(Base parent, string name, string text,
        int x, int y, int width, int height, int size, Color color, bool center = false)
    {
        var label = new Label(parent, name)
        {
            Text = text,
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("empire7") ??
                   GameContentManager.Current.GetFont("sourcesansproblack") ?? parent.Skin.DefaultFont,
            FontSize = size,
            TextAlign = center ? Pos.Center : Pos.Left | Pos.CenterV,
            TextColorOverride = color,
            MouseInputEnabled = false,
        };
        label.SetBounds(x, y, width, height);
        return label;
    }

    private static Button AddAction(Base parent, string name, string caption,
        int x, int y, int width, int height, Action? onClick, string? disabledReason = null)
    {
        var button = new Button(parent, name)
        {
            Text = string.Empty,
            IsDisabled = onClick == null,
        };
        button.SetBounds(x, y, width, height);

        var frame = new CopperButton(button, name + "Frame");
        frame.SetBounds(0, 0, width, height);
        AddLabel(button, name + "Label", caption, 5, 3, width - 10, height - 6,
            12, onClick != null ? Cream : Muted, true);
        if (onClick != null)
            button.Clicked += (_, _) => onClick();
        if (!string.IsNullOrEmpty(disabledReason))
            button.SetToolTipText(disabledReason);
        return button;
    }

    private static string Shorten(string? value, int maximum)
    {
        var clean = (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
        return clean.Length <= maximum ? clean : clean[..Math.Max(0, maximum - 3)] + "...";
    }

    private sealed class CopperButton : Base
    {
        public CopperButton(Base parent, string name) : base(parent, name)
        {
            MouseInputEnabled = false;
        }

        protected override void Render(SkinBase skin)
        {
            var r = RenderBounds;
            skin.Renderer.DrawColor = new Color(a: 255, r: 30, g: 19, b: 16);
            skin.Renderer.DrawFilledRect(r);
            skin.Renderer.DrawColor = new Color(a: 255, r: 109, g: 73, b: 60);
            skin.Renderer.DrawFilledRect(new UiRectangle(r.X + 3, r.Y + 3, r.Width - 6, r.Height - 6));
            skin.Renderer.DrawColor = new Color(a: 255, r: 177, g: 126, b: 91);
            skin.Renderer.DrawFilledRect(new UiRectangle(r.X + 6, r.Y + 6, r.Width - 12, 2));
        }
    }

    private sealed class VerticalDivider : Base
    {
        public VerticalDivider(Base parent, string name) : base(parent, name)
        {
            MouseInputEnabled = false;
        }

        protected override void Render(SkinBase skin)
        {
            skin.Renderer.DrawColor = new Color(a: 255, r: 166, g: 134, b: 80);
            skin.Renderer.DrawFilledRect(RenderBounds);
        }
    }
}
