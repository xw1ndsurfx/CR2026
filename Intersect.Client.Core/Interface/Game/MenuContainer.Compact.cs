using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.Control.EventArguments;
using Intersect.Client.Localization;

namespace Intersect.Client.Interface.Game;

public partial class MenuContainer
{
    private const int RowHeight = 27, FrameInset = 4, FooterHeight = 40;
    private ScrollControl? _compactMenuList;
    private bool _menuExpanded = true;
    private MenuRow? _houseMenuRow;
    private int _compactCanvasHeight, _compactCanvasWidth;
    private string _compactLanguage = "";
    private readonly List<MenuRow> _compactRows = [];

    private void ApplyCorpsRoyauxVerticalMenuLayout()
    {
        Padding = Padding.Zero;
        Dock = Pos.None;
        Alignment = [Alignments.Bottom, Alignments.Left];
        AlignmentPadding = new Padding { Left = 0, Bottom = FooterHeight };
        ShouldCacheToTexture = false;
        _compactMenuList = new ScrollControl(this, "CompactMenuEntries")
        {
            OverflowX = OverflowBehavior.Hidden, OverflowY = OverflowBehavior.Scroll,
            AutoHideBars = true,
        };
        var houseContainer = new ImagePanel(this, "MyHouseButtonContainer");
        var houseButton = new Button(houseContainer, "MyHouseMenuIcon", disableText: true);
        var containers = new[] { _inventoryButtonContainer, _spellsButtonContainer, _characterButtonContainer,
            _questsButtonContainer, _dungeonsButtonContainer, _newsButtonContainer, _dailyRewardButtonContainer,
            _logiCoinShopButtonContainer, _friendsButtonContainer, _partyButtonContainer, _guildButtonContainer, houseContainer, _escapeMenuButtonContainer };
        var buttons = new[] { _inventoryButton, _spellsButton, _characterButton, _questsButton, _dungeonsButton,
            _newsButton, _dailyRewardButton, _logiCoinShopButton, _friendsButton, _partyButton, _guildButton, houseButton, _escapeMenuButton };
        // Full labels remain in tooltips; short localized labels keep the list readable.
        var captions = new[] { "Inventory", "Spell Book", "Character", "Quest Log", "Dungeons", "News",
            "Daily Reward", "LogiCoin Shop", "Friends", "Party", "Guild", "My house", "Open Menu" };
        Base.GwenEventHandler<MouseButtonState>[] actions = [InventoryButton_Clicked, SpellsButton_Clicked,
            CharacterButton_Clicked, QuestBtn_Clicked, DungeonsButton_Clicked, NewsButton_Clicked,
            DailyRewardButton_Clicked, LogiCoinShopButton_Clicked, FriendsBtn_Clicked, PartyBtn_Clicked, GuildBtn_Clicked, (_, _) => Interface.InGameUiOrNull?.OpenHouseFromMenu(), EscapeMenuButtonClicked];
        for (var i = 0; i < containers.Length; i++)
        {
            var container = containers[i];
            container.Parent = _compactMenuList;
            container.Dock = Pos.None; container.Alignment = [];
            container.Margin = Margin.Zero; container.Padding = Padding.Zero;
            container.MinimumSize = Point.Empty; container.MaximumSize = new Point(1000, RowHeight);
            container.TextureFilename = null;
            container.SetBounds(0, i * RowHeight, 160, RowHeight);
            var row = new MenuRow(container, "MenuEntry" + i, buttons[i], captions[i]);
            row.SetBounds(0, 0, container.Width, RowHeight); row.Clicked += actions[i];
            _compactRows.Add(row);
            if (captions[i] == "My house") _houseMenuRow = row;
            var icon = buttons[i];
            icon.Parent = row; icon.Alignment = []; icon.Dock = Pos.None; icon.Margin = Margin.Zero;
            icon.MinimumSize = Point.Empty; icon.MaximumSize = new Point(24, 24);
            var wideIcon = i is >= 4 and <= 7;
            icon.SetBounds(2, wideIcon ? 4 : 1, 24, wideIcon ? 19 : 24);
            row.ConfigureIcon();
        }
        // Only the show/hide control stays outside the list, flush with the screen corner.
        _menuToggleButtonContainer.Dock = Pos.None;
        _menuToggleButtonContainer.Alignment = [Alignments.Bottom, Alignments.Left];
        _menuToggleButtonContainer.AlignmentPadding = new Padding { Left = 0, Bottom = 0 };
        _menuToggleButtonContainer.Margin = Margin.Zero;
        _menuToggleButton.SetToolTipText(ClientLanguages.Text("Hide menu"));
        UpdateCompactMenuLayout();
    }

    internal void CollapseForWorldClick() => SetMenuExpanded(false);

    private void SetMenuExpanded(bool expanded)
    {
        if (_menuExpanded == expanded) return;
        _menuExpanded = expanded;
        IsHidden = !expanded;
        _menuToggleButton.SetToolTipText(ClientLanguages.Text(expanded ? "Hide menu" : "Show menu"));
    }

    private void UpdateCompactMenuLayout()
    {
        if (_compactMenuList == null || Parent == null) return;
        foreach (var row in _compactRows) row.RefreshCaption();
        if (_houseMenuRow != null) _houseMenuRow.IsDisabled = Interface.InGameUiOrNull?.CanOpenHouse != true;
        if (_compactCanvasHeight == Parent.Height && _compactCanvasWidth == Parent.Width && _compactLanguage == ClientLanguages.Current) return;
        _compactCanvasHeight = Parent.Height; _compactCanvasWidth = Parent.Width; _compactLanguage = ClientLanguages.Current;
        var naturalHeight = _compactRows.Count * RowHeight + FrameInset * 2;
        var height = Math.Min(naturalHeight, Math.Max(RowHeight + FrameInset * 2, Parent.Height - FooterHeight - 84));
        var scrollbarWidth = height < naturalHeight ? 16 : 0;
        var captionWidth = _compactRows.Max(row => row.CaptionWidth);
        var width = Math.Min(Math.Max(80, Parent.Width - 8), Math.Max(110, captionWidth + 33 + 8 + FrameInset * 2 + scrollbarWidth));
        SetSize(width, height);
        _compactMenuList.SetBounds(FrameInset, FrameInset, width - FrameInset * 2, height - FrameInset * 2);
        for (var i = 0; i < _compactRows.Count; i++)
        {
            var rowWidth = width - FrameInset * 2 - scrollbarWidth;
            _compactRows[i].Parent!.SetBounds(0, i * RowHeight, rowWidth, RowHeight);
            _compactRows[i].SetSize(rowWidth, RowHeight);
        }
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
        private readonly MenuGlyph _glyph;
        internal int CaptionWidth => Skin.Renderer.MeasureText(Font, FontSize, Text).X;
        private string _language = "";
        internal MenuRow(Base parent, string name, Button icon, string caption) : base(parent, name)
        {
            _icon = icon; _caption = caption;
            _glyph = new MenuGlyph(this, caption); _glyph.SetBounds(2, 1, 24, 24);
            Font = GameContentManager.Current.GetFont("empire7") ?? GameContentManager.Current.GetFont("sourcesanspro") ?? parent.Skin.DefaultFont;
            FontSize = 10; AutoSizeToContents = false;
            TextAlign = Pos.Left | Pos.CenterV;
            Padding = new Padding { Left = 33, Right = 4 };
            SetTextColor(Color.White, ComponentState.Normal);
            SetTextColor(new Color(255, 255, 225, 174), ComponentState.Hovered);
            RefreshCaption();
        }
        internal void ConfigureIcon()
        {
            // Legacy Btn_* images are framed action buttons, not compact menu icons.
            var builtIn = _caption is "Dungeons" or "News" or "Daily Reward" or "LogiCoin Shop" || _icon.GetStateTexture(ComponentState.Normal) == null;
            _icon.IsHidden = builtIn; _glyph.IsHidden = !builtIn;
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
