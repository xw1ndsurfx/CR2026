using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Client.Networking;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.Housing;
using Intersect.Network.Packets.Housing;
namespace Intersect.Client.Interface.Game;
internal sealed class HousingWindow : Window
{
    private readonly ScrollControl _rows;
    private readonly Label _heading;
    private readonly Label _message;
    private readonly Button _enter;
    private readonly Button _privacy;
    private readonly Button[] _tabs;
    private HouseView? _view;
    private int _tab;
    public event Action<Guid, Guid>? PlaceRequested;
    public HousingWindow(Canvas canvas) : base(canvas, ClientLanguages.Text("My house"), false, nameof(HousingWindow))
    {
        SetSize(850, 670); Alignment = [Alignments.Center]; DeleteOnClose = false; DisableResizing();
        _heading = new Label(this, "HouseHeading") { Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont, FontSize = 14, TextColorOverride = Color.White };
        _heading.SetBounds(20, 38, 805, 28);
        _message = new Label(this, "HouseMessage") { Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont, FontSize = 10, TextColorOverride = Color.White };
        _message.SetBounds(20, 558, 800, 26);
        _enter = AddButton(this, "Go home", 20, 78, 150, () => { if (_view != null) PacketSender.SendHouseAction(_view.Inside ? HouseAction.Leave : HouseAction.Visit, _view.OwnerId); });
        _privacy = AddButton(this, "Open to public", 180, 78, 180, () => { if (_view != null) PacketSender.SendHouseAction(HouseAction.SetPublic, _view.OwnerId, open: !_view.House.Public); });
        _tabs = new[] { "Monthly catalog", "Furniture storage", "Placed furniture", "House pets" }.Select((text, index) =>
            AddButton(this, text, 20 + index * 202, 124, 194, () => { _tab = index; RefreshRows(); })).ToArray();
        _rows = new ScrollControl(this, "HouseRows") { OverflowX = OverflowBehavior.Hidden, OverflowY = OverflowBehavior.Scroll, AutoHideBars = true };
        _rows.SetBounds(20, 169, 808, 380);
        AddButton(this, "Close", 675, 588, 150, Hide);
        Hide();
    }
    protected override void EnsureInitialized() { }
    public void Apply(HouseView? view, string message)
    {
        _view = view;
        _message.Text = ClientLanguages.Text(message);
        if (view == null) { if (!string.IsNullOrEmpty(message)) Show(); return; }
        _heading.Text = ClientLanguages.Format("{0}'s house - {1}", view.OwnerName, view.Month);
        _enter.Text = ClientLanguages.Text(view.Inside ? "Leave house" : "Go home"); _enter.IsDisabled = !view.Enabled;
        _privacy.IsHidden = !view.IsOwner; _privacy.Text = ClientLanguages.Text(view.House.Public ? "Make private" : "Open to public");
        _tabs[0].IsHidden = _tabs[1].IsHidden = !view.IsOwner;
        if (!view.IsOwner && _tab < 2) _tab = 2;
        if (!view.Enabled) _message.Text = ClientLanguages.Text("Housing is not available yet.");
        RefreshRows();
    }
    private void RefreshRows()
    {
        _rows.DeleteAll();
        if (_view == null) return;
        var y = 4;
        void Row(string text, FurnitureDefinition? furniture, string action, Action? click, string? second = null, Action? other = null)
        {
            var card = new Base(_rows); card.SetBounds(0, y, 780, 76);
            if (furniture != null)
            {
                var image = new ImagePanel(card) { MaintainAspectRatio = true, MouseInputEnabled = false };
                image.SetBounds(4, 6, 62, 62); image.Texture = GameContentManager.Current.GetTexture(TextureType.Tileset, furniture.Tileset);
                if (image.Texture != null) image.SetTextureRect(furniture.SourceX, furniture.SourceY, furniture.Width * Options.Instance.Map.TileWidth, furniture.Height * Options.Instance.Map.TileHeight);
            }
            var label = new Label(card) { Text = text, Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont, FontSize = 11, TextColorOverride = Color.White }; label.SetBounds(78, 8, 450, 60);
            if (click != null) AddButton(card, action, 540, 10, 110, click);
            if (other != null) AddButton(card, second ?? "", 655, 10, 110, other);
            y += 82;
        }
        if (_tab == 0)
            foreach (var offer in _view.Catalog)
                Row(ClientLanguages.ContentText(offer.Name) + " - " + offer.Price.ToString("N0") + " " + ClientLanguages.ContentText(ItemDescriptor.Get(offer.CurrencyItemId)?.Name ?? ""), offer, "Buy", () => PacketSender.SendHouseAction(HouseAction.Buy, _view.OwnerId, offer.Id));
        else if (_tab == 1)
            foreach (var (id, owned) in _view.House.Furniture)
            {
                var stored = owned.Quantity - _view.House.Placements.Count(p => p.FurnitureId == id);
                Row(ClientLanguages.Format("{0} - stored: {1}", ClientLanguages.ContentText(owned.Definition.Name), stored), owned.Definition, "Place", stored > 0 && _view.Inside ? () => { PlaceRequested?.Invoke(id, Guid.Empty); Hide(); } : null);
            }
        else if (_tab == 2)
            foreach (var placed in _view.House.Placements)
            {
                if (!_view.House.Furniture.TryGetValue(placed.FurnitureId, out var owned)) continue;
                if (_view.Inside && owned.Definition.CommonEventId != Guid.Empty)
                    Row(ClientLanguages.ContentText(owned.Definition.Name), owned.Definition, "Use", () => PacketSender.SendHouseAction(HouseAction.Interact, _view.OwnerId, placement: placed.Id));
                Row(ClientLanguages.ContentText(owned.Definition.Name) + $" ({placed.X}, {placed.Y})", owned.Definition, "Move", _view.IsOwner && _view.Inside ? () => { PlaceRequested?.Invoke(placed.FurnitureId, placed.Id); Hide(); } : null,
                    "Store", _view.IsOwner ? () => PacketSender.SendHouseAction(HouseAction.Remove, _view.OwnerId, placement: placed.Id) : null);
            }
        else
            foreach (var pet in _view.House.Pets)
                Row(pet.Name, null, "Collect pet", _view.IsOwner ? () => PacketSender.SendHouseAction(HouseAction.CollectPet, _view.OwnerId, pet.Id) : null);
        if (y == 4)
        {
            var empty = new Label(_rows) { Text = ClientLanguages.Text(_tab == 3 ? "Send pets home from the Pets window." : "Nothing here yet."), Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont, FontSize = 11, TextColorOverride = Color.White };
            empty.SetBounds(12, 20, 750, 40);
        }
    }
    private static Button AddButton(Base parent, string text, int x, int y, int width, Action action)
    {
        var button = new Button(parent) { Text = ClientLanguages.Text(text), Font = GameContentManager.Current.GetFont("sourcesanspro") ?? parent.Skin.DefaultFont, FontSize = 10 };
        button.SetBounds(x, y, width, 30); button.Clicked += (_, _) => action(); return button;
    }
}
