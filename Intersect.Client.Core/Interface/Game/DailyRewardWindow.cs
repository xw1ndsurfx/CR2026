using Intersect.Client.Core;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets.Server;
using UiRectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

/// <summary>
/// Admurin's compact, vertical Daily Reward panel. The reward and progress art
/// comes from the original PNG sheets in resources/gui/admurin. Content and
/// claim eligibility always come from the server packet, never from the UI.
/// </summary>
internal sealed class DailyRewardWindow : Window
{
    private static readonly Color Gold = new(a: 255, r: 232, g: 209, b: 151);
    private static readonly Color Cream = new(a: 255, r: 245, g: 233, b: 206);
    private static readonly Color Muted = new(a: 255, r: 177, g: 165, b: 145);

    private sealed class RewardBoard : Base
    {
        public RewardBoard(Base parent) : base(parent, "DailyRewardBoard")
        {
            MouseInputEnabled = false;
        }

        protected override void Render(SkinBase skin)
        {
            var r = RenderBounds;
            var renderer = skin.Renderer;
            renderer.DrawColor = new Color(a: 255, r: 171, g: 139, b: 76);
            renderer.DrawFilledRect(r);
            renderer.DrawColor = new Color(a: 255, r: 12, g: 17, b: 9);
            renderer.DrawFilledRect(new UiRectangle(r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4));
        }
    }

    private sealed class RewardCard : Base
    {
        private readonly ImagePanel _icon;
        private readonly ImagePanel? _claimedTick;
        private readonly ImagePanel? _slot;
        private readonly Label _count;
        private readonly Label _day;
        private bool _active;
        private bool _claimed;

        public RewardCard(Base parent, int day) : base(parent, $"DailyRewardCard{day}")
        {
            SetSize(78, 78);
            MouseInputEnabled = false;

            _slot = AdmurinGuiArt.Add(this, $"DailyRewardSlot{day}", "daily_1",
                0, 0, 78, 78);
            _day = new Label(this, "Day")
            {
                AutoSizeToContents = false,
                Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
                FontSize = 8,
                TextColorOverride = Gold,
                TextAlign = Pos.Left | Pos.CenterV,
                Text = $"{day:00}",
                MouseInputEnabled = false,
            };
            _day.SetBounds(7, 4, 29, 16);

            _icon = new ImagePanel(this, "Icon")
            {
                MaintainAspectRatio = true,
                ShouldDrawBackground = false,
                MouseInputEnabled = false,
            };
            _icon.SetBounds(18, 12, 44, 44);

            _count = new Label(this, "Count")
            {
                AutoSizeToContents = false,
                Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
                FontSize = 10,
                TextColorOverride = Cream,
                TextAlign = Pos.Center,
                MouseInputEnabled = false,
            };
            _count.SetBounds(5, 56, 69, 17);

            _claimedTick = AdmurinGuiArt.Add(this, $"DailyRewardClaimed{day}", "daily_2",
                15, 12, 48, 47);
            _claimedTick?.Hide();
        }

        public void SetReward(DailyRewardPacketEntry[] entries, bool active, bool claimed)
        {
            _active = active;
            _claimed = claimed;
            if (_claimedTick != null)
            {
                if (claimed) _claimedTick.Show();
                else _claimedTick.Hide();
            }

            if (_slot != null)
                _slot.RenderColor = active
                    ? new Color(a: 255, r: 255, g: 247, b: 205)
                    : Color.White;

            var first = entries.FirstOrDefault();
            var item = first == null ? null : ItemDescriptor.Get(first.ItemId);
            _icon.Texture = item == null || string.IsNullOrWhiteSpace(item.Icon)
                ? null
                : Globals.ContentManager.GetTexture(TextureType.Item, item.Icon);
            _icon.RenderColor = item?.Color ?? Color.White;

            // The preview displays the quantity rather than a long item name.
            // The tooltip still lists all rewards when one day grants a bundle.
            _count.Text = first == null ? "-" :
                entries.Length > 1
                    ? $"x{first.Quantity:N0} +{entries.Length - 1}"
                    : $"x{first.Quantity:N0}";
            var details = entries
                .Select(entry =>
                    $"x{entry.Quantity:N0} {ItemDescriptor.Get(entry.ItemId)?.Name ?? "Unknown item"}");
            SetToolTipText(entries.Length == 0
                ? "No reward configured"
                : string.Join("\n", details));
        }

        protected override void Render(SkinBase skin)
        {
            if (_slot != null)
                return;

            var r = RenderBounds;
            skin.Renderer.DrawColor = _active
                ? new Color(a: 255, r: 105, g: 74, b: 49)
                : _claimed
                    ? new Color(a: 255, r: 54, g: 54, b: 40)
                    : new Color(a: 255, r: 74, g: 49, b: 40);
            skin.Renderer.DrawFilledRect(r);
        }
    }

    private readonly Label _status;
    private readonly Base _milestones;
    private readonly ScrollControl _cards;
    private readonly Button _claim;

    public DailyRewardWindow(Canvas parent)
        : base(parent, "Daily Reward", false, nameof(DailyRewardWindow))
    {
        SetSize(350, 552);
        Alignment = [Alignments.Center];
        DeleteOnClose = false;
        DisableResizing();

        // Chest sprites are 48x32 cells in a 5x8 sheet; draw selected cells
        // rather than scaling the entire 240x256 spritesheet into the panel.
        _milestones = new Base(this, "DailyMilestones");
        _milestones.SetBounds(23, 36, 304, 122);
        _milestones.MouseInputEnabled = false;

        var frame = new RewardBoard(this);
        frame.SetBounds(24, 165, 302, 314);
        // Original 16x16 gold-bordered item container, sliced so the two-pixel
        // border remains sharp around the variable-length reward grid.
        AdmurinGuiArt.AddNineSlice(frame, "AdmurinDailyBoard", "bordered_container",
            0, 0, 302, 314, 16, 16,
            2, 2, 2, 2, 3, 3, 3, 3);

        _cards = new ScrollControl(this, "Cards")
        {
            OverflowX = OverflowBehavior.Hidden,
            OverflowY = OverflowBehavior.Scroll,
            AutoHideBars = true,
        };
        _cards.SetBounds(36, 175, 280, 296);

        _status = new Label(this, "Status")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 9,
            TextAlign = Pos.Center,
            TextColorOverride = Muted,
            MouseInputEnabled = false,
        };
        _status.SetBounds(18, 480, 314, 24);

        _claim = new Button(this, "Claim")
        {
            Text = "Claim",
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 12,
        };
        _claim.SetBounds(108, 506, 134, 35);
        _claim.Clicked += (_, _) =>
        {
            if (!_claim.IsDisabled)
            {
                _claim.IsDisabled = true; // Re-enabled by the authoritative reply.
                Networking.PacketSender.SendClaimDailyReward();
            }
        };

        Hide();
    }

    protected override void EnsureInitialized() { }

    private void DrawMilestones(DailyRewardStatePacket state)
    {
        _milestones.DeleteAllChildren();

        const int barX = 10;
        const int barY = 51;
        const int barWidth = 278;

        AdmurinGuiArt.Add(_milestones, "DailyProgressTrack", "daily_4",
            barX, barY, barWidth, 16, 0, 0, 240, 16);

        var cycle = Math.Max(1, state.CycleDays);
        var completed = Math.Clamp(state.LastClaimedDay, 0, cycle);
        var fraction = completed / (double)cycle;
        if (fraction > 0)
        {
            var fillWidth = Math.Clamp((int)Math.Round((barWidth - 6) * fraction), 1, barWidth - 6);
            var sourceWidth = Math.Clamp((int)Math.Round(240 * fraction), 1, 240);
            AdmurinGuiArt.Add(_milestones, "DailyProgressFill", "daily_4",
                barX + 3, barY + 4, fillWidth, 8, 0, 16, sourceWidth, 16);
        }

        int[] markers = [1, Math.Max(2, (int)Math.Ceiling(cycle / 2d)), cycle];
        int[] positions = [27, 144, 267];
        int[] chestRows = [0, 64, 128];

        for (var i = 0; i < markers.Length; i++)
        {
            var day = Math.Clamp(markers[i], 1, cycle);
            var done = completed >= day;
            var active = state.CurrentDay == day && state.CanClaim;

            AdmurinGuiArt.Add(_milestones, $"MilestoneChest{i}", "chests",
                positions[i] - 25, 2, 50, 34,
                0, chestRows[i] + (done ? 32 : 0), 48, 32);

            AdmurinGuiArt.Add(_milestones, $"MilestoneDot{i}", "daily_3",
                positions[i] - 11, 58, 22, 22,
                done || active ? 32 : 0, 0, 32, 32);

            var dayLabel = new Label(_milestones, $"MilestoneDay{i}")
            {
                Text = day.ToString(),
                AutoSizeToContents = false,
                TextAlign = Pos.Center,
                TextColorOverride = done || active ? Cream : Muted,
                Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
                FontSize = 9,
                MouseInputEnabled = false,
            };
            dayLabel.SetBounds(positions[i] - 17, 85, 34, 22);
        }
    }

    public void Apply(DailyRewardStatePacket state)
    {
        _cards.DeleteAll();
        DrawMilestones(state);

        if (!state.Enabled)
        {
            _status.Text = string.IsNullOrWhiteSpace(state.Message)
                ? "Daily rewards are disabled in the Game Editor."
                : state.Message;
            _claim.IsDisabled = true;
            _claim.Text = "Unavailable";
            _cards.SetInnerSize(260, 286);
            _cards.UpdateScrollBars();
            return;
        }

        var grouped = (state.Rewards ?? [])
            .GroupBy(reward => reward.Day)
            .ToDictionary(group => group.Key, group => group.ToArray());

        const int columns = 3;
        const int cardWidth = 78;
        const int spacing = 8;
        const int viewportWidth = 264;
        const int rowHeight = 85;

        var count = Math.Max(0, state.CycleDays);
        for (var index = 0; index < count; index++)
        {
            var day = index + 1;
            var row = index / columns;
            var column = index % columns;
            var rowCount = Math.Min(columns, count - row * columns);
            var rowWidth = rowCount * cardWidth + (rowCount - 1) * spacing;
            var startX = (viewportWidth - rowWidth) / 2;
            var card = new RewardCard(_cards, day);
            card.SetPosition(startX + column * (cardWidth + spacing), 5 + row * rowHeight);

            card.SetReward(
                grouped.GetValueOrDefault(day) ?? [],
                state.CanClaim && state.CurrentDay == day,
                state.LastClaimedDay >= day);
        }

        var rowCountTotal = (int)Math.Ceiling(count / (double)columns);
        _cards.SetInnerSize(viewportWidth, Math.Max(288, 9 + rowCountTotal * rowHeight));
        _cards.UpdateScrollBars();

        _status.Text = string.IsNullOrWhiteSpace(state.Message)
            ? state.CanClaim
                ? $"Day {state.CurrentDay} is ready to claim."
                : $"Day {state.CurrentDay} claimed. Come back tomorrow."
            : state.Message;
        _claim.IsDisabled = !state.CanClaim;
        _claim.Text = state.CanClaim ? "CLAIM" : "CLAIMED";
    }

    public void ShowAndRequest()
    {
        Show();
        BringToFront();
        Networking.PacketSender.SendRequestDailyRewardState();
    }
}
