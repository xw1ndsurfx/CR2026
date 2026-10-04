using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

internal sealed class QuestShopWindow : Window
{
    private readonly Label _description;
    private readonly Label _message;
    private readonly ScrollControl _quests;
    private Guid _shopId;

    public QuestShopWindow(Canvas parent)
        : base(parent, "Quest Shop", false, nameof(QuestShopWindow))
    {
        SetSize(720, 570);
        Alignment = [Alignments.Center];
        DeleteOnClose = false;
        DisableResizing();

        _description = new Label(this, "Description")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 10,
            TextColorOverride = Color.White,
        };
        _description.SetBounds(24, 42, 666, 55);

        _message = new Label(this, "Message")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 9,
            TextColorOverride = new Color(a: 255, r: 220, g: 196, b: 135),
        };
        _message.SetBounds(24, 100, 666, 28);

        _quests = new ScrollControl(this, "QuestList")
        {
            OverflowX = OverflowBehavior.Hidden,
            OverflowY = OverflowBehavior.Scroll,
            AutoHideBars = false,
        };
        _quests.SetBounds(24, 134, 666, 405);

        Hide();
    }

    protected override void EnsureInitialized()
    {
    }

    public void Apply(QuestShopStatePacket state)
    {
        _shopId = state.ShopId;
        Title = string.IsNullOrWhiteSpace(state.ShopName) ? "Quest Shop" : state.ShopName;
        _description.Text = state.Description ?? string.Empty;
        _message.Text = state.Message ?? string.Empty;
        _quests.DeleteAll();

        var y = 4;
        foreach (var entry in state.Entries ?? [])
        {
            var row = new QuestRow(_quests, state.ShopId, entry);
            row.SetBounds(4, y, 638, 112);
            y += 118;
        }

        if ((state.Entries?.Length ?? 0) == 0)
        {
            var empty = new Label(_quests, "Empty")
            {
                AutoSizeToContents = false,
                Text = "No quests are currently offered here.",
                TextAlign = Pos.Center,
                TextColorOverride = Color.White,
            };
            empty.SetBounds(20, 80, 590, 50);
        }

        _quests.SetInnerSize(646, Math.Max(405, y + 4));
        _quests.UpdateScrollBars();
    }

    private sealed class QuestRow : Base
    {
        public QuestRow(Base parent, Guid shopId, QuestShopEntryPacket entry)
            : base(parent, "Quest_" + entry.QuestId.ToString("N"))
        {
            var title = new Label(this, "Title")
            {
                AutoSizeToContents = false,
                Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
                FontSize = 12,
                TextColorOverride = Color.White,
                Text = entry.Name,
            };
            title.SetBounds(14, 8, 390, 24);

            var description = new Label(this, "Description")
            {
                AutoSizeToContents = false,
                Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
                FontSize = 9,
                TextColorOverride = new Color(a: 255, r: 200, g: 205, b: 210),
                Text = entry.Description ?? string.Empty,
            };
            description.SetBounds(14, 34, 430, 68);

            var status = new Label(this, "Status")
            {
                AutoSizeToContents = false,
                Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
                FontSize = 9,
                TextAlign = Pos.Center,
                TextColorOverride = StatusColor(entry.Status),
                Text = entry.Status,
            };
            status.SetBounds(460, 12, 160, 26);

            var accept = new Button(this, "Accept")
            {
                Text = entry.CanAccept ? "ACCEPT QUEST" : entry.Status,
                Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
                FontSize = 9,
                IsDisabled = !entry.CanAccept,
            };
            accept.SetBounds(470, 52, 150, 38);
            accept.Clicked += (_, _) =>
            {
                if (!entry.CanAccept)
                    return;
                accept.IsDisabled = true;
                accept.Text = "ACCEPTING...";
                Networking.PacketSender.SendAcceptQuestShopQuest(shopId, entry.QuestId);
            };
        }

        private static Color StatusColor(string? status) =>
            status switch
            {
                "AVAILABLE" => new Color(a: 255, r: 145, g: 230, b: 150),
                "IN PROGRESS" => new Color(a: 255, r: 100, g: 180, b: 245),
                "COMPLETED" => new Color(a: 255, r: 220, g: 196, b: 135),
                _ => new Color(a: 255, r: 170, g: 170, b: 170),
            };
    }
}
