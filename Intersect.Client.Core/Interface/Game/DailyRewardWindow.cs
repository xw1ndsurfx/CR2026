using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets.Server;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

internal sealed class DailyRewardWindow : Window
{
    private sealed class RewardCard : Base
    {
        private readonly Label _day;
        private readonly Label _reward;
        private readonly ImagePanel _icon;
        private bool _active;
        private bool _claimed;

        public RewardCard(Base parent, int day) : base(parent, "DailyRewardCard" + day)
        {
            SetSize(132, 112);
            _day = new Label(this, "Day")
            {
                AutoSizeToContents = false,
                TextAlign = Pos.Center,
                TextColorOverride = new Color(a:255,r:236,g:210,b:153),
                FontSize = 12,
                Text = $"Day {day}",
            };
            _day.SetBounds(4, 5, 124, 20);

            _icon = new ImagePanel(this, "Icon") { MaintainAspectRatio = true };
            _icon.SetBounds(46, 29, 40, 40);

            _reward = new Label(this, "Reward")
            {
                AutoSizeToContents = false,
                TextAlign = Pos.Center,
                TextColorOverride = Color.White,
                FontSize = 10,
            };
            _reward.SetBounds(6, 73, 120, 32);
        }

        public void SetReward(ItemDescriptor? item, int quantity, bool active, bool claimed)
        {
            _active = active;
            _claimed = claimed;
            _reward.Text = item == null ? "No reward" : $"{quantity:N0} x {item.Name}";
            _icon.Texture = item == null || string.IsNullOrWhiteSpace(item.Icon)
                ? null
                : Globals.ContentManager.GetTexture(TextureType.Item, item.Icon);
        }

        protected override void Render(SkinBase skin)
        {
            var renderer = skin.Renderer;
            renderer.DrawColor = _active
                ? new Color(a:255,r:92,g:64,b:44)
                : _claimed
                    ? new Color(a:255,r:48,g:66,b:44)
                    : new Color(a:255,r:42,g:29,b:27);
            renderer.DrawFilledRect(RenderBounds);
            renderer.DrawColor = _active
                ? new Color(a:255,r:218,g:184,b:86)
                : new Color(a:255,r:126,g:82,b:62);
            var b = RenderBounds;
            renderer.DrawFilledRect(new Rectangle(b.X, b.Y, b.Width, 2));
            renderer.DrawFilledRect(new Rectangle(b.X, b.Bottom - 2, b.Width, 2));
            renderer.DrawFilledRect(new Rectangle(b.X, b.Y, 2, b.Height));
            renderer.DrawFilledRect(new Rectangle(b.Right - 2, b.Y, 2, b.Height));
        }
    }

    private readonly Label _title;
    private readonly Label _status;
    private bool _initialized;
    private readonly Panel _cards;
    private readonly Button _claim;
    private DailyRewardStatePacket? _state;

    public DailyRewardWindow(Canvas parent) : base(parent, "DailyRewardWindow")
    {
        SetTitle("Daily Reward", false);
        SetSize(620, 420);
        Alignment = [Alignments.Center];
        DeleteOnClose = false;

        _title = new Label(this, "Title")
        {
            AutoSizeToContents = false,
            TextAlign = Pos.Center,
            TextColorOverride = new Color(a:255,r:236,g:210,b:153),
            FontSize = 22,
            Text = "Daily Reward",
        };
        _title.SetBounds(20, 38, 580, 38);

        _status = new Label(this, "Status")
        {
            AutoSizeToContents = false,
            TextAlign = Pos.Center,
            TextColorOverride = Color.White,
            FontSize = 12,
        };
        _status.SetBounds(20, 78, 580, 36);

        _cards = new Panel(this, "Cards") { ShouldDrawBackground = false };
        _cards.SetBounds(28, 120, 564, 230);

        _claim = new Button(this, "Claim") { Text = "Claim", FontSize = 16 };
        _claim.SetBounds(225, 358, 170, 38);
        _claim.Clicked += (_, _) => Networking.PacketSender.SendClaimDailyReward();

        Hide();
    }

    protected override void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;
    }

    public void Apply(DailyRewardStatePacket state)
    {
        _state = state;
        _cards.DeleteAllChildren();

        var grouped = state.Rewards.GroupBy(reward => reward.Day)
            .ToDictionary(group => group.Key, group => group.ToArray());

        for (var day = 1; day <= state.CycleDays; ++day)
        {
            var card = new RewardCard(_cards, day);
            var col = (day - 1) % 4;
            var row = (day - 1) / 4;
            card.SetPosition(col * 140, row * 116);

            var first = grouped.GetValueOrDefault(day)?.FirstOrDefault();
            var item = first == null ? null : ItemDescriptor.Get(first.ItemId);
            card.SetReward(item, first?.Quantity ?? 0,
                active: state.CanClaim && state.CurrentDay == day,
                claimed: state.ClaimedToday && state.CurrentDay == day);
        }

        _status.Text = string.IsNullOrWhiteSpace(state.Message)
            ? state.CanClaim
                ? $"Day {state.CurrentDay} is ready to claim."
                : $"Day {state.CurrentDay} claimed. Come back tomorrow."
            : state.Message;

        _claim.IsDisabled = !state.CanClaim;
        _claim.Text = state.CanClaim ? "Claim" : "Claimed";
    }

    public void ShowAndRequest()
    {
        Show();
        BringToFront();
        Networking.PacketSender.SendRequestDailyRewardState();
    }
}
