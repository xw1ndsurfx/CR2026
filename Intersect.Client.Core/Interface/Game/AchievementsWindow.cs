using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Framework.Core.Achievements;
using Intersect.Network.Packets.Server;
using UiRectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

internal sealed class AchievementsWindow : Window
{
    private static readonly Color Gold = new(a: 255, r: 224, g: 190, b: 113);
    private static readonly Color Cream = new(a: 255, r: 246, g: 229, b: 202);
    private static readonly Color Muted = new(a: 255, r: 193, g: 180, b: 161);
    private readonly Label _summary;
    private readonly bool _readOnly;
    private readonly ScrollControl _scroll;

    public AchievementsWindow(Canvas parent, bool readOnly = false) : base(parent,
        readOnly ? "Player Achievements" : "Achievements", false,
        readOnly ? "InspectedAchievementsWindow" : nameof(AchievementsWindow))
    {
        _readOnly = readOnly;
        SetSize(760, 620);
        Alignment = [Alignments.Center];
        DeleteOnClose = false;
        DisableResizing();

        var frame = new AchievementChrome(this, "AchievementsWindowFrame", ChromeStyle.Window);
        frame.SetBounds(14, 30, 732, 562);

        var title = new Label(this, "AchievementsTitle")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 17,
            TextAlign = Pos.Center,
            TextColorOverride = new Color(a: 255, r: 236, g: 210, b: 153),
            Text = readOnly ? "PLAYER ACHIEVEMENTS" : "ACHIEVEMENTS",
        };
        title.SetBounds(20, 36, 720, 34);

        _summary = new Label(this, "AchievementsSummary")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 10,
            TextAlign = Pos.Center,
            TextColorOverride = Muted,
        };
        _summary.SetBounds(20, 72, 720, 32);

        _scroll = new ScrollControl(this, "AchievementsScroll")
        {
            OverflowX = OverflowBehavior.Hidden,
            OverflowY = OverflowBehavior.Scroll,
            AutoHideBars = true,
        };
        _scroll.SetBounds(30, 115, 700, 454);

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
        _summary.Text = (_readOnly ? packet.InspectedPlayerName + "  |  VIEW ONLY  |  " : "") +
            $"{completedCount:N0} / {AchievementConfiguration.Instance.Achievements.Length:N0} completed";

        _scroll.DeleteAll();

        var y = 12;
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
                category.SetBounds(12, y, 650, 24);
                y += 30;
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

        _scroll.SetInnerSize(680, Math.Max(440, y + 12));
        _scroll.UpdateScrollBars();
    }

    private int AddAchievementCard(
        AchievementDefinition definition,
        AchievementProgressEntry progress,
        int y
    )
    {
        const int width = 660;
        const int height = 160;

        var card = new Base(_scroll, $"AchievementCard{definition.Id}")
        {
            MouseInputEnabled = false,
        };
        card.SetBounds(8, y, width, height);

        var frame = new AchievementChrome(card, $"AchievementFrame{definition.Id}", ChromeStyle.Card);
        frame.SetBounds(0, 0, width, height);
        var portraitFrame = new AchievementChrome(card, $"AchievementIconFrame{definition.Id}", ChromeStyle.Portrait);
        portraitFrame.SetBounds(13, 13, 84, 84);

        var icon = new ImagePanel(card, $"AchievementIcon{definition.Id}")
        {
            MaintainAspectRatio = true,
            MouseInputEnabled = false,
        };
        icon.SetBounds(19, 19, 72, 72);
        if (!string.IsNullOrWhiteSpace(definition.Icon))
        {
            icon.Texture = Globals.ContentManager.GetTexture(TextureType.Achievement, definition.Icon);
            if (icon.Texture == null) icon.Hide();
        }
        else icon.Hide();

        AddText(card, $"AchievementName{definition.Id}", definition.Name,
            110, 13, 380, 25, 13, progress.Completed ? Gold : Cream, bold: true);
        AddText(card, $"AchievementDescription{definition.Id}", definition.Description,
            110, 39, 383, 37, 9, Muted);

        var statusText = _readOnly
            ? (progress.Completed ? "COMPLETED" : "IN PROGRESS")
            : progress.Claimed ? "REWARD CLAIMED"
            : progress.Completed ? "READY TO CLAIM" : "IN PROGRESS";
        var statusColor = progress.Claimed
            ? new Color(a: 255, r: 151, g: 196, b: 132)
            : progress.Completed ? Gold : Muted;
        AddText(card, $"AchievementStatus{definition.Id}", statusText,
            500, 13, 147, 23, 9, statusColor, bold: true, center: true);

        var targetAmount = Math.Max(1L, definition.TargetAmount);
        var boundedProgress = Math.Min(targetAmount, Math.Max(0L, progress.Progress));
        var fraction = (float)Math.Clamp((double)boundedProgress / targetAmount, 0d, 1d);
        AddText(card, $"AchievementProgressCaption{definition.Id}", "PROGRESS",
            110, 78, 210, 18, 9, Gold, bold: true);
        var progressBar = new AchievementChrome(card, $"AchievementProgressBar{definition.Id}", ChromeStyle.Bar)
        {
            Progress = fraction,
            ProgressColor = new Color(a: 255, r: 82, g: 159, b: 91),
        };
        progressBar.SetBounds(110, 100, 383, 17);
        AddText(card, $"AchievementProgressValue{definition.Id}",
            $"{boundedProgress:N0} / {targetAmount:N0}", 110, 100, 383, 17,
            9, Cream, center: true);

        var rewardText = BuildRewardText(definition.Reward);
        AddText(card, $"AchievementRewardCaption{definition.Id}", "REWARD",
            14, 126, 83, 22, 9, Gold, bold: true);
        AddText(card, $"AchievementReward{definition.Id}", rewardText,
            100, 126, 377, 22, 9, Cream);

        // The inspection window never creates reward-claim controls.
        if (!_readOnly)
        {
            var claim = new Button(card, $"AchievementClaim{definition.Id}")
            {
                Text = string.Empty,
                IsDisabled = !progress.Completed || progress.Claimed,
            };
            claim.SetBounds(500, 113, 148, 36);
            var claimFrame = new AchievementChrome(claim, $"AchievementClaimFrame{definition.Id}",
                progress.Completed && !progress.Claimed ? ChromeStyle.PrimaryButton : ChromeStyle.SecondaryButton);
            claimFrame.SetBounds(0, 0, 148, 36);
            AddText(claim, $"AchievementClaimLabel{definition.Id}",
                progress.Claimed ? "CLAIMED" : progress.Completed ? "CLAIM REWARD" : "LOCKED",
                4, 0, 140, 36, 10, Cream, bold: true, center: true).BringToFront();
            claim.Clicked += (_, _) =>
                Networking.PacketSender.SendClaimAchievementReward(definition.Id);
    
    
        }

        return y + height + 12;
    }

    private static Label AddText(Base parent, string id, string text,
        int x, int y, int width, int height, int size, Color color,
        bool bold = false, bool center = false)
    {
        var label = new Label(parent, id)
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont(
                bold ? "sourcesansproblack" : "sourcesanspro") ?? parent.Skin.DefaultFont,
            FontSize = size,
            TextAlign = center ? Pos.Center : Pos.Left | Pos.CenterV,
            TextColorOverride = color,
            Text = text,
            MouseInputEnabled = false,
        };
        label.SetBounds(x, y, width, height);
        return label;
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
    private enum ChromeStyle
    {
        Window, Card, Portrait, Bar, PrimaryButton, SecondaryButton
    }

    // Shared visual language with Pets: dark panels and copper/gold pixel frames.
    private sealed class AchievementChrome : Base
    {
        private static readonly Color Dark = new(a: 255, r: 28, g: 18, b: 17);
        private static readonly Color Brown = new(a: 255, r: 56, g: 35, b: 28);
        private static readonly Color Copper = new(a: 255, r: 90, g: 55, b: 37);
        private static readonly Color BorderColor = new(a: 255, r: 162, g: 116, b: 55);
        private static readonly Color Highlight = new(a: 255, r: 228, g: 183, b: 93);
        private readonly ChromeStyle _style;

        public float Progress { get; set; }
        public Color ProgressColor { get; set; } = Color.White;

        public AchievementChrome(Base parent, string name, ChromeStyle style)
            : base(parent, name)
        {
            _style = style;
            MouseInputEnabled = false;
            KeyboardInputEnabled = false;
        }

        protected override void Render(SkinBase skin)
        {
            var bounds = RenderBounds;
            switch (_style)
            {
                case ChromeStyle.Window:
                    Fill(skin, bounds, Dark);
                    Border(skin, bounds, BorderColor, 2);
                    Border(skin, Inset(bounds, 5), Copper, 1);
                    Fill(skin, bounds.X + 18, bounds.Y + 73, bounds.Width - 36, 1, Copper);
                    break;
                case ChromeStyle.Card:
                    Fill(skin, bounds, Dark);
                    Border(skin, bounds, BorderColor, 2);
                    Border(skin, Inset(bounds, 4), Copper, 1);
                    Fill(skin, bounds.X + 105, bounds.Y + 10,
                        bounds.Width - 119, 65, Copper);
                    Fill(skin, bounds.X + 105, bounds.Y + 75,
                        bounds.Width - 119, 1, Highlight);
                    Fill(skin, bounds.X + 12, bounds.Bottom - 40,
                        bounds.Width - 24, 1, Copper);
                    break;
                case ChromeStyle.Portrait:
                    Fill(skin, bounds, Dark);
                    Border(skin, bounds, Highlight, 2);
                    Border(skin, Inset(bounds, 5), Copper, 1);
                    break;
                case ChromeStyle.Bar:
                    Fill(skin, bounds, Dark);
                    Border(skin, bounds, Copper, 1);
                    Fill(skin, bounds.X + 2, bounds.Y + 2,
                        (int)((bounds.Width - 4) * Math.Clamp(Progress, 0f, 1f)),
                        bounds.Height - 4, ProgressColor);
                    break;
                case ChromeStyle.PrimaryButton:
                    Fill(skin, bounds, new Color(a: 255, r: 49, g: 77, b: 42));
                    Border(skin, bounds, new Color(a: 255, r: 148, g: 183, b: 105), 2);
                    Border(skin, Inset(bounds, 4), Copper, 1);
                    break;
                case ChromeStyle.SecondaryButton:
                    Fill(skin, bounds, Copper);
                    Border(skin, bounds, Highlight, 2);
                    Border(skin, Inset(bounds, 4), Brown, 1);
                    break;
            }
        }

        private static UiRectangle Inset(UiRectangle rect, int amount) =>
            new(rect.X + amount, rect.Y + amount,
                rect.Width - amount * 2, rect.Height - amount * 2);

        private static void Fill(SkinBase skin, UiRectangle rect, Color color)
        {
            skin.Renderer.DrawColor = color;
            skin.Renderer.DrawFilledRect(rect);
        }

        private static void Fill(SkinBase skin, int x, int y, int width, int height, Color color)
        {
            if (width <= 0 || height <= 0) return;
            Fill(skin, new UiRectangle(x, y, width, height), color);
        }

        private static void Border(SkinBase skin, UiRectangle rect, Color color, int thickness)
        {
            Fill(skin, rect.X, rect.Y, rect.Width, thickness, color);
            Fill(skin, rect.X, rect.Bottom - thickness, rect.Width, thickness, color);
            Fill(skin, rect.X, rect.Y, thickness, rect.Height, color);
            Fill(skin, rect.Right - thickness, rect.Y, thickness, rect.Height, color);
        }
    }

}
