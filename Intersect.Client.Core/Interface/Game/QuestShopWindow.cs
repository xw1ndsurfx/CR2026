using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Network.Packets.Server;
using Chrome = Intersect.Client.Interface.Game.MarketplaceWindow.MarketChrome;
using Style = Intersect.Client.Interface.Game.MarketplaceWindow.FrameStyle;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;
namespace Intersect.Client.Interface.Game;

internal sealed class QuestShopWindow : Window
{
    private static readonly Color Gold = new(235, 204, 140), Cream = new(244, 231, 204), Muted = new(190, 175, 155);
    private readonly Chrome _hero, _sidebar, _listFrame, _detailFrame;
    private readonly Label _heading, _shopName, _description, _message, _count, _categoryHeading, _listHeading, _detailHeading;
    private readonly TextBox _search;
    private readonly ComboBox _filter, _categorySelect;
    private readonly ScrollControl _categories, _quests, _details;
    private readonly RichLabel _detailText;
    private readonly Button _openOffer, _clear, _back;
    private QuestShopStatePacket _state = new();
    private Guid _selected;
    private string _category = "", _filterValue = "ALL";
    private bool _ready, _showDetail;

    public QuestShopWindow(Canvas parent) : base(parent, ClientLanguages.Text("Quest Shop"), false, nameof(QuestShopWindow))
    {
        SetSize(1040, 730); Alignment = [Alignments.Center]; DeleteOnClose = false; DisableResizing();
        _hero = new Chrome(this, "QuestShopHero", Style.Ribbon);
        _sidebar = new Chrome(this, "QuestShopCategoriesFrame", Style.Section);
        _listFrame = new Chrome(this, "QuestShopCatalogFrame", Style.Section);
        _detailFrame = new Chrome(this, "QuestShopDetailsFrame", Style.Section);
        _heading = Label(this, "QuestExchangeHeading", ClientLanguages.Text("QUEST EXCHANGE"), 11, Gold);
        _shopName = Label(this, "QuestShopName", "", 20, Cream);
        _description = Label(this, "QuestShopDescription", "", 10, Muted);
        _message = Label(this, "QuestShopMessage", "", 10, Gold);
        _count = Label(this, "QuestShopCount", "", 10, Muted);
        _categoryHeading = Label(this, "QuestShopCategoryHeading", ClientLanguages.Text("CATEGORIES"), 11, Gold);
        _listHeading = Label(this, "QuestShopListHeading", ClientLanguages.Text("QUESTS"), 12, Gold);
        _detailHeading = Label(this, "QuestShopDetailHeading", ClientLanguages.Text("QUEST DETAILS"), 12, Gold);
        _search = new TextBox(this, "QuestShopSearch")
        {
            Text = "", PlaceholderText = ClientLanguages.Text("Search quests..."), Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont, FontSize = 11
        };
        _filter = Combo("QuestShopStatusFilter"); _categorySelect = Combo("QuestShopCategoryFilter");
        foreach (var (value, caption) in new[] { ("ALL", "All statuses"), ("AVAILABLE", "AVAILABLE"), ("IN PROGRESS", "IN PROGRESS"), ("COMPLETED", "COMPLETED"), ("LOCKED", "LOCKED"), ("REPEATABLE", "Repeatable") })
            _filter.AddItem(ClientLanguages.Text(caption), userData: value);
        _categories = Scroll("QuestShopCategories"); _quests = Scroll("QuestShopCatalog"); _details = Scroll("QuestShopDetails");
        _detailText = EventPanelText.CreateBody(_details, "QuestShopDetailText", 11);
        _openOffer = Action("QuestShopView", "VIEW QUEST", () =>
        {
            var entry = _state.Entries.FirstOrDefault(e => e.QuestId == _selected);
            if (entry is not { CanAccept: true }) return;
            _openOffer.IsDisabled = true; _openOffer.Text = ClientLanguages.Text("OPENING...");
            Networking.PacketSender.SendRequestQuestShopOffer(_state.ShopId, entry.QuestId);
        });
        _clear = Action("QuestShopClear", "Reset filters", () =>
        {
            _showDetail = false; _category = ""; _filterValue = "ALL"; _filter.Text = ClientLanguages.Text("All statuses");
            _categorySelect.Text = ClientLanguages.Text("All categories"); _search.Text = ""; Arrange(); BuildCategories(); RenderQuests();
        });
        _back = Action("QuestShopBack", "Back to quests", () => { _showDetail = false; Arrange(); RenderQuests(); });
        _search.TextChanged += (_, _) => { if (_ready) { _showDetail = false; Arrange(); RenderQuests(); _quests.ScrollToTop(); } };
        _filter.ItemSelected += (_, e) => { if (e.Automated) return; _filterValue = e.SelectedUserData as string ?? "ALL"; _showDetail = false; Arrange(); RenderQuests(); _quests.ScrollToTop(); };
        _categorySelect.ItemSelected += (_, e) => { if (e.Automated) return; _category = e.SelectedUserData as string ?? ""; _showDetail = false; Arrange(); BuildCategories(); RenderQuests(); _quests.ScrollToTop(); };
        _ready = true; Arrange(); BuildCategories(); RenderQuests(); Hide();
    }
    protected override void EnsureInitialized() { }
    private Label Label(Base parent, string id, string text, int size, Color color) => new(parent, id)
    {
        Text = text, Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont, FontSize = size,
        AutoSizeToContents = false, TextColorOverride = color, MouseInputEnabled = false, TextAlign = Pos.Left | Pos.CenterV
    };
    private ComboBox Combo(string id) => new(this, id) { Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont, FontSize = 10, AutoSizeToContents = false };
    private ScrollControl Scroll(string id) => new(this, id) { OverflowX = OverflowBehavior.Hidden, OverflowY = OverflowBehavior.Scroll, AutoHideBars = false, ShouldDrawBackground = false };
    private Button Action(string id, string text, Action click)
    {
        var button = new Button(this, id) { Text = ClientLanguages.Text(text), Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont, FontSize = 10 };
        button.Clicked += (_, _) => click(); return button;
    }
    protected override void Layout(SkinBase skin)
    {
        var width = Math.Max(320, Math.Min(1040, (Parent?.Width ?? 1064) - 24));
        var height = Math.Max(420, Math.Min(730, (Parent?.Height ?? 754) - 24));
        if (_ready && (Width != width || Height != height)) { SetSize(width, height); Arrange(); BuildCategories(); RenderQuests(); }
        base.Layout(skin);
    }
    private void Arrange()
    {
        var w = Width; var h = Height - Titlebar.Height - 8; var wide = w >= 920;
        _hero.SetBounds(22, 36, w - 44, 108); _heading.SetBounds(42, 42, w - 84, 22);
        _shopName.SetBounds(42, 67, w - 84, 35); _description.SetBounds(42, 108, w - 84, 24);
        _search.SetBounds(24, 158, w - 248, 32); _filter.SetBounds(w - 214, 158, 190, 32);
        _categorySelect.SetBounds(24, 198, Math.Max(120, w - 190), 30); _categorySelect.IsHidden = wide;
        _clear.SetBounds(w - 154, 198, 130, 30);
        var top = 242; var bodyHeight = Math.Max(130, h - top - 58);
        _sidebar.IsHidden = _categories.IsHidden = _categoryHeading.IsHidden = !wide;
        _sidebar.SetBounds(24, top, 164, bodyHeight); _categoryHeading.SetBounds(36, top + 12, 140, 24);
        _categories.SetBounds(32, top + 52, 148, Math.Max(30, bodyHeight - 62));
        var listX = wide ? 200 : 24; var listWidth = wide ? w - listX - 336 : w - 48;
        var listHeight = bodyHeight;
        var detailX = wide ? w - 324 : 24; var detailY = top;
        var detailWidth = wide ? 300 : w - 48; var detailHeight = bodyHeight;
        _listFrame.SetBounds(listX, top, listWidth, listHeight); _listHeading.SetBounds(listX + 14, top + 12, listWidth - 28, 24);
        _quests.SetBounds(listX + 8, top + 52, listWidth - 16, Math.Max(20, listHeight - 62));
        _detailFrame.SetBounds(detailX, detailY, detailWidth, detailHeight); _detailHeading.SetBounds(detailX + 12, detailY + 12, detailWidth - 24, 24);
        _details.SetBounds(detailX + 10, detailY + 50, detailWidth - 20, Math.Max(18, detailHeight - 102));
        _openOffer.SetBounds(detailX + 12, detailY + detailHeight - 43, detailWidth - 24, 32);
        _back.SetBounds(detailX + detailWidth - 150, detailY + 9, 136, 30); _back.IsHidden = wide || !_showDetail;
        _listFrame.IsHidden = _listHeading.IsHidden = _quests.IsHidden = !wide && _showDetail;
        _detailFrame.IsHidden = _detailHeading.IsHidden = _details.IsHidden = _openOffer.IsHidden = !wide && !_showDetail;
        if (!wide) _detailHeading.Width = Math.Max(40, detailWidth - 170);
        _count.SetBounds(24, h - 48, w - 48, 20); _message.SetBounds(24, h - 26, w - 48, 20);
        EventPanelText.Fit(_shopName, ClientLanguages.ContentText(_state.ShopName), 20);
        EventPanelText.Fit(_description, ClientLanguages.ContentText(_state.Description), 10);
    }
    public void Apply(QuestShopStatePacket state)
    {
        if (_state.ShopId != state.ShopId) { _category = ""; _filterValue = "ALL"; _showDetail = false; _selected = Guid.Empty; _search.Text = ""; _filter.Text = ClientLanguages.Text("All statuses"); }
        _state = state; _state.Entries ??= [];
        Title = string.IsNullOrWhiteSpace(state.ShopName) ? ClientLanguages.Text("Quest Shop") : ClientLanguages.ContentText(state.ShopName);
        EventPanelText.Fit(_message, ClientLanguages.Expanded(state.Message ?? ""), 10);
        Arrange(); BuildCategories(); RenderQuests();
    }
    private static string Category(QuestShopEntryPacket entry) => string.IsNullOrWhiteSpace(entry.Category) ? "General" : entry.Category;
    private static string CategoryText(string category) => category == "General" ? ClientLanguages.Text(category) : ClientLanguages.ContentText(category);
    private void BuildCategories()
    {
        _categories.DeleteAll(); _categorySelect.ClearItems();
        var groups = _state.Entries.GroupBy(Category).OrderBy(g => g.Key).Select(g => (Category: g.Key, Count: g.Count())).ToArray();
        if (_category.Length > 0 && !groups.Any(g => g.Category == _category)) _category = "";
        var options = new[] { (Category: "", Count: _state.Entries.Length) }.Concat(groups);
        var y = 3;
        foreach (var option in options)
        {
            var key = option.Category; var text = key.Length == 0 ? ClientLanguages.Text("All categories") : CategoryText(key);
            _categorySelect.AddItem(text, userData: key);
            var button = new Button(_categories, "QuestCategory_" + y) { Text = "", ShouldDrawBackground = false };
            button.SetBounds(0, y, 126, 46);
            var frame = new Chrome(button, "CategoryFrame", key == _category ? Style.SelectedTab : Style.Tab); frame.SetBounds(0, 0, 126, 46);
            var caption = Label(button, "CategoryCaption", text, 10, Cream); caption.SetBounds(8, 3, 110, 21); EventPanelText.Fit(caption, text, 10);
            var amount = Label(button, "CategoryCount", ClientLanguages.Format("{0} quests", option.Count), 9, Muted); amount.SetBounds(8, 24, 110, 18);
            button.Clicked += (_, _) => { _category = key; BuildCategories(); RenderQuests(); _quests.ScrollToTop(); };
            y += 52;
        }
        _categorySelect.Text = _category.Length == 0 ? ClientLanguages.Text("All categories") : CategoryText(_category);
        _categories.SetInnerSize(126, y); _categories.UpdateScrollBars();
    }
    internal static bool Matches(QuestShopEntryPacket entry, string query, string category, string filter)
    {
        if (category.Length > 0 && Category(entry) != category) return false;
        if (filter == "REPEATABLE" ? !entry.Repeatable : filter != "ALL" && entry.Status != filter) return false;
        query = query.Trim();
        return query.Length == 0 || new[] { entry.Name, entry.Description, entry.Requirements, Category(entry) }
            .Any(s => (s ?? "").Contains(query, StringComparison.OrdinalIgnoreCase) || ClientLanguages.ContentText(s ?? "").Contains(query, StringComparison.OrdinalIgnoreCase));
    }
    private void RenderQuests()
    {
        var scroll = _quests.VerticalScrollBar.ScrollAmount; _quests.DeleteAll();
        var entries = _state.Entries.Where(e => Matches(e, _search.Text ?? "", _category, _filterValue)).ToArray();
        if (!entries.Any(e => e.QuestId == _selected)) _selected = entries.FirstOrDefault()?.QuestId ?? Guid.Empty;
        var y = 4; var width = Math.Max(100, _quests.Width - 26);
        foreach (var entry in entries)
        {
            var row = new Button(_quests, "Quest_" + entry.QuestId.ToString("N")) { Text = "", ShouldDrawBackground = false }; row.SetBounds(2, y, width, 100);
            var frame = new Chrome(row, "QuestCard", entry.QuestId == _selected ? Style.SelectedTab : Style.Card); frame.SetBounds(0, 0, width, 100);
            var icon = new Chrome(row, "QuestSeal", Style.Preview); icon.SetBounds(13, 18, 48, 58);
            var seal = Label(row, "QuestSealLetter", "Q", 24, Gold); seal.SetBounds(23, 25, 30, 42);
            var name = Label(row, "QuestName", "", 12, Cream); name.SetBounds(74, 10, width - 86, 28); EventPanelText.Fit(name, ClientLanguages.ContentText(entry.Name), 12);
            var category = Label(row, "QuestCategory", "", 10, Muted); category.SetBounds(74, 40, width - 86, 23); EventPanelText.Fit(category, CategoryText(Category(entry)), 10);
            var status = Label(row, "QuestStatus", ClientLanguages.Text(entry.Status), 10, entry.CanAccept ? new Color(150, 225, 160) : Gold); status.SetBounds(74, 69, width - 86, 21);
            EventPanelText.Fit(status, ClientLanguages.Text(entry.Status) + (entry.Repeatable ? " | " + ClientLanguages.Text("Repeatable") : ""), 10);
            row.Clicked += (_, _) => { _selected = entry.QuestId; _showDetail = true; Arrange(); RenderQuests(); };
            y += 108;
        }
        if (entries.Length == 0)
        {
            var empty = Label(_quests, "NoMatchingQuests", ClientLanguages.Text("No quests match your filters."), 10, Muted); empty.SetBounds(10, 12, width - 16, 50);
            EventPanelText.Fit(empty, empty.Text, 10);
        }
        _quests.SetInnerSize(width + 4, y); _quests.UpdateScrollBars(); _quests.VerticalScrollBar.ScrollAmount = scroll;
        _count.Text = ClientLanguages.Format("Quests: {0} / {1}", entries.Length, _state.Entries.Length);
        UpdateDetails();
    }
    private void UpdateDetails()
    {
        var entry = _state.Entries.FirstOrDefault(e => e.QuestId == _selected);
        _openOffer.IsDisabled = entry?.CanAccept != true;
        _openOffer.Text = ClientLanguages.Text(entry == null || entry.CanAccept ? "VIEW QUEST" : entry.Status);
        var body = entry == null ? ClientLanguages.Text("Select a quest to view its story and requirements.") : string.Join("\n\n",
            ClientLanguages.ContentText(entry.Name), CategoryText(Category(entry)), ClientLanguages.Text(entry.Status),
            ClientLanguages.ContentText(entry.Description ?? ""), ClientLanguages.Text("PREREQUISITES"), ClientLanguages.Expanded(entry.Requirements ?? ""));
        EventPanelText.SetBody(_details, _detailText, body); _details.ScrollToTop();
    }
}
