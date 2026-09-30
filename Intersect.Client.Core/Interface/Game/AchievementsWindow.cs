using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Framework.Core.Achievements;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

internal sealed class AchievementsWindow : Window
{
    private readonly Label _summary;
    private readonly ScrollControl _scroll;

    public AchievementsWindow(Canvas parent) : base(parent, "Achievements", false, nameof(AchievementsWindow))
    {
        SetSize(760, 620);
        Alignment = [Alignments.Center];
        DeleteOnClose = false;
        DisableResizing();

        var title = new Label(this, "AchievementsTitle")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 22,
            TextAlign = Pos.Center,
            TextColorOverride = new Color(a: 255, r: 236, g: 210, b: 153),
            Text = "ACHIEVEMENTS",
        };
        title.SetBounds(20, 36, 720, 34);

        _summary = new Label(this, "AchievementsSummary")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 10,
            TextAlign = Pos.Center,
            TextColorOverride = Color.White,
        };
        _summary.SetBounds(20, 72, 720, 32);

        _scroll = new ScrollControl(this, "AchievementsScroll")
        {
            OverflowX = OverflowBehavior.Hidden,
            OverflowY = OverflowBehavior.Scroll,
            AutoHideBars = true,
        };
        _scroll.SetBounds(28, 112, 704, 472);

        Hide();
    }

    protected override void EnsureInitialized()
    {
    }

    public void Apply(AchievementStatePacket packet)
    {
        AchievementConfiguration.Load(packet.ConfigurationJson);
        var state = (packet.Progress ?? []).ToDictionary(entry => entry.AchievementId);

        var definitions = AchievementConfiguration.Instance.Achievements
            .Where(
                definition =>
                    !definition.HiddenUntilCompleted ||
                    state.TryGetValue(definition.Id, out var progress) && progress.Completed
            )
            .OrderBy(definition => definition.Category)
            .ThenBy(definition => definition.SortOrder)
            .ThenBy(definition => definition.Name)
            .ToArray();

        var completedCount = state.Values.Count(entry => entry.Completed);
        _summary.Text =
            $"{completedCount:N0} / {AchievementConfiguration.Instance.Achievements.Length:N0} completed";

        _scroll.DeleteAll();

        var y = 8;
        string? lastCategory = null;

        foreach (var definition in definitions)
        {
            if (!string.Equals(lastCategory, definition.Category, StringComparison.Ordinal))
            {
                lastCategory = definition.Category;
                var category = new Label(_scroll, $"AchievementCategory{definition.Id}")
                {
                    AutoSizeToContents = false,
                    Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
                    FontSize = 13,
                    TextColorOverride = new Color(a: 255, r: 236, g: 210, b: 153),
                    Text = string.IsNullOrWhiteSpace(definition.Category) ? "GENERAL" : definition.Category.ToUpperInvariant(),
                };
                category.SetBounds(8, y, 650, 24);
                y += 28;
            }

            state.TryGetValue(definition.Id, out var progress);
            progress ??= new AchievementProgressEntry(definition.Id, 0, false, false, 0);

            y = AddAchievementCard(definition, progress, y);
        }

        if (definitions.Length == 0)
        {
            var empty = new Label(_scroll, "NoAchievements")
            {
                AutoSizeToContents = false,
                Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
                FontSize = 11,
                TextAlign = Pos.Center,
                TextColorOverride = Color.White,
                Text = "No achievements are configured yet.",
            };
            empty.SetBounds(20, 80, 640, 40);
            y = 140;
        }

        _scroll.SetInnerSize(680, Math.Max(450, y + 12));
        _scroll.UpdateScrollBars();
    }

    private int AddAchievementCard(
        AchievementDefinition definition,
        AchievementProgressEntry progress,
        int y
    )
    {
        const int width = 660;
        const int height = 128;

        var card = new Button(_scroll, $"AchievementCard{definition.Id}")
        {
            Text = string.Empty,
            MouseInputEnabled = false,
        };
        card.SetBounds(8, y, width, height);
        card.SetStateTexture(ComponentState.Normal, "control_button.png");

        var icon = new ImagePanel(card, $"AchievementIcon{definition.Id}")
        {
            MaintainAspectRatio = true,
            MouseInputEnabled = false,
        };
        icon.SetBounds(12, 14, 72, 72);

        if (!string.IsNullOrWhiteSpace(definition.Icon))
        {
            icon.Texture = Globals.ContentManager.GetTexture(TextureType.Image, definition.Icon);
            if (icon.Texture == null)
                icon.Hide();
        }
        else
        {
            icon.Hide();
        }

        var name = new Label(card, $"AchievementName{definition.Id}")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 12,
            TextColorOverride = progress.Completed
                ? new Color(a: 255, r: 241, g: 211, b: 125)
                : Color.White,
            Text = definition.Name,
            MouseInputEnabled = false,
        };
        name.SetBounds(96, 10, 360, 22);

        var description = new Label(card, $"AchievementDescription{definition.Id}")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 9,
            TextColorOverride = new Color(a: 255, r: 220, g: 214, b: 202),
            Text = definition.Description,
            MouseInputEnabled = false,
        };
        description.SetBounds(96, 34, 410, 34);

        var targetAmount = Math.Max(1L, definition.TargetAmount);
        var boundedProgress = Math.Min(targetAmount, Math.Max(0L, progress.Progress));

        var bar = new ProgressBar(card)
        {
            AutoLabel = false,
            Value = (float)Math.Clamp((double)boundedProgress / targetAmount, 0d, 1d),
            Text = $"{boundedProgress:N0} / {targetAmount:N0}",
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 9,
            TextColorOverride = Color.White,
            MouseInputEnabled = false,
        };
        bar.SetBounds(96, 76, 410, 24);

        var status = new Label(card, $"AchievementStatus{definition.Id}")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 9,
            TextAlign = Pos.Center,
            Text = progress.Claimed ? "REWARD CLAIMED" : progress.Completed ? "COMPLETED" : "IN PROGRESS",
            TextColorOverride = progress.Claimed
                ? new Color(a: 255, r: 151, g: 196, b: 132)
                : progress.Completed
                    ? new Color(a: 255, r: 241, g: 211, b: 125)
                    : new Color(a: 255, r: 210, g: 195, b: 175),
            MouseInputEnabled = false,
        };
        status.SetBounds(514, 12, 134, 20);

        var rewardText = BuildRewardText(definition.Reward);
        var reward = new Label(card, $"AchievementReward{definition.Id}")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 8,
            TextAlign = Pos.Center,
            TextColorOverride = Color.White,
            Text = rewardText,
            MouseInputEnabled = false,
        };
        reward.SetBounds(514, 38, 134, 34);

        var claim = new Button(card, $"AchievementClaim{definition.Id}")
        {
            Text = progress.Claimed ? "Claimed" : "Claim Reward",
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 9,
            IsDisabled = !progress.Completed || progress.Claimed,
        };
        claim.SetBounds(516, 79, 130, 32);
        claim.Clicked += (_, _) =>
            Networking.PacketSender.SendClaimAchievementReward(definition.Id);

        return y + height + 10;
    }

    private static string BuildRewardText(AchievementRewardDefinition reward)
    {
        var parts = new List<string>();

        if (reward.ItemId != Guid.Empty && reward.ItemQuantity > 0)
            parts.Add($"{reward.ItemQuantity:N0} item");

        if (reward.CurrencyItemId != Guid.Empty && reward.CurrencyQuantity > 0)
            parts.Add($"{reward.CurrencyQuantity:N0} currency");

        if (reward.Experience > 0)
            parts.Add($"{reward.Experience:N0} XP");

        if (reward.CommonEventId != Guid.Empty)
            parts.Add("event reward");

        return parts.Count == 0 ? "No reward" : string.Join(" + ", parts);
    }

    public void ShowWindow()
    {
        Show();
        BringToFront();
    }
}
