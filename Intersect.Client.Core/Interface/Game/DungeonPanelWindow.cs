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

namespace Intersect.Client.Interface.Game;

internal sealed class DungeonPanelWindow : Window
{
    private readonly Label _summary;
    private readonly Label _personalSummary;
    private readonly ScrollControl _scroll;
    private Dictionary<Guid, DungeonPodiumEntry> _podiums = [];
    private long _nextPodiumRefreshAt;

    public DungeonPanelWindow(Canvas parent) : base(parent, "Dungeons", false, nameof(DungeonPanelWindow))
    {
        SetSize(820, 650);
        Alignment = [Alignments.Center];
        DeleteOnClose = false;
        DisableResizing();

        var title = new Label(this, "DungeonPanelTitle")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 22,
            TextAlign = Pos.Center,
            TextColorOverride = new Color(a: 255, r: 220, g: 196, b: 135),
            Text = "DUNGEON GATES",
        };
        title.SetBounds(20, 34, 780, 34);

        var subtitle = new Label(this, "DungeonPanelSubtitle")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 9,
            TextAlign = Pos.Center,
            TextColorOverride = new Color(a: 255, r: 188, g: 185, b: 178),
            Text = "ROYAL GATE REGISTRY • DETECTED DUNGEONS",
        };
        subtitle.SetBounds(20, 68, 780, 20);

        _summary = new Label(this, "DungeonPanelSummary")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 10,
            TextAlign = Pos.Center,
            TextColorOverride = Color.White,
        };
        _summary.SetBounds(20, 91, 780, 24);

        _personalSummary = new Label(this, "DungeonPanelPersonalSummary")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 9,
            TextAlign = Pos.Center,
            TextColorOverride = new Color(a: 255, r: 220, g: 196, b: 135),
        };
        _personalSummary.SetBounds(20, 115, 780, 20);

        var refresh = new Button(this, "DungeonPanelRefresh")
        {
            Text = "REFRESH",
            FontSize = 9,
        };
        refresh.SetBounds(685, 37, 102, 27);
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
        _scroll.SetBounds(24, 143, 772, 477);

        Hide();
    }

    protected override void EnsureInitialized()
    {
    }

    public void Apply(DungeonStatePacket packet)
    {
        DungeonConfiguration.Load(packet.ConfigurationJson);
        var statuses = (packet.Statuses ?? []).ToDictionary(status => status.DungeonId);
        var personalStats = (packet.PlayerStats ?? []).ToDictionary(entry => entry.DungeonId);
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
        _summary.Text = $"{openCount:N0} ACTIVE GATE{(openCount == 1 ? string.Empty : "S")}  •  {definitions.Length:N0} REGISTERED";
        var totalAttempts = personalStats.Values.Sum(entry => entry.Attempts);
        var totalCompletions = personalStats.Values.Sum(entry => entry.Completions);
        var totalFailures = personalStats.Values.Sum(entry => entry.Failures);
        var totalDeaths = personalStats.Values.Sum(entry => entry.Deaths);
        _personalSummary.Text = $"YOUR RECORD  •  {totalCompletions:N0} CLEARS / {totalAttempts:N0} RUNS" +
            $"  •  {totalFailures:N0} FAILURES  •  {totalDeaths:N0} DEATHS";

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
            var empty = new Label(_scroll, "NoDungeons")
            {
                AutoSizeToContents = false,
                Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
                FontSize = 11,
                TextAlign = Pos.Center,
                TextColorOverride = Color.White,
                Text = "No dungeon gates are registered yet.",
            };
            empty.SetBounds(20, 80, 710, 40);
            y = 140;
        }

        _scroll.SetInnerSize(744, Math.Max(480, y + 12));
        _scroll.UpdateScrollBars();
    }

    private int AddDungeonCard(
        DungeonDefinition dungeon,
        DungeonStatusEntry status,
        DungeonPlayerStatEntry record,
        DungeonPodiumEntry? podium,
        int y
    )
    {
        const int width = 724;
        const int height = 366;

        var card = new Button(_scroll, $"DungeonCard{dungeon.Id}")
        {
            Text = string.Empty,
            MouseInputEnabled = false,
        };
        card.SetBounds(8, y, width, height);
        card.SetStateTexture(ComponentState.Normal, "control_button.png");

        var image = new ImagePanel(card, $"DungeonImage{dungeon.Id}")
        {
            MaintainAspectRatio = true,
            MouseInputEnabled = false,
        };
        image.SetBounds(12, 14, 130, 100);

        if (!string.IsNullOrWhiteSpace(dungeon.Image))
        {
            var contentManager = Globals.ContentManager;
            image.Texture =
                contentManager?.GetTexture(TextureType.Dungeon, dungeon.Image) ??
                contentManager?.GetTexture(TextureType.Image, dungeon.Image);
            if (image.Texture == null)
                image.Hide();
        }
        else
        {
            image.Hide();
        }

        var rank = new Label(card, $"DungeonRank{dungeon.Id}")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 24,
            TextAlign = Pos.Center,
            TextColorOverride = RankColor(dungeon.Rank),
            Text = dungeon.Rank.ToString(),
            MouseInputEnabled = false,
        };
        rank.SetBounds(654, 10, 54, 42);

        var name = new Label(card, $"DungeonName{dungeon.Id}")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 13,
            TextColorOverride = Color.White,
            Text = dungeon.Name,
            MouseInputEnabled = false,
        };
        name.SetBounds(154, 12, 420, 24);

        var location = new Label(card, $"DungeonLocation{dungeon.Id}")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 8,
            TextColorOverride = new Color(a: 255, r: 188, g: 185, b: 178),
            Text = string.IsNullOrWhiteSpace(dungeon.Location)
                ? "Location unknown"
                : dungeon.Location,
            MouseInputEnabled = false,
        };
        location.SetBounds(154, 37, 420, 18);

        var description = new Label(card, $"DungeonDescription{dungeon.Id}")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 8,
            TextColorOverride = new Color(a: 255, r: 218, g: 214, b: 205),
            Text = dungeon.Description,
            MouseInputEnabled = false,
        };
        description.SetBounds(154, 57, 488, 32);

        var quest = dungeon.AssociatedQuestId == Guid.Empty
            ? null
            : QuestDescriptor.Get(dungeon.AssociatedQuestId);

        var questLabel = new Label(card, $"DungeonQuest{dungeon.Id}")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 8,
            TextColorOverride = new Color(a: 255, r: 220, g: 196, b: 135),
            Text = quest == null ? string.Empty : $"QUEST • {quest.Name}",
            MouseInputEnabled = false,
        };
        questLabel.SetBounds(154, 91, 488, 18);
        if (quest == null)
            questLabel.Hide();

        var maximumLevel = dungeon.MaximumLevel <= 0 ? "+" : $"-{dungeon.MaximumLevel}";
        var details = new Label(card, $"DungeonDetails{dungeon.Id}")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 8,
            TextColorOverride = Color.White,
            Text =
                $"LEVEL {dungeon.MinimumLevel}{maximumLevel}  •  RECOMMENDED {dungeon.RecommendedLevel}+  •  " +
                $"PARTY {dungeon.MinimumPartySize}-{dungeon.MaximumPartySize}" +
                (dungeon.TimeLimitMinutes > 0 ? $"  •  {dungeon.TimeLimitMinutes} MIN" : string.Empty) +
                $"  •  LIVES {Math.Max(1, dungeon.MaxLives)}" +
                (dungeon.PremiumRequired ? "  •  PREMIUM" : string.Empty),
            MouseInputEnabled = false,
        };
        details.SetBounds(154, 113, 540, 18);

        var requirements = dungeon.CompletionRequirements == DungeonCompletionRequirement.None
            ? DungeonCompletionRequirement.DefeatFinalBoss
            : dungeon.CompletionRequirements;
        var objectiveParts = new List<string>();
        if ((requirements & DungeonCompletionRequirement.DefeatFinalBoss) != 0)
            objectiveParts.Add("FINAL BOSS");
        if ((requirements & DungeonCompletionRequirement.DefeatAllMonsters) != 0)
            objectiveParts.Add("ALL MONSTERS");

        var objective = new Label(card, $"DungeonObjective{dungeon.Id}")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 8,
            TextColorOverride = new Color(a: 255, r: 190, g: 178, b: 150),
            Text = $"OBJECTIVE • {string.Join(" + ", objectiveParts)}",
            MouseInputEnabled = false,
        };
        objective.SetBounds(154, 135, 540, 18);

        var rewardItem = dungeon.CompletionItemId == Guid.Empty
            ? null
            : ItemDescriptor.Get(dungeon.CompletionItemId);
        var rewardParts = new List<string>();
        if (dungeon.CompletionExperience > 0)
            rewardParts.Add($"{dungeon.CompletionExperience:N0} EXP");
        if (rewardItem != null && dungeon.CompletionItemQuantity > 0)
            rewardParts.Add($"{rewardItem.Name} x{dungeon.CompletionItemQuantity:N0}");

        var rewards = new Label(card, $"DungeonRewards{dungeon.Id}")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 8,
            TextColorOverride = new Color(a: 255, r: 220, g: 196, b: 135),
            Text = rewardParts.Count > 0
                ? $"REWARDS • {string.Join(" • ", rewardParts)}"
                : "REWARDS • None configured",
            MouseInputEnabled = false,
        };
        rewards.SetBounds(154, 157, 540, 18);

        var personalRecord = new Label(card, $"DungeonPersonalRecord{dungeon.Id}")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 8,
            TextColorOverride = new Color(a: 255, r: 186, g: 218, b: 240),
            Text = $"YOUR RECORD • {record.Attempts:N0} RUNS  •  {record.Completions:N0} CLEARS" +
                   $"  •  {record.Failures:N0} FAILURES  •  {record.Deaths:N0} DEATHS",
            MouseInputEnabled = false,
        };
        personalRecord.SetBounds(154, 181, 540, 18);

        var clearRate = record.Attempts > 0 ? 100.0 * record.Completions / record.Attempts : 0.0;
        var personalBest = new Label(card, $"DungeonPersonalBest{dungeon.Id}")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 8,
            TextColorOverride = new Color(a: 255, r: 210, g: 208, b: 199),
            Text = $"BEST TIME • {FormatDuration(record.BestClearTimeMilliseconds)}" +
                   $"  •  LAST CLEAR • {FormatLastClear(record.LastCompletedUnixMilliseconds)}" +
                   $"  •  CLEAR RATE • {clearRate:0}%",
            MouseInputEnabled = false,
        };
        personalBest.SetBounds(154, 201, 540, 18);

        var statusLabel = new Label(card, $"DungeonStatus{dungeon.Id}")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 10,
            TextColorOverride = status.Available
                ? new Color(a: 255, r: 118, g: 210, b: 118)
                : new Color(a: 255, r: 205, g: 92, b: 82),
            Text = status.Available ? "● AVAILABLE" : "◆ SEALED",
            MouseInputEnabled = false,
        };
        statusLabel.SetBounds(154, 227, 150, 20);

        var transition = new Label(card, $"DungeonTransition{dungeon.Id}")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 8,
            TextAlign = Pos.Right,
            TextColorOverride = new Color(a: 255, r: 220, g: 196, b: 135),
            Text = BuildTransitionText(status),
            MouseInputEnabled = false,
        };
        transition.SetBounds(310, 227, 382, 20);

        AddPodiumColumn(
            card,
            dungeon.Id,
            "Clears",
            "TOP 3 • MOST CLEARS",
            154,
            podium?.TopClears ?? [],
            fastest: false
        );
        AddPodiumColumn(
            card,
            dungeon.Id,
            "Fastest",
            "TOP 3 • FASTEST CLEARS",
            432,
            podium?.FastestClears ?? [],
            fastest: true
        );

        return y + height + 10;
    }

    private void AddPodiumColumn(
        Button card,
        Guid dungeonId,
        string key,
        string header,
        int x,
        DungeonPodiumPlayerEntry[] entries,
        bool fastest
    )
    {
        var title = new Label(card, $"DungeonPodiumHeader{key}{dungeonId}")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 9,
            TextColorOverride = new Color(a: 255, r: 223, g: 191, b: 120),
            Text = header,
            MouseInputEnabled = false,
        };
        title.SetBounds(x, 257, 266, 20);

        if (entries.Length == 0)
        {
            var empty = new Label(card, $"DungeonPodiumEmpty{key}{dungeonId}")
            {
                AutoSizeToContents = false,
                Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
                FontSize = 8,
                TextColorOverride = new Color(a: 255, r: 169, g: 164, b: 154),
                Text = "No champions yet.",
                MouseInputEnabled = false,
            };
            empty.SetBounds(x, 287, 260, 20);
            return;
        }

        for (var index = 0; index < Math.Min(3, entries.Length); ++index)
        {
            var entry = entries[index];
            if (entry == null)
                continue;

            var score = fastest
                ? FormatDuration(entry.BestClearTimeMilliseconds)
                : $"{entry.Completions:N0} CLEARS";

            var row = new Label(card, $"DungeonPodium{key}{dungeonId}Rank{index}")
            {
                AutoSizeToContents = false,
                Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
                FontSize = 8,
                TextColorOverride = PodiumColor(index),
                Text = $"#{index + 1}  {ShortPlayerName(entry.PlayerName)}  •  {score}",
                MouseInputEnabled = false,
            };
            row.SetBounds(x, 282 + index * 24, 267, 20);
        }
    }

    private static string ShortPlayerName(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        return trimmed.Length > 17 ? trimmed[..14] + "..." : trimmed;
    }

    private static Color PodiumColor(int index) =>
        index switch
        {
            0 => new Color(a: 255, r: 246, g: 207, b: 95),
            1 => new Color(a: 255, r: 195, g: 201, b: 218),
            _ => new Color(a: 255, r: 204, g: 149, b: 106),
        };

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
            return status.StatusText;

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

    public void ShowWindow()
    {
        Show();
        BringToFront();
    }
}
