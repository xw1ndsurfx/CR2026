using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.Control.EventArguments;
using Intersect.Client.Localization;

namespace Intersect.Client.Interface.Game;

public partial class MenuContainer
{
    private const int MenuWidth = 204, RowHeight = 27, FrameInset = 4, FooterHeight = 50;
    private ScrollControl? _compactMenuList;
    private bool _menuExpanded = true;
    private int _compactCanvasHeight;
    private readonly List<MenuRow> _compactRows = [];

    private void ApplyCorpsRoyauxVerticalMenuLayout()
    {
        Padding = Padding.Zero;
        Dock = Pos.None;
        Alignment = [Alignments.Bottom, Alignments.Left];
        AlignmentPadding = new Padding { Left = 4, Bottom = FooterHeight + 4 };
        ShouldCacheToTexture = false;
        _compactMenuList = new ScrollControl(this, "CompactMenuEntries")
        {
            OverflowX = OverflowBehavior.Hidden, OverflowY = OverflowBehavior.Scroll,
            AutoHideBars = true,
        };
        var containers = new[] { _inventoryButtonContainer, _spellsButtonContainer, _characterButtonContainer,
            _questsButtonContainer, _dungeonsButtonContainer, _newsButtonContainer, _dailyRewardButtonContainer,
            _logiCoinShopButtonContainer, _friendsButtonContainer, _partyButtonContainer, _guildButtonContainer };
        var buttons = new[] { _inventoryButton, _spellsButton, _characterButton, _questsButton, _dungeonsButton,
            _newsButton, _dailyRewardButton, _logiCoinShopButton, _friendsButton, _partyButton, _guildButton };
        // Full labels remain in tooltips; short localized labels keep the list readable.
        var captions = new[] { "Inventory", "Spell Book", "Character", "Quest Log", "Dungeons", "News",
            "Daily Reward", "LogiCoin Shop", "Friends", "Party", "Guild" };
        Base.GwenEventHandler<MouseButtonState>[] actions = [InventoryButton_Clicked, SpellsButton_Clicked,
            CharacterButton_Clicked, QuestBtn_Clicked, DungeonsButton_Clicked, NewsButton_Clicked,
            DailyRewardButton_Clicked, LogiCoinShopButton_Clicked, FriendsBtn_Clicked, PartyBtn_Clicked, GuildBtn_Clicked];
        for (var i = 0; i < containers.Length; i++)
        {
            var container = containers[i];
            container.Parent = _compactMenuList;
            container.Dock = Pos.None; container.Alignment = [];
            container.Margin = Margin.Zero; container.Padding = Padding.Zero;
            container.MinimumSize = Point.Empty; container.MaximumSize = new Point(MenuWidth, RowHeight);
            container.TextureFilename = null;
            container.SetBounds(0, i * RowHeight, MenuWidth - FrameInset * 2 - 16, RowHeight);
            var row = new MenuRow(container, "MenuEntry" + i, buttons[i], captions[i]);
            row.SetBounds(0, 0, container.Width, RowHeight); row.Clicked += actions[i];
            _compactRows.Add(row);
            var icon = buttons[i];
            icon.Parent = row; icon.Alignment = []; icon.Dock = Pos.None; icon.Margin = Margin.Zero;
            icon.MinimumSize = Point.Empty; icon.MaximumSize = new Point(24, 24);
            var wideIcon = i is >= 4 and <= 7;
            icon.SetBounds(2, wideIcon ? 4 : 1, 24, wideIcon ? 19 : 24);
        }
        // The settings button anchors the list; the independent toggle stays reachable when collapsed.
        _escapeMenuButtonContainer.Parent = Parent;
        _escapeMenuButtonContainer.Dock = Pos.None;
        _escapeMenuButtonContainer.Alignment = [Alignments.Bottom, Alignments.Left];
        _escapeMenuButtonContainer.AlignmentPadding = new Padding { Left = 4, Bottom = 4 };
        _escapeMenuButtonContainer.MinimumSize = new Point(44, 44);
        _escapeMenuButtonContainer.MaximumSize = new Point(44, 44);
        _escapeMenuButtonContainer.SetSize(44, 44);
        _escapeMenuButton.Alignment = [Alignments.Center]; _escapeMenuButton.SetSize(32, 32);
        _menuToggleButtonContainer.Dock = Pos.None;
        _menuToggleButtonContainer.Alignment = [Alignments.Bottom, Alignments.Left];
        _menuToggleButtonContainer.AlignmentPadding = new Padding { Left = 54, Bottom = 4 };
        _menuToggleButtonContainer.Margin = Margin.Zero;
        _menuToggleButton.SetToolTipText(ClientLanguages.Text("Hide menu"));
        UpdateCompactMenuLayout();
    }

    private void UpdateCompactMenuLayout()
    {
        if (_compactMenuList == null || Parent == null) return;
        // Scroll on short viewports instead of shrinking or clipping the icons and captions.
        if (_compactCanvasHeight != Parent.Height)
        {
            _compactCanvasHeight = Parent.Height;
            var height = Math.Min(_compactRows.Count * RowHeight + FrameInset * 2, Math.Max(RowHeight + FrameInset * 2, Parent.Height - FooterHeight - 84));
            SetSize(MenuWidth, height);
            _compactMenuList.SetBounds(FrameInset, FrameInset, MenuWidth - FrameInset * 2, height - FrameInset * 2);
        }
        foreach (var row in _compactRows) row.RefreshCaption();
    }

    protected override void Render(Intersect.Client.Framework.Gwen.Skin.Base skin)
    {
        var bounds = RenderBounds;
        skin.DrawRectFill(bounds, new Color(255, 58, 32, 33));
        var edge = new Color(255, 190, 133, 99);
        skin.DrawRectFill(bounds with { Height = 2 }, edge);
        skin.DrawRectFill(bounds with { Y = bounds.Bottom - 2, Height = 2 }, edge);
        skin.DrawRectFill(bounds with { Width = 2 }, edge);
        skin.DrawRectFill(bounds with { X = bounds.Right - 2, Width = 2 }, edge);
    }

    private sealed class MenuRow : Button
    {
        private readonly Button _icon;
        private readonly string _caption;
        private string _language = "";
        internal MenuRow(Base parent, string name, Button icon, string caption) : base(parent, name)
        {
            _icon = icon; _caption = caption;
            Font = GameContentManager.Current.GetFont("empire7") ?? GameContentManager.Current.GetFont("sourcesanspro") ?? parent.Skin.DefaultFont;
            FontSize = 10; AutoSizeToContents = false;
            TextAlign = Pos.Left | Pos.CenterV;
            Padding = new Padding { Left = 33, Right = 4 };
            SetTextColor(Color.White, ComponentState.Normal);
            SetTextColor(new Color(255, 255, 225, 174), ComponentState.Hovered);
            RefreshCaption();
        }
        internal void RefreshCaption()
        {
            if (_language == ClientLanguages.Current) return;
            _language = ClientLanguages.Current;
            Text = ClientLanguages.Text(_caption); SetToolTipText(Text);
        }
        protected override void Render(Intersect.Client.Framework.Gwen.Skin.Base skin)
        {
            // Text and the existing icon render as children, without an individual framed button.
            if (IsHovered || _icon.IsHovered || IsActive)
            {
                skin.DrawRectFill(RenderBounds, new Color(255, 104, 64, 51));
                skin.DrawRectFill(RenderBounds with { Width = 2 }, new Color(255, 224, 170, 113));
            }
        }
    }
}
