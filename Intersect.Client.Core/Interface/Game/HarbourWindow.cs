using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

internal sealed class HarbourWindow : Window
{
    private readonly Label _message, _pageLabel;
    private readonly Base _cards;
    private readonly Button _previous, _next;
    private HarbourStatePacket _state = new();
    private int _page;
    private bool _waiting;
    private long _sent;
    public HarbourWindow(Canvas parent) : base(parent, ClientLanguages.Text("Harbour"), false, nameof(HarbourWindow))
    {
        SetSize(660, 500); Alignment = [Alignments.Center]; DeleteOnClose = false; DisableResizing();
        _message = Text(this, "Status", "", 16, 36, 625, 58);
        _cards = new Base(this, "ShipCards"); _cards.SetBounds(16, 100, 625, 320);
        _previous = Button(this, "Previous", "Previous", 16, 435, 110, () => { _page--; RenderEntries(); });
        _pageLabel = Text(this, "Page", "", 170, 440, 200, 28);
        _next = Button(this, "Next", "Next", 420, 435, 100, () => { _page++; RenderEntries(); });
        Button(this, "Refresh", "Refresh", 530, 435, 110, () => Request(HarbourAction.Open, ""));
    }
    protected override void EnsureInitialized() { }
    private static Label Text(Base parent, string id, string text, int x, int y, int w, int h)
    {
        var label = new Label(parent, id) { Text = text, AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? parent.Skin.DefaultFont, FontSize = 11 };
        label.SetBounds(x,y,w,h); return label;
    }
    private static Button Button(Base parent, string id, string caption, int x, int y, int w, Action action)
    {
        var button = new Button(parent, id) { Text = ClientLanguages.Text(caption),
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? parent.Skin.DefaultFont, FontSize = 11 };
        button.SetBounds(x,y,w,30); button.Clicked += (_,_) => action(); return button;
    }
    internal void Apply(HarbourStatePacket packet)
    {
        _state = packet; _state.Entries ??= []; _waiting = false;
        _message.Text = ClientLanguages.Expanded(packet.Message.Length == 0 ? "Choose a ship, buy one or fully repair your fleet." : packet.Message);
        RenderEntries();
    }
    private void Request(HarbourAction action, string model)
    {
        // Retry is possible after a lost reply. The server revalidates every operation at the dock.
        if (_waiting && Environment.TickCount64 - _sent < 3000) return;
        _waiting = true; _sent = Environment.TickCount64;
        Networking.Network.SendPacket(new HarbourRequestPacket { Action = action, ModelId = model });
    }
    private void RenderEntries()
    {
        _cards.DeleteAllChildren();
        var pages = Math.Max(1, (_state.Entries.Length + 1) / 2); _page = Math.Clamp(_page, 0, pages - 1);
        _pageLabel.Text = $"{_page+1} / {pages}"; _previous.IsDisabled = _page == 0; _next.IsDisabled = _page >= pages-1;
        var i = 0;
        foreach (var entry in _state.Entries.Skip(_page*2).Take(2))
        {
            var y = i++ * 155;
            Text(_cards, "Name" + i, entry.Name is "Royal ship" or "Royal ship (Premium)" ? ClientLanguages.Text(entry.Name) : ClientLanguages.ContentText(entry.Name), 4, y, 610, 24);
            var status = entry.Owned ? (entry.Health == 0 ? ClientLanguages.Text("Sunken") :
                string.Format(ClientLanguages.Text("Hull: {0}/100"), entry.Health)) : ClientLanguages.Text("Not owned");
            if (entry.PremiumRequired) status += " · " + ClientLanguages.Text("Premium required");
            Text(_cards, "Condition" + i, status, 4, y+25, 610, 23);
            Text(_cards, "Cost" + i, string.Format(ClientLanguages.Text("Purchase: {0} {1} · Balance: {2}"),
                entry.Price, entry.Currency, entry.Balance), 4, y+49, 610, 24);
            Text(_cards, "RepairCost" + i, string.Format(ClientLanguages.Text("Full repair: {0} {1}"),
                entry.RepairPrice, entry.Currency), 4, y+75, 610, 24);
            var buy = Button(_cards, "Buy" + i, "Buy ship", 4, y+110, 195, () => Request(HarbourAction.Buy, entry.ModelId));
            buy.IsDisabled = !HarbourUiRules.CanBuy(entry, _state.Premium);
            var launch = Button(_cards, "Launch" + i, "Set sail", 211, y+110, 195, () => Request(HarbourAction.Launch, entry.ModelId));
            launch.IsDisabled = !HarbourUiRules.CanLaunch(entry, _state.Premium);
            var repair = Button(_cards, "Repair" + i, "Fully repair", 418, y+110, 195, () => Request(HarbourAction.Repair, entry.ModelId));
            repair.IsDisabled = !HarbourUiRules.CanRepair(entry);
        }
    }
}

internal static class HarbourUiRules
{
    internal static bool CanBuy(HarbourEntry e, bool premium) => !e.Owned && e.CurrencyAvailable && e.Balance >= e.Price && (!e.PremiumRequired || premium);
    internal static bool CanLaunch(HarbourEntry e, bool premium) => e.Owned && e.Health > 0 && (!e.PremiumRequired || premium);
    internal static bool CanRepair(HarbourEntry e) => e.Owned && e.Health < 100 && e.CurrencyAvailable && e.Balance >= e.RepairPrice;
}
