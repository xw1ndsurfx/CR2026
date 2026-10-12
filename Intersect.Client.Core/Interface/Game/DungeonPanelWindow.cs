using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Framework.Core.Dungeons;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.Network.Packets.Server;
using Intersect.Utilities;
using UiRectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

/// <summary>
/// Compact Dungeon Gates registry based on Admurin's art. The carousel selects
/// one dungeon; Details, Record and Rankings describe that dungeon only.
/// All statistics and availability values come from DungeonStatePacket.
/// </summary>
internal sealed class DungeonPanelWindow : Window
{
    private static readonly Color Gold = new(a: 255, r: 224, g: 189, b: 116);
    private static readonly Color Cream = new(a: 255, r: 246, g: 229, b: 198);
    private static readonly Color Muted = new(a: 255, r: 169, g: 149, b: 125);
    private static readonly Color Good = new(a: 255, r: 132, g: 226, b: 154);
    private static readonly Color Bad = new(a: 255, r: 215, g: 124, b: 111);

    private sealed class DataFrame : Base
    {
        public DataFrame(Base parent, string name) : base(parent, name)
        {
            MouseInputEnabled = false;
        }

        protected override void Render(SkinBase skin)
        {
            var bounds = RenderBounds;
            var renderer = skin.Renderer;
            renderer.DrawColor = new Color(a: 255, r: 170, g: 142, b: 82);
            renderer.DrawFilledRect(bounds);
            renderer.DrawColor = new Color(a: 255, r: 12, g: 18, b: 9);
            renderer.DrawFilledRect(new UiRectangle(
                bounds.X + 2, bounds.Y + 2, bounds.Width - 4, bounds.Height - 4));
        }
    }

    private readonly Label _summary;
    private readonly Label _personalSummary;
    private readonly Base _gateStrip;
    private readonly DataFrame _details;
    private readonly DataFrame _record;
    private readonly DataFrame _clearsRanking;
    private readonly DataFrame _fastestRanking;
    private readonly Button _previous;
    private readonly Button _next;
    private readonly List<(int Index, ImagePanel? Frame, ImagePanel? Icon, Label Name, Label Location)> _gateCards = [];

    private DungeonDefinition[] _definitions = [];
    private Dictionary<Guid, DungeonStatusEntry> _statuses = [];
    private Dictionary<Guid, DungeonPlayerStatEntry> _playerStats = [];
    private Dictionary<Guid, DungeonPodiumEntry> _podiums = [];
    private Guid _selectedId;
    private int _pageStart;
    private long _nextPodiumRefreshAt;

    public DungeonPanelWindow(Canvas parent)
        : base(parent, "Dungeons", false, nameof(DungeonPanelWindow))
    {
        SetSize(506, 689);
        Alignment = [Alignments.Center];
        DeleteOnClose = false;
        DisableResizing();

        // The complete frame is one authored 544x725 sprite. Render it as one
        // image, not on top of an extra hand-painted competing frame.
        if (AdmurinGuiArt.Add(this, "AdmurinDungeonFrame", "dungeon_gates_0a",
                10, 26, 486, 646) == null)
        {
            var fallback = new DataFrame(this, "DungeonFallbackFrame");
            fallback.SetBounds(10, 26, 486, 646);
        }

        AddText(this, "Heading", "DUNGEON GATES", 72, 43, 361, 26, 20, Gold, centered: true);
        AddText(this, "Subtitle", "ROYAL GATE REGISTRY - DETECTED DUNGEONS",
            47, 72, 412, 16, 9, Cream, centered: true);
        _summary = AddText(this, "Summary", "", 55, 89, 399, 15, 9, Cream, centered: true);

        var ribbon = new DataFrame(this, "RecordRibbon");
        ribbon.SetBounds(38, 108, 430, 23);
        _personalSummary = AddText(ribbon, "RecordText", "", 9, 2, 413, 18, 9, Gold, centered: true);

        _gateStrip = new Base(this, "DungeonGateStrip");
        _gateStrip.SetBounds(54, 140, 398, 83);
        // Keep the strip hit-testable so its three gate buttons receive clicks.

        _previous = new Button(this, "DungeonPagePrevious") { Text = string.Empty };
        _previous.SetBounds(21, 164, 29, 33);
        AdmurinGuiArt.Add(_previous, "PreviousArrow", "dungeon_gates_4",
            4, 0, 18, 32, 0, 0, 16, 32);
        _previous.Clicked += (_, _) => ChangePage(-3);

        _next = new Button(this, "DungeonPageNext") { Text = string.Empty };
        _next.SetBounds(459, 164, 28, 33);
        var nextArrow = AdmurinGuiArt.Add(_next, "NextArrow", "dungeon_gates_4",
            4, 0, 18, 32, 0, 0, 16, 32);
        // The artist supplied left arrows; mirrored UVs produce the right arrow.
        nextArrow?.SetUVs(16f / 64f, 0f, 0f, 1f);
        _next.Clicked += (_, _) => ChangePage(3);

        AddText(this, "DetailsHeading", "DETAILS", 61, 224, 388, 21, 17, Gold, centered: true);
        _details = new DataFrame(this, "DungeonDetails");
        _details.SetBounds(38, 248, 430, 154);

        AddText(this, "RecordHeading", "RECORD", 62, 404, 386, 21, 17, Gold, centered: true);
        _record = new DataFrame(this, "DungeonRecord");
        _record.SetBounds(38, 429, 430, 111);

        AddText(this, "RankingHeading", "RANKINGS", 62, 541, 386, 20, 17, Gold, centered: true);
        _clearsRanking = new DataFrame(this, "MostClears");
        _clearsRanking.SetBounds(38, 567, 208, 91);
        _fastestRanking = new DataFrame(this, "Fastest");
        _fastestRanking.SetBounds(259, 567, 209, 91);

        var refresh = new Button(this, "DungeonRankingsRefresh") { Text = string.Empty };
        refresh.SetBounds(443, 542, 25, 24);
        AdmurinGuiArt.Add(refresh, "RefreshIcon", "dungeon_gates_3",
            0, 0, 24, 24, 48, 0, 24, 24);
        refresh.SetToolTipText("Refresh dungeon rankings");
        refresh.Clicked += (_, _) =>
        {
            var now = Timing.Global.Milliseconds;
            if (now < _nextPodiumRefreshAt)
                return;
            _nextPodiumRefreshAt = now + 3_000;
            Networking.PacketSender.SendRequestDungeonPanel(openWindow: true);
        };

        Hide();
    }

    public void Apply(DungeonStatePacket packet)
    {
        DungeonConfiguration.Load(packet.ConfigurationJson);
        _definitions = (DungeonConfiguration.Instance.Dungeons ?? [])
            .OrderBy(dungeon => dungeon.SortOrder)
            .ThenBy(dungeon => dungeon.Rank)
            .ThenBy(dungeon => dungeon.Name)
            .ToArray();

        _statuses = (packet.Statuses ?? [])
            .Where(entry => entry != null)
            .GroupBy(entry => entry.DungeonId)
            .ToDictionary(group => group.Key, group => group.First());
        _playerStats = (packet.PlayerStats ?? [])
            .Where(entry => entry != null)
            .GroupBy(entry => entry.DungeonId)
            .ToDictionary(group => group.Key, group => group.First());

        // A null podium list means a background status packet; keep the last
        // manually requested leaderboard rather than erasing it.
        if (packet.Podiums != null)
        {
            _podiums = packet.Podiums
                .Where(entry => entry != null && entry.DungeonId != Guid.Empty)
                .GroupBy(entry => entry.DungeonId)
                .ToDictionary(group => group.Key, group => group.First());
        }

        var openCount = _definitions.Count(dungeon =>
            _statuses.TryGetValue(dungeon.Id, out var status) && status.Available);
        _summary.Text = $"{openCount} ACTIVE GATE{(openCount == 1 ? "" : "S")}  /  {_definitions.Length} REGISTERED";

        var clears = _playerStats.Values.Sum(value => value.Completions);
        var attempts = _playerStats.Values.Sum(value => value.Attempts);
        var failures = _playerStats.Values.Sum(value => value.Failures);
        var deaths = _playerStats.Values.Sum(value => value.Deaths);
        _personalSummary.Text =
            $"YOUR RECORD  {clears:N0} CLEARS / {attempts:N0} RUNS / {failures:N0} FAILURES / {deaths:N0} DEATHS";

        var selectedIndex = Array.FindIndex(_definitions, definition => definition.Id == _selectedId);
        if (selectedIndex < 0)
        {
            selectedIndex = 0;
            _selectedId = _definitions.Length > 0 ? _definitions[0].Id : Guid.Empty;
        }
        _pageStart = selectedIndex / 3 * 3;

        BuildGateStrip();
        RenderSelection();
    }

    private void ChangePage(int offset)
    {
        if (_definitions.Length == 0)
            return;

        var lastPage = (_definitions.Length - 1) / 3 * 3;
        _pageStart = Math.Clamp(_pageStart + offset, 0, lastPage);
        _selectedId = _definitions[_pageStart].Id;
        BuildGateStrip();
        RenderSelection();
    }

    private void BuildGateStrip()
    {
        _gateStrip.DeleteAll();
        _gateCards.Clear();

        _previous.IsDisabled = _pageStart == 0;
        _next.IsDisabled = _pageStart + 3 >= _definitions.Length;

        for (var slot = 0; slot < 3 && _pageStart + slot < _definitions.Length; slot++)
        {
            var index = _pageStart + slot;
            var definition = _definitions[index];
            var available = _statuses.TryGetValue(definition.Id, out var status) && status.Available;
            var button = new Button(_gateStrip, $"GateChoice{index}") { Text = string.Empty };
            button.SetBounds(slot * 129 + 5, 0, 117, 81);
            button.SetToolTipText(string.IsNullOrWhiteSpace(definition.Description)
                ? definition.Name : definition.Name + "\n" + Shorten(definition.Description, 170));

            var frame = AdmurinGuiArt.Add(button, $"GateCardArt{index}", "dungeon_gates_2",
                0, 0, 117, 81);

            ImagePanel? gateIcon = null;
            if (!string.IsNullOrWhiteSpace(definition.Image))
            {
                var image = GameContentManager.Current.GetTexture(TextureType.Dungeon, definition.Image) ??
                            GameContentManager.Current.GetTexture(TextureType.Image, definition.Image);
                if (image != null)
                {
                    gateIcon = new ImagePanel(button, $"GateCustomIcon{index}")
                    {
                        Texture = image,
                        MouseInputEnabled = false,
                        MaintainAspectRatio = true,
                        ShouldDrawBackground = false,
                    };
                    gateIcon.SetBounds(44, 6, 30, 29);
                }
            }

            if (gateIcon == null)
                gateIcon = AdmurinGuiArt.Add(button, $"GateDefaultIcon{index}",
                    "dungeon_gates_1", 43, 7, 31, 30,
                    available ? 0 : 32, 0, 32, 32);

            var name = AddText(button, $"GateName{index}",
                Shorten(definition.Name, 17), 5, 43, 106, 15, 9, Cream, centered: true);
            var location = AddText(button, $"GateLocation{index}",
                Shorten(definition.Location, 18), 5, 58, 106, 14, 8, Gold, centered: true);

            button.Clicked += (_, _) =>
            {
                _selectedId = definition.Id;
                RenderSelection();
            };
            _gateCards.Add((index, frame, gateIcon, name, location));
        }
        HighlightSelection();
    }

    private void HighlightSelection()
    {
        foreach (var gate in _gateCards)
        {
            var definition = _definitions[gate.Index];
            var selected = definition.Id == _selectedId;
            var available = _statuses.TryGetValue(definition.Id, out var status) && status.Available;
            if (gate.Frame != null)
            {
                gate.Frame.RenderColor = selected
                    ? Color.White
                    : new Color(a: 255, r: 157, g: 135, b: 125);
            }

            if (gate.Icon != null && string.IsNullOrWhiteSpace(definition.Image))
            {
                gate.Icon.SetTextureRect(selected && available ? 0 : available ? 64 : 32, 0, 32, 32);
            }
            gate.Name.TextColorOverride = selected ? Gold : Muted;
            gate.Location.TextColorOverride = selected ? Cream : Muted;
        }
    }

    private void RenderSelection()
    {
        HighlightSelection();
        _details.DeleteAll();
        _record.DeleteAll();
        _clearsRanking.DeleteAll();
        _fastestRanking.DeleteAll();

        var selected = _definitions.FirstOrDefault(definition => definition.Id == _selectedId);
        if (selected == null)
        {
            AddText(_details, "Empty", "No dungeon gates have been registered.", 12, 55, 406, 28,
                11, Cream, centered: true);
            return;
        }

        _statuses.TryGetValue(selected.Id, out var status);
        status ??= new DungeonStatusEntry(selected.Id, false, "SEALED", 0);
        _playerStats.TryGetValue(selected.Id, out var record);
        record ??= new DungeonPlayerStatEntry { DungeonId = selected.Id };
        _podiums.TryGetValue(selected.Id, out var podium);

        var associatedQuest = selected.AssociatedQuestId == Guid.Empty
            ? null : QuestDescriptor.Get(selected.AssociatedQuestId);
        var requiredQuest = selected.RequiredQuestInProgressId == Guid.Empty
            ? null : QuestDescriptor.Get(selected.RequiredQuestInProgressId);
        var questName = requiredQuest?.Name ?? associatedQuest?.Name ?? "NONE";

        var requirements = selected.CompletionRequirements == DungeonCompletionRequirement.None
            ? DungeonCompletionRequirement.DefeatFinalBoss
            : selected.CompletionRequirements;
        var objectives = new List<string>();
        if ((requirements & DungeonCompletionRequirement.DefeatFinalBoss) != 0)
            objectives.Add("Final Boss");
        if ((requirements & DungeonCompletionRequirement.DefeatAllMonsters) != 0)
            objectives.Add("All Monsters");

        var rewardParts = new List<string>();
        if (selected.CompletionExperience > 0)
            rewardParts.Add($"{selected.CompletionExperience:N0} EXP");
        if (selected.CompletionItemId != Guid.Empty && selected.CompletionItemQuantity > 0)
            rewardParts.Add($"{ItemDescriptor.GetName(selected.CompletionItemId)} x{selected.CompletionItemQuantity:N0}");

        var levelRange = selected.MaximumLevel > 0
            ? $"{selected.MinimumLevel}-{selected.MaximumLevel}"
            : $"{selected.MinimumLevel}+";
        var rows = new (string Heading, string Value, Color Tint)[]
        {
            ("NAME", selected.Name, Cream),
            ("LOCATION", string.IsNullOrWhiteSpace(selected.Location) ? "UNKNOWN" : selected.Location, Cream),
            ("STATUS", status.Available ? "AVAILABLE" : "SEALED - " + status.StatusText,
                status.Available ? Good : Bad),
            ("TIER", selected.Rank.ToString(), Cream),
            ("QUEST REQUIRED", questName, Cream),
            ("LEVEL", $"{levelRange} / RECOMMENDED {selected.RecommendedLevel}+", Cream),
            ("PARTY", $"{selected.MinimumPartySize}-{selected.MaximumPartySize}", Cream),
            ("LIVES", Math.Max(1, selected.MaxLives).ToString(), Cream),
            ("OBJECTIVE", objectives.Count > 0 ? string.Join(" / ", objectives) : "NONE", Cream),
            ("REWARDS", rewardParts.Count > 0 ? string.Join(" / ", rewardParts) : "NONE", Cream),
        };
        for (var index = 0; index < rows.Length; index++)
        {
            var (heading, value, tint) = rows[index];
            var y = 6 + index * 14;
            AddText(_details, $"DetailHeading{index}", heading, 7, y, 144, 14, 9, Gold);
            AddText(_details, $"DetailValue{index}", Shorten(value, 45),
                158, y, 263, 14, 9, tint);
        }

        var clearRate = record.Attempts > 0
            ? 100.0 * record.Completions / record.Attempts : 0;
        var recordRows = new (string Heading, string Value)[]
        {
            ("BEST TIME", FormatDuration(record.BestClearTimeMilliseconds)),
            ("CLEARS", record.Completions.ToString("N0")),
            ("RUNS", record.Attempts.ToString("N0")),
            ("FAILURES", record.Failures.ToString("N0")),
            ("DEATHS", record.Deaths.ToString("N0")),
            ("LAST CLEAR", FormatLastClear(record.LastCompletedUnixMilliseconds)),
            ("CLEAR RATE", $"{clearRate:0}%"),
        };
        for (var index = 0; index < recordRows.Length; index++)
        {
            var (heading, value) = recordRows[index];
            AddText(_record, $"RecordHeading{index}", heading, 7, 5 + index * 14, 151, 14, 9, Gold);
            AddText(_record, $"RecordValue{index}", value, 163, 5 + index * 14, 258, 14, 9, Cream);
        }

        DrawRanking(_clearsRanking, "CLEARS", podium?.TopClears ?? [], fastest: false);
        DrawRanking(_fastestRanking, "FASTEST", podium?.FastestClears ?? [], fastest: true);
    }

    private static void DrawRanking(
        Base parent, string identifier, DungeonPodiumPlayerEntry[] players, bool fastest)
    {
        AddText(parent, identifier + "Heading", fastest ? "RANK   FASTEST CLEARS    TIME" :
            "RANK    MOST CLEARS     SCORE", 7, 3, 192, 14, 8, Gold);
        if (players.Length == 0)
        {
            AddText(parent, identifier + "Empty", "No records yet.", 8, 30, 188, 18, 9, Muted);
            return;
        }

        for (var index = 0; index < Math.Min(3, players.Length); index++)
        {
            var player = players[index];
            if (player == null)
                continue;
            AddText(parent, identifier + "Rank" + index,
                (index + 1).ToString(), 8, 21 + index * 21, 20, 18, 9, Cream);
            AddText(parent, identifier + "Player" + index,
                Shorten(player.PlayerName, 12), 36, 21 + index * 21, 106, 18, 9, Cream);
            AddText(parent, identifier + "Score" + index,
                fastest ? FormatDuration(player.BestClearTimeMilliseconds) : player.Completions.ToString("N0"),
                139, 21 + index * 21, 60, 18, 9, Cream, centered: true);
        }
    }

    private static Label AddText(
        Base parent, string id, string content,
        int x, int y, int width, int height, int size, Color color, bool centered = false)
    {
        var label = new Label(parent, id)
        {
            Text = content,
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("empire7") ??
                   GameContentManager.Current.GetFont("sourcesansproblack") ?? parent.Skin.DefaultFont,
            FontSize = size,
            TextColorOverride = color,
            TextAlign = centered ? Pos.Center : Pos.Left | Pos.CenterV,
            MouseInputEnabled = false,
        };
        label.SetBounds(x, y, width, height);
        return label;
    }

    private static string Shorten(string? text, int maximum)
    {
        var value = (text ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
        return value.Length <= maximum ? value : value[..Math.Max(0, maximum - 3)] + "...";
    }

    private static string FormatDuration(long milliseconds)
    {
        if (milliseconds <= 0)
            return "--:--";
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

    public void ShowWindow()
    {
        Show();
        BringToFront();
    }
}
