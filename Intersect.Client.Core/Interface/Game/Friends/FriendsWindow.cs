using Intersect.Client.Core;
using Intersect.Client.Framework.Entities;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.Control.EventArguments;
using Intersect.Client.Framework.Gwen.Control.EventArguments.InputSubmissionEvent;
using Intersect.Client.General;
using Intersect.Client.Interface.Shared;
using Intersect.Client.Localization;
using Intersect.Client.Networking;
using UiRectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;
using Label = Intersect.Client.Framework.Gwen.Control.Label;

namespace Intersect.Client.Interface.Game;

/// <summary>
/// Friends roster using the same dark copper / gold presentation as the
/// recent Character Information, Pets and Achievements windows.
/// </summary>
partial class FriendsWindow
{
    private static readonly Color Gold = new(a: 255, r: 235, g: 202, b: 144);
    private static readonly Color Cream = new(a: 255, r: 241, g: 225, b: 204);
    private static readonly Color Muted = new(a: 255, r: 183, g: 173, b: 159);

    private readonly Base mParent;
    private WindowControl mWindow = null!;
    private Button mAdd = null!;
    private ScrollControl mFriendContainer = null!;
    private ImagePanel mFriendListAnchor = null!;
    private List<FriendsRow> mRows = [];
    private readonly List<Label> _sectionHeaders = [];
    private Label _summary = null!;
    private Label _hint = null!;
    private Label _empty = null!;

    public FriendsWindow(Canvas gameCanvas)
    {
        mParent = gameCanvas;
        GenerateControls();

        // Load the established in-game skin, then apply a stable, readable
        // geometry independent of legacy JSON row positions.
        mWindow.LoadJsonUi(GameContentManager.UI.InGame, Graphics.Renderer.GetResolutionString());
        ConfigureLayout();
        UpdateList();
    }

    private void GenerateControls()
    {
        mWindow = new WindowControl(mParent, Strings.Friends.Title, false, "FriendsWindow");
        mAdd = new Button(mWindow, "AddFriendButton");
        mFriendContainer = new ScrollControl(mWindow, "FriendContainer");
        mFriendListAnchor = new ImagePanel(mFriendContainer, "FriendListAnchor");

        mWindow.DisableResizing();
        mFriendContainer.EnableScroll(false, true);
        mAdd.Clicked += addButton_Clicked;
    }

    private void ConfigureLayout()
    {
        mWindow.SetSize(700, 600);
        mWindow.Alignment = [Alignments.Center];
        mWindow.TitleLabel.Font = GameContentManager.Current.GetFont("sourcesansproblack")
            ?? mWindow.Skin.DefaultFont;
        mWindow.TitleLabel.FontSize = 13;

        var headerFrame = new FriendsHeaderChrome(mWindow, "FriendsRosterHeader");
        headerFrame.SetBounds(21, 37, 655, 127);
        AdmurinGuiArt.Add(mWindow, "AdmurinUserListPanel", "userlist_0", 21, 174, 655, 396);
        headerFrame.SendToBack();

        var heading = AddText(mWindow, "FriendsRosterHeading", "FRIENDS",
            40, 47, 415, 34, 19, Gold, bold: true);
        heading.TextAlign = Pos.Left | Pos.CenterV;

        _summary = AddText(mWindow, "FriendsRosterSummary", string.Empty,
            40, 86, 430, 22, 11, Cream, bold: true);
        _hint = AddText(mWindow, "FriendsRosterHint",
            "Select PROFILE to inspect an online friend",
            40, 114, 439, 21, 9, Muted);

        mAdd.SetBounds(496, 70, 161, 42);
        mAdd.Text = string.Empty;
        mAdd.SetToolTipText(Intersect.Client.Localization.ClientLanguages.Text("Send a friend request"));
        var addFrame = new FriendsAddButtonChrome(mAdd, "FriendsAddButtonFrame");
        addFrame.SetBounds(0, 0, 161, 42);
        addFrame.SendToBack();

        var addCaption = AddText(mAdd, "FriendsAddCaption", "ADD FRIEND",
            5, 0, 151, 42, 11, Gold, bold: true);
        addCaption.TextAlign = Pos.Center;
        addCaption.BringToFront();

        mFriendContainer.SetBounds(25, 174, 648, 396);
        mFriendListAnchor.Hide();

        _empty = AddText(mFriendContainer, "FriendsRosterEmpty",
            "Your friends list is empty. Use ADD FRIEND to invite someone.",
            25, 105, 577, 55, 11, Muted);
        _empty.TextAlign = Pos.Center;
        _empty.Hide();
    }

    public bool IsVisible => !mWindow.IsHidden;

    public void Show()
    {
        UpdateList();
        mWindow.Show();
        mWindow.BringToFront();
    }

    public void Hide() => mWindow.Hide();

    public void Update()
    {
        if (!IsVisible)
            ClearList();
    }

    private void ClearList()
    {
        foreach (var row in mRows)
            row.Dispose();
        mRows.Clear();

        foreach (var header in _sectionHeaders)
            mFriendContainer.RemoveChild(header, true);
        _sectionHeaders.Clear();
    }

    public void UpdateList()
    {
        ClearList();

        var friends = Globals.Me?.Friends;
        IFriendInstance[] online = friends == null
            ? []
            : friends.Where(friend => friend is { Online: true })
                .OrderBy(friend => friend.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        IFriendInstance[] offline = friends == null
            ? []
            : friends.Where(friend => friend is { Online: false })
                .OrderBy(friend => friend.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

        _summary.Text = $"{online.Length} ONLINE    {offline.Length} OFFLINE" +
            $"    {online.Length + offline.Length} TOTAL";

        var y = 7;
        y = AddSection("ONLINE FRIENDS", online, y);
        y = AddSection("OFFLINE FRIENDS", offline, y);

        var isEmpty = online.Length == 0 && offline.Length == 0;
        if (isEmpty)
            _empty.Show();
        else
            _empty.Hide();

        mFriendContainer.SetInnerSize(623, Math.Max(388, y + 18));
        mFriendContainer.UpdateScrollBars();
    }

    private int AddSection(string title,
        IReadOnlyList<IFriendInstance> friends, int y)
    {
        if (friends.Count == 0)
            return y;

        var header = AddText(mFriendContainer, "FriendsGroupHeader" + title.Replace(" ", ""),
            $"{title}  ({friends.Count})", 14, y, 577, 24, 11, Gold, bold: true);
        _sectionHeaders.Add(header);
        y += 30;

        foreach (var friend in friends)
        {
            var row = new FriendsRow(
                mFriendContainer, friend.Name, friend.Map ?? string.Empty, friend.Online);
            row.SetPosition(8, y);
            mRows.Add(row);
            y += FriendsRow.RowHeight + 9;
        }

        return y + 13;
    }

    private static Label AddText(Base parent, string id, string value,
        int x, int y, int width, int height, int fontSize, Color color,
        bool bold = false)
    {
        var label = new Label(parent, id)
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont(
                bold ? "sourcesansproblack" : "sourcesanspro") ?? parent.Skin.DefaultFont,
            FontSize = fontSize,
            TextColorOverride = color,
            TextAlign = Pos.Left | Pos.CenterV,
            Text = value,
            MouseInputEnabled = false,
        };
        label.SetBounds(x, y, width, height);
        return label;
    }

    private void addButton_Clicked(Base sender, MouseButtonState arguments)
    {
        _ = new InputBox(
            title: Strings.Friends.AddFriend,
            prompt: Strings.Friends.AddFriendPrompt,
            inputType: InputType.TextInput,
            onSubmit: (control, args) =>
            {
                if (control is not InputBox ||
                    args.Value is not StringSubmissionValue submissionValue)
                    return;

                var value = submissionValue.Value?.Trim();
                if (value is not { Length: >= 3 })
                    return;

                PacketSender.SendAddFriend(value);
            }
        );
    }

    private sealed class FriendsHeaderChrome : Base
    {
        public FriendsHeaderChrome(Base parent, string name) : base(parent, name)
        {
            MouseInputEnabled = false;
        }

        protected override void Render(SkinBase skin)
        {
            var r = RenderBounds;
            var border = new Color(a: 255, r: 151, g: 110, b: 64);
            Fill(skin, r.X, r.Y, r.Width, r.Height,
                new Color(a: 255, r: 49, g: 32, b: 27));
            Fill(skin, r.X, r.Y, r.Width, 2, border);
            Fill(skin, r.X, r.Bottom - 2, r.Width, 2, border);
            Fill(skin, r.X, r.Y, 2, r.Height, border);
            Fill(skin, r.Right - 2, r.Y, 2, r.Height, border);
        }
    }

    private sealed class FriendsAddButtonChrome : Base
    {
        public FriendsAddButtonChrome(Base parent, string name) : base(parent, name)
        {
            MouseInputEnabled = false;
        }

        protected override void Render(SkinBase skin)
        {
            var r = RenderBounds;
            var border = new Color(a: 255, r: 220, g: 173, b: 91);
            Fill(skin, r.X, r.Y, r.Width, r.Height,
                new Color(a: 255, r: 79, g: 53, b: 32));
            Fill(skin, r.X, r.Y, r.Width, 2, border);
            Fill(skin, r.X, r.Bottom - 2, r.Width, 2, border);
            Fill(skin, r.X, r.Y, 2, r.Height, border);
            Fill(skin, r.Right - 2, r.Y, 2, r.Height, border);
        }
    }

    private static void Fill(SkinBase skin, int x, int y,
        int width, int height, Color color)
    {
        if (width <= 0 || height <= 0)
            return;

        skin.Renderer.DrawColor = color;
        skin.Renderer.DrawFilledRect(new UiRectangle(x, y, width, height));
    }
}
