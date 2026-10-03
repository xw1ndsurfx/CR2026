using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Framework.Core.Dungeons;
using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

internal sealed class DungeonPanelWindow : Window
{
    private readonly Label _summary;
    private readonly ScrollControl _scroll;

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

        _scroll = new ScrollControl(this, "DungeonPanelScroll")
        {
            OverflowX = OverflowBehavior.Hidden,
            OverflowY = OverflowBehavior.Scroll,
            AutoHideBars = true,
        };
        _scroll.SetBounds(24, 120, 772, 500);

        Hide();
    }

    protected override void EnsureInitialized()
    {
    }

    public void Apply(DungeonStatePacket packet)
    {
        DungeonConfiguration.Load(packet.ConfigurationJson);
        var statuses = (packet.Statuses ?? []).ToDictionary(status => status.DungeonId);
        var definitions = DungeonConfiguration.Instance.Dungeons
            .OrderBy(dungeon => dungeon.SortOrder)
            .ThenBy(dungeon => dungeon.Rank)
            .ThenBy(dungeon => dungeon.Name)
            .ToArray();

        var openCount = statuses.Values.Count(status => status.Available);
        _summary.Text = $"{openCount:N0} ACTIVE GATE{(openCount == 1 ? string.Empty : "S")}  •  {definitions.Length:N0} REGISTERED";

        _scroll.DeleteAll();

        var y = 8;
        foreach (var dungeon in definitions)
        {
            statuses.TryGetValue(dungeon.Id, out var status);
            status ??= new DungeonStatusEntry(dungeon.Id, false, "SEALED", 0);
            y = AddDungeonCard(dungeon, status, y);
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

    private int AddDungeonCard(DungeonDefinition dungeon, DungeonStatusEntry status, int y)
    {
        const int width = 724;
        const int height = 174;

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
            image.Texture = Globals.ContentManager.GetTexture(TextureType.Image, dungeon.Image);
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
                (dungeon.TimeLimitMinutes > 0 ? $"  •  {dungeon.TimeLimitMinutes} MIN" : string.Empty),
            MouseInputEnabled = false,
        };
        details.SetBounds(154, 113, 540, 18);

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
        statusLabel.SetBounds(154, 141, 150, 20);

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
        transition.SetBounds(310, 141, 382, 20);

        return y + height + 10;
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
