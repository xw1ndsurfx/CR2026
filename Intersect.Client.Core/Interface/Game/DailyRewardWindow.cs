using Intersect.Client.Core;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets.Server;
using Rectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
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
            SetSize(132, 122);
            _day = new Label(this, "Day")
            {
                AutoSizeToContents = false,
                Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
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
                Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
                TextAlign = Pos.Center,
                TextColorOverride = Color.White,
                FontSize = 9,
            };
            _reward.SetBounds(6, 72, 120, 30);
        }

        public void SetReward(ItemDescriptor? item, int quantity, int extraRewards, bool active, bool claimed)
        {
            _active = active;
            _claimed = claimed;
            _reward.Text = item == null
                ? "No reward"
                : $"{quantity:N0} x {item.Name}" + (extraRewards > 0 ? $"\n+ {extraRewards} more" : "");
            _icon.Texture = item == null || string.IsNullOrWhiteSpace(item.Icon)
                ? null
                : Globals.ContentManager.GetTexture(TextureType.Item, item.Icon);
            if (item != null)
            {
                _icon.RenderColor = item.Color;
            }

            var state = new Label(this, "State")
            {
                AutoSizeToContents = false,
                Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
                TextAlign = Pos.Center,
                FontSize = 8,
                Text = active ? "AVAILABLE" : claimed ? "CLAIMED" : "LOCKED",
                TextColorOverride = active
                    ? new Color(a:255,r:255,g:225,b:125)
                    : claimed
                        ? new Color(a:255,r:150,g:190,b:125)
                        : new Color(a:255,r:175,g:165,b:155),
            };
            state.SetBounds(6, 101, 120, 16);
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
    private readonly ScrollControl _cards;
    private readonly Button _claim;
    private DailyRewardStatePacket? _state;

    public DailyRewardWindow(Canvas parent) : base(parent, "Daily Rewards", false, nameof(DailyRewardWindow))
    {
        SetSize(620, 420);
        Alignment = [Alignments.Center];
        DeleteOnClose = false;

        _title = new Label(this, "Title")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            TextAlign = Pos.Center,
            TextColorOverride = new Color(a:255,r:236,g:210,b:153),
            FontSize = 22,
            Text = "DAILY REWARDS",
        };
        _title.SetBounds(20, 38, 580, 38);

        _status = new Label(this, "Status")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            TextAlign = Pos.Center,
            TextColorOverride = Color.White,
            FontSize = 11,
        };
        _status.SetBounds(20, 78, 580, 36);

        _cards = new ScrollControl(this, "Cards")
        {
            OverflowX = OverflowBehavior.Hidden,
            OverflowY = OverflowBehavior.Scroll,
            AutoHideBars = true,
        };
        _cards.SetBounds(28, 120, 564, 230);

        _claim = new Button(this, "Claim")
        {
            Text = "Claim",
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 14,
        };
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
        _cards.DeleteAll();

        if (!state.Enabled)
        {
            _status.Text = string.IsNullOrWhiteSpace(state.Message)
                ? "Daily rewards are disabled. Enable them in the Game Editor."
                : state.Message;

            var disabled = new Label(_cards, "DailyRewardsDisabled")
            {
                AutoSizeToContents = false,
                Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
                FontSize = 11,
                Text = "Daily Rewards are currently disabled.\nEnable them in Content Editors > Daily & Level Rewards Editor.",
                TextAlign = Pos.Center,
                TextColorOverride = new Color(a:255,r:222,g:210,b:185),
            };
            disabled.SetBounds(20, 55, 500, 80);

            _claim.IsDisabled = true;
            _claim.Text = "Unavailable";
            _cards.SetInnerSize(540, 190);
            _cards.UpdateScrollBars();
            return;
        }

        var grouped = state.Rewards.GroupBy(reward => reward.Day)
            .ToDictionary(group => group.Key, group => group.ToArray());

        for (var day = 1; day <= state.CycleDays; ++day)
        {
            var card = new RewardCard(_cards, day);
            var col = (day - 1) % 4;
            var row = (day - 1) / 4;
            card.SetPosition(col * 140, row * 126);

            var entries = grouped.GetValueOrDefault(day) ?? [];
            var first = entries.FirstOrDefault();
            var item = first == null ? null : ItemDescriptor.Get(first.ItemId);
            card.SetReward(
                item,
                first?.Quantity ?? 0,
                Math.Max(0, entries.Length - 1),
                active: state.CanClaim && state.CurrentDay == day,
                claimed: state.ClaimedToday ? day <= state.CurrentDay : state.CurrentDay > 1 && day < state.CurrentDay
            );
        }

        var rows = (int)Math.Ceiling(state.CycleDays / 4d);
        _cards.SetInnerSize(544, Math.Max(230, 10 + rows * 126));
        _cards.UpdateScrollBars();

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
