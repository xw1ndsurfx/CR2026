using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Dungeons;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.GameObjects;
using Intersect.Network.Packets.Server;
using Intersect.Utilities;
using UiRectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

/// <summary>
/// The player's dungeon registry. All decorative accents are drawn in Gwen
/// instead of using decorative Unicode characters, which do not exist in
/// every glyph set of the game's pixel font.
/// </summary>
internal sealed class DungeonPanelWindow : Window
{
    private static readonly Color Gold = new(a: 255, r: 220, g: 188, b: 111);
    private static readonly Color SoftGold = new(a: 255, r: 190, g: 158, b: 97);
    private static readonly Color Cream = new(a: 255, r: 241, g: 225, b: 195);
    private static readonly Color Muted = new(a: 255, r: 184, g: 171, b: 151);
    private static readonly Color Good = new(a: 255, r: 135, g: 213, b: 143);
    private static readonly Color Bad = new(a: 255, r: 225, g: 122, b: 115);

    private readonly Label _summary;
    private readonly Label _personalSummary;
    private readonly ScrollControl _scroll;
    private Dictionary<Guid, DungeonPodiumEntry> _podiums = [];
    private long _nextPodiumRefreshAt;

    public DungeonPanelWindow(Canvas parent)
        : base(parent, "Dungeons", false, nameof(DungeonPanelWindow))
    {
        SetSize(820, 685);
        Alignment = [Alignments.Center];
        DeleteOnClose = false;
        DisableResizing();

        var backdrop = new DungeonChrome(this, "DungeonWindowFrame", ChromeStyle.Window);
        backdrop.SetBounds(13, 27, 794, 642);

        AddText(this, "DungeonPanelTitle", "DUNGEON GATES",
            125, 43, 570, 31, 21, Gold, bold: true, center: true);

        AddText(this, "DungeonPanelSubtitle", "ROYAL GATE REGISTRY - DETECTED DUNGEONS",
            120, 75, 580, 20, 9, Muted, center: true);

        _summary = AddText(this, "DungeonPanelSummary", "",
            125, 99, 570, 24, 10, Cream, bold: true, center: true);

        var ribbon = new DungeonChrome(this, "DungeonRecordRibbon", ChromeStyle.Ribbon);
        ribbon.SetBounds(28, 128, 764, 31);

        _personalSummary = AddText(this, "DungeonPanelPersonalSummary", "",
            42, 134, 736, 19, 9, Gold, bold: true, center: true);

        var refresh = new Button(this, "DungeonPanelRefresh")
        {
            Text = "REFRESH",
            FontSize = 9,
        };
        refresh.SetBounds(690, 42, 98, 27);
        refresh.Clicked += (_, _) =>
        {
            var now = Timing.Global.Milliseconds;
            if (now < _nextPodiumRefreshAt)
                return;

            _nextPodiumRefreshAt = now + 3_000;
            Networking.PacketSender.SendRequestDungeonPanel(openWindow: true);
        };

        _scroll = new ScrollControl(this, "DungeonPanelScroll")
        {
            OverflowX = OverflowBehavior.Hidden,
            OverflowY = OverflowBehavior.Scroll,
            AutoHideBars = true,
        };
        _scroll.SetBounds(24, 170, 772, 489);

        Hide();
    }

    protected override void EnsureInitialized()
    {
    }

    public void Apply(DungeonStatePacket packet)
    {
        DungeonConfiguration.Load(packet.ConfigurationJson);

        var statuses = (packet.Statuses ?? [])
            .Where(value => value != null)
            .GroupBy(value => value.DungeonId)
            .ToDictionary(group => group.Key, group => group.First());

        var personalStats = (packet.PlayerStats ?? [])
            .Where(value => value != null)
            .GroupBy(value => value.DungeonId)
            .ToDictionary(group => group.Key, group => group.First());

        if (packet.Podiums != null)
        {
            _podiums = packet.Podiums
                .Where(entry => entry != null && entry.DungeonId != Guid.Empty)
                .GroupBy(entry => entry.DungeonId)
                .ToDictionary(group => group.Key, group => group.First());
        }

        var definitions = DungeonConfiguration.Instance.Dungeons
            .OrderBy(dungeon => dungeon.SortOrder)
            .ThenBy(dungeon => dungeon.Rank)
            .ThenBy(dungeon => dungeon.Name)
            .ToArray();

        var openCount = statuses.Values.Count(status => status.Available);
        _summary.Text = $"{openCount:N0} ACTIVE GATE{(openCount == 1 ? "" : "S")}    /    {definitions.Length:N0} REGISTERED";

        var totalAttempts = personalStats.Values.Sum(entry => entry.Attempts);
        var totalCompletions = personalStats.Values.Sum(entry => entry.Completions);
        var totalFailures = personalStats.Values.Sum(entry => entry.Failures);
        var totalDeaths = personalStats.Values.Sum(entry => entry.Deaths);
        _personalSummary.Text =
            $"YOUR RECORD    {totalCompletions:N0} CLEARS  /  {totalAttempts:N0} RUNS" +
            $"  /  {totalFailures:N0} FAILURES  /  {totalDeaths:N0} DEATHS";

        _scroll.DeleteAll();

        var y = 8;
        foreach (var dungeon in definitions)
        {
            statuses.TryGetValue(dungeon.Id, out var status);
            status ??= new DungeonStatusEntry(dungeon.Id, false, "SEALED", 0);
            personalStats.TryGetValue(dungeon.Id, out var record);
            record ??= new DungeonPlayerStatEntry { DungeonId = dungeon.Id };
            _podiums.TryGetValue(dungeon.Id, out var podium);
            y = AddDungeonCard(dungeon, status, record, podium, y);
        }

        if (definitions.Length == 0)
        {
            AddText(_scroll, "NoDungeons", "No dungeon gates are registered yet.",
                22, 90, 694, 40, 12, Cream, center: true);
            y = 155;
        }

        _scroll.SetInnerSize(744, Math.Max(482, y + 12));
        _scroll.UpdateScrollBars();
    }

    private int AddDungeonCard(
        DungeonDefinition dungeon,
        DungeonStatusEntry status,
        DungeonPlayerStatEntry record,
        DungeonPodiumEntry? podium,
        int y)
    {
        const int width = 724;
        const int height = 453;

        // This non-interactive painted panel replaces the generic button
        // texture and removes its accidental hover/placeholder artifacts.
        var card = new DungeonChrome(_scroll, $"DungeonCard{dungeon.Id}", ChromeStyle.Card);
        card.SetBounds(8, y, width, height);

        var preview = new DungeonChrome(card, $"DungeonPreviewFrame{dungeon.Id}", ChromeStyle.Preview);
        preview.SetBounds(17, 17, 124, 104);

        // Use the image configured in Game Editor. Otherwise the preview
        // displays a deliberately painted pixel-art dungeon gate.
        if (!string.IsNullOrWhiteSpace(dungeon.Image))
        {
            var manager = Globals.ContentManager;
            var texture =
                manager?.GetTexture(TextureType.Dungeon, dungeon.Image) ??
                manager?.GetTexture(TextureType.Image, dungeon.Image);
            if (texture != null)
            {
                var image = new ImagePanel(card, $"DungeonImage{dungeon.Id}")
                {
                    MaintainAspectRatio = true,
                    MouseInputEnabled = false,
                    Texture = texture,
                };
                image.SetBounds(22, 22, 114, 94);
            }
        }

        AddText(card, $"DungeonName{dungeon.Id}", Shorten(dungeon.Name, 43),
            165, 17, 468, 28, 15, Cream, bold: true);

        AddText(card, $"DungeonRank{dungeon.Id}", dungeon.Rank.ToString(),
            657, 15, 49, 29, 21, RankColor(dungeon.Rank), bold: true, center: true);

        AddText(card, $"DungeonLocation{dungeon.Id}",
            "LOCATION  " + Shorten(
                string.IsNullOrWhiteSpace(dungeon.Location) ? "Unknown" : dungeon.Location, 57),
            165, 49, 524, 21, 10, Gold, bold: true);

        AddText(card, $"DungeonDescription{dungeon.Id}", Shorten(
                string.IsNullOrWhiteSpace(dungeon.Description) ? "No description provided." : dungeon.Description,
                105),
            165, 76, 515, 30, 9, Muted);

        AddText(card, $"DungeonTransition{dungeon.Id}", Shorten(BuildTransitionText(status), 57),
            165, 109, 390, 19, 9, Muted);

        AddText(card, $"DungeonStatus{dungeon.Id}", status.Available ? "AVAILABLE" : "SEALED",
            579, 104, 112, 22, 10, status.Available ? Good : Bad, bold: true, center: true);

        var associatedQuest = dungeon.AssociatedQuestId == Guid.Empty
            ? null
            : QuestDescriptor.Get(dungeon.AssociatedQuestId);
        var requiredQuest = dungeon.RequiredQuestInProgressId == Guid.Empty
            ? null
            : QuestDescriptor.Get(dungeon.RequiredQuestInProgressId);

        var questText = associatedQuest?.Name ?? "None";
        if (requiredQuest != null)
            questText += " / REQUIRED IN PROGRESS: " + requiredQuest.Name;

        AddInformationRow(card, dungeon.Id, "Quest", "QUEST", Shorten(questText, 72), 138);
        var maxLevel = dungeon.MaximumLevel > 0 ? $"-{dungeon.MaximumLevel}" : "+";
        var levelText = $"LEVEL {dungeon.MinimumLevel}{maxLevel}  /  RECOMMENDED {dungeon.RecommendedLevel}+" +
                        $"  /  PARTY {dungeon.MinimumPartySize}-{dungeon.MaximumPartySize}" +
                        $"  /  LIVES {Math.Max(1, dungeon.MaxLives)}";
        if (dungeon.TimeLimitMinutes > 0)
            levelText += $"  /  {dungeon.TimeLimitMinutes} MIN";
        if (dungeon.PremiumRequired)
            levelText += "  /  PREMIUM";
        AddInformationRow(card, dungeon.Id, "Level", "LEVEL", Shorten(levelText, 100), 169);

        var requirements = dungeon.CompletionRequirements == DungeonCompletionRequirement.None
            ? DungeonCompletionRequirement.DefeatFinalBoss
            : dungeon.CompletionRequirements;
        var objectives = new List<string>();
        if ((requirements & DungeonCompletionRequirement.DefeatFinalBoss) != 0)
            objectives.Add("Final Boss");
        if ((requirements & DungeonCompletionRequirement.DefeatAllMonsters) != 0)
            objectives.Add("All Monsters");
        AddInformationRow(card, dungeon.Id, "Objective", "OBJECTIVE",
            objectives.Count > 0 ? string.Join("  /  ", objectives) : "No objectives configured", 200);

        var rewards = new List<string>();
        if (dungeon.CompletionExperience > 0)
            rewards.Add($"{dungeon.CompletionExperience:N0} EXP");
        var item = dungeon.CompletionItemId == Guid.Empty
            ? null
            : ItemDescriptor.Get(dungeon.CompletionItemId);
        if (item != null && dungeon.CompletionItemQuantity > 0)
            rewards.Add($"{item.Name} x{dungeon.CompletionItemQuantity:N0}");
        AddInformationRow(card, dungeon.Id, "Rewards", "REWARDS",
            rewards.Count > 0 ? Shorten(string.Join("  /  ", rewards), 88) : "None configured", 231);

        AddText(card, $"DungeonPersonalRecordTitle{dungeon.Id}", "YOUR RECORD",
            38, 270, 244, 20, 11, Gold, bold: true);

        AddText(card, $"DungeonPersonalRecord{dungeon.Id}",
            $"{record.Completions:N0} CLEARS  /  {record.Attempts:N0} RUNS" +
            $"  /  {record.Failures:N0} FAILURES  /  {record.Deaths:N0} DEATHS",
            38, 294, 651, 22, 10, Cream, bold: true);

        var clearRate = record.Attempts > 0
            ? 100.0 * record.Completions / record.Attempts
            : 0.0;
        AddText(card, $"DungeonPersonalBestTitle{dungeon.Id}", "BEST TIME",
            38, 324, 122, 20, 10, Gold, bold: true);
        AddText(card, $"DungeonPersonalBest{dungeon.Id}",
            $"{FormatDuration(record.BestClearTimeMilliseconds)}" +
            $"  /  LAST CLEAR: {FormatLastClear(record.LastCompletedUnixMilliseconds)}" +
            $"  /  CLEAR RATE: {clearRate:0}%",
            168, 324, 521, 20, 9, Cream);

        AddPodiumColumn(card, dungeon.Id, "Clears", "TOP 3 - MOST CLEARS",
            37, podium?.TopClears ?? [], fastest: false);
        AddPodiumColumn(card, dungeon.Id, "Fastest", "TOP 3 - FASTEST CLEARS",
            390, podium?.FastestClears ?? [], fastest: true);

        return y + height + 11;
    }

    private void AddInformationRow(
        Base parent,
        Guid dungeonId,
        string key,
        string heading,
        string content,
        int y)
    {
        AddText(parent, $"DungeonInfo{key}Label{dungeonId}", heading,
            55, y, 142, 25, 10, Gold, bold: true);
        AddText(parent, $"DungeonInfo{key}Value{dungeonId}", content,
            201, y, 493, 25, 9, Cream);
    }

    private void AddPodiumColumn(
        Base parent,
        Guid dungeonId,
        string key,
        string title,
        int x,
        DungeonPodiumPlayerEntry[] entries,
        bool fastest)
    {
        AddText(parent, $"DungeonPodiumHeader{key}{dungeonId}", title,
            x, 360, 300, 22, 10, Gold, bold: true);

        if (entries.Length == 0)
        {
            AddText(parent, $"DungeonPodiumEmpty{key}{dungeonId}", "No champions yet.",
                x, 394, 306, 21, 9, Muted);
            return;
        }

        for (var index = 0; index < Math.Min(3, entries.Length); index++)
        {
            var entry = entries[index];
            if (entry == null)
                continue;

            var result = fastest
                ? FormatDuration(entry.BestClearTimeMilliseconds)
                : $"{entry.Completions:N0} CLEARS";
            AddText(parent, $"DungeonPodium{key}{dungeonId}Rank{index}",
                $"#{index + 1}  {Shorten(entry.PlayerName, 17)}  -  {result}",
                x, 385 + index * 19, 301, 19, 9, PodiumColor(index), bold: true);
        }
    }

    private Label AddText(
        Base parent,
        string name,
        string text,
        int x,
        int y,
        int width,
        int height,
        int size,
        Color color,
        bool bold = false,
        bool center = false)
    {
        var label = new Label(parent, name)
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont(bold ? "sourcesansproblack" : "sourcesanspro") ??
                   Skin.DefaultFont,
            FontSize = size,
            TextColorOverride = color,
            TextAlign = center ? Pos.Center : Pos.Left | Pos.CenterV,
            Text = text,
            MouseInputEnabled = false,
        };
        label.SetBounds(x, y, width, height);
        return label;
    }

    private static string Shorten(string? value, int maxLength)
    {
        var trimmed = (value ?? string.Empty)
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : trimmed[..Math.Max(0, maxLength - 3)] + "...";
    }

    private static string FormatDuration(long milliseconds)
    {
        if (milliseconds <= 0)
            return "NO RECORD";

        var duration = TimeSpan.FromMilliseconds(milliseconds);
        return duration.TotalHours >= 1
            ? $"{(long)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{(long)duration.TotalMinutes:00}:{duration.Seconds:00}";
    }

    private static string FormatLastClear(long unixMilliseconds)
    {
        if (unixMilliseconds <= 0)
            return "NEVER";

        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds)
                .ToLocalTime().ToString("yyyy-MM-dd");
        }
        catch (ArgumentOutOfRangeException)
        {
            return "NEVER";
        }
    }

    private static string BuildTransitionText(DungeonStatusEntry status)
    {
        if (status.NextChangeUnixMilliseconds <= 0)
            return status.Available ? "GATE OPEN" : "GATE SEALED";

        var next = DateTimeOffset.FromUnixTimeMilliseconds(status.NextChangeUnixMilliseconds);
        var remaining = next - DateTimeOffset.Now;
        if (remaining <= TimeSpan.Zero)
            return "Updating gate state...";

        var prefix = status.Available ? "Closes in" : "Opens in";
        if (remaining.TotalDays >= 1)
            return $"{prefix} {(int)remaining.TotalDays}d {remaining.Hours:00}h";
        if (remaining.TotalHours >= 1)
            return $"{prefix} {(int)remaining.TotalHours:00}h {remaining.Minutes:00}m";
        return $"{prefix} {Math.Max(0, remaining.Minutes):00}m";
    }

    private static Color RankColor(DungeonRank rank) =>
        rank switch
        {
            DungeonRank.S => new Color(a: 255, r: 228, g: 92, b: 92),
            DungeonRank.A => new Color(a: 255, r: 224, g: 142, b: 68),
            DungeonRank.B => new Color(a: 255, r: 211, g: 185, b: 74),
            DungeonRank.C => new Color(a: 255, r: 111, g: 191, b: 115),
            DungeonRank.D => new Color(a: 255, r: 87, g: 161, b: 207),
            DungeonRank.E => new Color(a: 255, r: 139, g: 139, b: 201),
            _ => new Color(a: 255, r: 175, g: 175, b: 175),
        };

    private static Color PodiumColor(int index) =>
        index switch
        {
            0 => new Color(a: 255, r: 245, g: 211, b: 112),
            1 => new Color(a: 255, r: 211, g: 216, b: 227),
            _ => new Color(a: 255, r: 214, g: 158, b: 118),
        };

    public void ShowWindow()
    {
        Show();
        BringToFront();
    }

    private enum ChromeStyle
    {
        Window,
        Ribbon,
        Card,
        Preview,
    }

    /// <summary>
    /// Draws a restrained pixel-art gold frame using rectangles. This uses
    /// no icon fonts or new art files, and stays compatible with Gwen skins.
    /// </summary>
    private sealed class DungeonChrome : Base
    {
        private static readonly Color OuterGold = new(a: 255, r: 158, g: 112, b: 51);
        private static readonly Color BrightGold = new(a: 255, r: 224, g: 179, b: 88);
        private static readonly Color InnerGold = new(a: 255, r: 100, g: 69, b: 40);
        private static readonly Color Darkest = new(a: 255, r: 24, g: 15, b: 14);
        private static readonly Color Dark = new(a: 255, r: 38, g: 23, b: 19);
        private static readonly Color Card = new(a: 255, r: 49, g: 31, b: 25);
        private static readonly Color Band = new(a: 255, r: 77, g: 46, b: 34);
        private static readonly Color Stripe = new(a: 255, r: 54, g: 35, b: 29);

        private readonly ChromeStyle _style;

        public DungeonChrome(Base parent, string name, ChromeStyle style) : base(parent, name)
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
                    Fill(skin, bounds, Darkest);
                    Border(skin, bounds, OuterGold, 2);
                    Border(skin, Inset(bounds, 5), InnerGold, 1);
                    Fill(skin, bounds.X + 14, bounds.Y + 6, 34, 2, BrightGold);
                    Fill(skin, bounds.Right - 48, bounds.Y + 6, 34, 2, BrightGold);
                    Fill(skin, bounds.X + 18, bounds.Y + 103, bounds.Width - 36, 1, InnerGold);
                    Fill(skin, bounds.X + 12, bounds.Bottom - 9, bounds.Width - 24, 2, InnerGold);
                    Corners(skin, bounds, BrightGold);
                    break;

                case ChromeStyle.Ribbon:
                    Fill(skin, bounds, Dark);
                    Border(skin, bounds, OuterGold, 1);
                    Fill(skin, bounds.X + 7, bounds.Y + 4, 3, bounds.Height - 8, BrightGold);
                    Fill(skin, bounds.Right - 10, bounds.Y + 4, 3, bounds.Height - 8, BrightGold);
                    break;

                case ChromeStyle.Card:
                    Fill(skin, bounds, Card);
                    Border(skin, bounds, OuterGold, 2);
                    Border(skin, Inset(bounds, 4), InnerGold, 1);
                    Corners(skin, bounds, BrightGold);

                    // Header and rank band
                    Fill(skin, bounds.X + 152, bounds.Y + 13, bounds.Width - 168, 34, Band);
                    Fill(skin, bounds.X + 152, bounds.Y + 45, bounds.Width - 168, 1, OuterGold);
                    Fill(skin, bounds.X + 654, bounds.Y + 13, 54, 34, Darkest);
                    Border(skin, new UiRectangle(bounds.X + 654, bounds.Y + 13, 54, 34), InnerGold, 1);
                    Fill(skin, bounds.X + 152, bounds.Y + 128, bounds.Width - 168, 1, OuterGold);

                    // Alternating quest, level, objective and reward rows
                    for (var index = 0; index < 4; index++)
                    {
                        var rowY = bounds.Y + 133 + index * 31;
                        Fill(skin, bounds.X + 17, rowY, bounds.Width - 34, 29,
                            index % 2 == 0 ? Dark : Stripe);
                        Fill(skin, bounds.X + 17, rowY + 29, bounds.Width - 34, 1, InnerGold);
                        Fill(skin, bounds.X + 35, rowY + 10, 9, 9, OuterGold);
                        Fill(skin, bounds.X + 38, rowY + 13, 3, 3, BrightGold);
                        Fill(skin, bounds.X + 184, rowY + 5, 1, 19, InnerGold);
                    }

                    // Personal statistics inset, separated from dungeon details.
                    var record = new UiRectangle(bounds.X + 17, bounds.Y + 266, bounds.Width - 34, 82);
                    Fill(skin, record, Darkest);
                    Border(skin, record, InnerGold, 1);
                    Fill(skin, bounds.X + 25, bounds.Y + 317, bounds.Width - 50, 1, InnerGold);
                    Fill(skin, bounds.X + 25, bounds.Y + 276, 5, 8, BrightGold);

                    // Independent Top 3 boxes for victories and fastest clears.
                    var first = new UiRectangle(bounds.X + 17, bounds.Y + 355, 341, 89);
                    var second = new UiRectangle(bounds.X + 366, bounds.Y + 355, 341, 89);
                    Fill(skin, first, Dark);
                    Fill(skin, second, Dark);
                    Border(skin, first, InnerGold, 1);
                    Border(skin, second, InnerGold, 1);
                    Fill(skin, first.X + 10, first.Y + 25, first.Width - 20, 1, InnerGold);
                    Fill(skin, second.X + 10, second.Y + 25, second.Width - 20, 1, InnerGold);
                    break;

                case ChromeStyle.Preview:
                    Fill(skin, bounds, Darkest);
                    Border(skin, bounds, BrightGold, 2);
                    Border(skin, Inset(bounds, 4), InnerGold, 1);

                    // A tiny dungeon entrance is the fallback for missing images.
                    Fill(skin, bounds.X + 14, bounds.Y + 17, 96, 74, new Color(a: 255, r: 61, g: 55, b: 53));
                    Fill(skin, bounds.X + 22, bounds.Y + 11, 80, 14, new Color(a: 255, r: 103, g: 91, b: 77));
                    Fill(skin, bounds.X + 31, bounds.Y + 30, 62, 64, new Color(a: 255, r: 39, g: 36, b: 36));
                    Fill(skin, bounds.X + 41, bounds.Y + 40, 42, 54, Darkest);
                    Fill(skin, bounds.X + 28, bounds.Y + 26, 8, 58, new Color(a: 255, r: 120, g: 101, b: 78));
                    Fill(skin, bounds.X + 88, bounds.Y + 26, 8, 58, new Color(a: 255, r: 120, g: 101, b: 78));
                    Fill(skin, bounds.X + 27, bounds.Y + 47, 6, 15, BrightGold);
                    Fill(skin, bounds.X + 91, bounds.Y + 47, 6, 15, BrightGold);
                    Fill(skin, bounds.X + 18, bounds.Y + 91, 88, 4, InnerGold);
                    break;
            }
        }

        private static UiRectangle Inset(UiRectangle rectangle, int size) =>
            new(rectangle.X + size, rectangle.Y + size,
                rectangle.Width - size * 2, rectangle.Height - size * 2);

        private static void Fill(SkinBase skin, UiRectangle rectangle, Color color) =>
            Fill(skin, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, color);

        private static void Fill(
            SkinBase skin, int x, int y, int width, int height, Color color)
        {
            if (width <= 0 || height <= 0)
                return;

            skin.Renderer.DrawColor = color;
            skin.Renderer.DrawFilledRect(new UiRectangle(x, y, width, height));
        }

        private static void Border(SkinBase skin, UiRectangle rectangle, Color color, int thickness)
        {
            Fill(skin, rectangle.X, rectangle.Y, rectangle.Width, thickness, color);
            Fill(skin, rectangle.X, rectangle.Bottom - thickness, rectangle.Width, thickness, color);
            Fill(skin, rectangle.X, rectangle.Y, thickness, rectangle.Height, color);
            Fill(skin, rectangle.Right - thickness, rectangle.Y, thickness, rectangle.Height, color);
        }

        private static void Corners(SkinBase skin, UiRectangle rectangle, Color color)
        {
            const int length = 14;
            Fill(skin, rectangle.X + 5, rectangle.Y + 5, length, 2, color);
            Fill(skin, rectangle.X + 5, rectangle.Y + 5, 2, length, color);
            Fill(skin, rectangle.Right - length - 5, rectangle.Y + 5, length, 2, color);
            Fill(skin, rectangle.Right - 7, rectangle.Y + 5, 2, length, color);
            Fill(skin, rectangle.X + 5, rectangle.Bottom - 7, length, 2, color);
            Fill(skin, rectangle.X + 5, rectangle.Bottom - length - 5, 2, length, color);
            Fill(skin, rectangle.Right - length - 5, rectangle.Bottom - 7, length, 2, color);
            Fill(skin, rectangle.Right - 7, rectangle.Bottom - length - 5, 2, length, color);
        }
    }
}
