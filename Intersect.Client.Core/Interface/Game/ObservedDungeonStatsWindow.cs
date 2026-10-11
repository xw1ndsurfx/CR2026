using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Framework.Core.Dungeons;
using Intersect.Network.Packets.Server;
using UiRectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

/// <summary>
/// Read-only per-dungeon history for another online player. Separate from the
/// owner's Dungeon Gates window so inspection cannot overwrite personal data.
/// </summary>
internal sealed class ObservedDungeonStatsWindow : Window
{
    private static readonly Color Gold = new(a: 255, r: 224, g: 190, b: 113);
    private static readonly Color Cream = new(a: 255, r: 246, g: 229, b: 202);
    private static readonly Color Muted = new(a: 255, r: 190, g: 175, b: 153);
    private static readonly Color Green = new(a: 255, r: 143, g: 212, b: 150);

    private readonly Label _subtitle;
    private readonly Label _summary;
    private readonly ScrollControl _scroll;

    public ObservedDungeonStatsWindow(Canvas parent)
        : base(parent, "Player Dungeon Records", false, nameof(ObservedDungeonStatsWindow))
    {
        SetSize(780, 650);
        Alignment = [Alignments.Center];
        DeleteOnClose = false;
        DisableResizing();

        var header = new RecordFrame(this, "DungeonInspectionHeader", header: true);
        header.SetBounds(21, 39, 738, 113);
        header.SendToBack();

        AddText(this, "DungeonInspectionTitle", "PLAYER DUNGEONS",
            42, 49, 694, 32, 18, Gold, bold: true, center: true);
        _subtitle = AddText(this, "DungeonInspectionName", string.Empty,
            42, 87, 694, 22, 11, Cream, bold: true, center: true);
        _summary = AddText(this, "DungeonInspectionSummary", string.Empty,
            42, 117, 694, 22, 10, Muted, center: true);

        _scroll = new ScrollControl(this, "DungeonInspectionScroll")
        {
            OverflowX = OverflowBehavior.Hidden,
            OverflowY = OverflowBehavior.Scroll,
            AutoHideBars = true,
        };
        _scroll.SetBounds(28, 165, 724, 449);

        Hide();
    }

    protected override void EnsureInitialized()
    {
    }

    public void Apply(DungeonStatePacket packet)
    {
        if (packet.InspectedPlayerId == Guid.Empty)
            return;

        _subtitle.Text = packet.InspectedPlayerName + "  |  VIEW ONLY";

        var records = (packet.PlayerStats ?? [])
            .Where(entry => entry != null && entry.DungeonId != Guid.Empty)
            .GroupBy(entry => entry.DungeonId)
            .ToDictionary(group => group.Key, group => group.First());

        var attempts = records.Values.Sum(record => record.Attempts);
        var completions = records.Values.Sum(record => record.Completions);
        var failures = records.Values.Sum(record => record.Failures);
        var deaths = records.Values.Sum(record => record.Deaths);
        _summary.Text = $"CLEARS {completions:N0}    RUNS {attempts:N0}    " +
            $"FAILURES {failures:N0}    DEATHS {deaths:N0}";

        _scroll.DeleteAll();

        var definitions = DungeonConfiguration.Instance.Dungeons
            .OrderBy(definition => definition.SortOrder)
            .ThenBy(definition => definition.Rank)
            .ThenBy(definition => definition.Name)
            .ToArray();

        var y = 10;
        foreach (var dungeon in definitions)
        {
            records.TryGetValue(dungeon.Id, out var record);
            record ??= new DungeonPlayerStatEntry { DungeonId = dungeon.Id };
            y = AddRecord(dungeon, record, y);
        }

        if (definitions.Length == 0)
        {
            AddText(_scroll, "DungeonInspectionEmpty",
                "No dungeons are currently configured.",
                30, 110, 640, 38, 12, Cream, center: true);
            y = 170;
        }

        _scroll.SetInnerSize(697, Math.Max(442, y + 12));
        _scroll.UpdateScrollBars();
    }

    private int AddRecord(DungeonDefinition dungeon, DungeonPlayerStatEntry record, int y)
    {
        const int width = 687;
        const int height = 169;

        var card = new RecordFrame(_scroll, "InspectedDungeon" + dungeon.Id, header: false);
        card.SetBounds(7, y, width, height);

        // Reuse the actual image configured for the dungeon, where available.
        if (!string.IsNullOrWhiteSpace(dungeon.Image))
        {
            var iconTexture = Globals.ContentManager?.GetTexture(TextureType.Dungeon, dungeon.Image);
            if (iconTexture != null)
            {
                var icon = new ImagePanel(card, "InspectedDungeonIcon" + dungeon.Id)
                {
                    MaintainAspectRatio = true,
                    ShouldDrawBackground = false,
                    MouseInputEnabled = false,
                    Texture = iconTexture,
                };
                icon.SetBounds(16, 13, 72, 51);
            }
        }

        AddText(card, "InspectedDungeonName" + dungeon.Id, dungeon.Name,
            103, 13, 465, 25, 13, Gold, bold: true);
        AddText(card, "InspectedDungeonRank" + dungeon.Id,
            "RANK " + dungeon.Rank,
            566, 13, 100, 25, 11, Gold, bold: true, center: true);
        AddText(card, "InspectedDungeonLocation" + dungeon.Id,
            string.IsNullOrWhiteSpace(dungeon.Location) ? "Location unspecified" : dungeon.Location,
            103, 39, 480, 20, 9, Muted);

        var started = record.Attempts > 0 || record.Completions > 0 ||
            record.Failures > 0 || record.Deaths > 0;

        var headings = new[] { "RUNS", "CLEARS", "FAILURES", "DEATHS" };
        var values = new[] { record.Attempts, record.Completions, record.Failures, record.Deaths };
        for (var i = 0; i < headings.Length; i++)
        {
            var x = 19 + i * 166;
            AddText(card, "DungeonInspectionMetricTitle" + i + dungeon.Id,
                headings[i], x, 71, 152, 19, 9, Muted);
            AddText(card, "DungeonInspectionMetricValue" + i + dungeon.Id,
                values[i].ToString("N0"), x, 90, 152, 25, 13, Cream, bold: true);
        }

        var clearRate = record.Attempts > 0
            ? 100.0 * record.Completions / record.Attempts : 0d;
        AddText(card, "DungeonInspectionBest" + dungeon.Id,
            "BEST: " + FormatDuration(record.BestClearTimeMilliseconds),
            19, 132, 205, 23, 9, Cream, bold: true);
        AddText(card, "DungeonInspectionLast" + dungeon.Id,
            "LAST CLEAR: " + FormatDate(record.LastCompletedUnixMilliseconds),
            238, 132, 246, 23, 9, Cream);
        AddText(card, "DungeonInspectionRate" + dungeon.Id,
            started ? $"CLEAR RATE: {clearRate:0.#}%" : "NOT STARTED",
            492, 132, 180, 23, 9, started ? Green : Muted,
            bold: true, center: true);

        return y + height + 12;
    }

    private Label AddText(Base parent, string name, string value,
        int x, int y, int width, int height, int fontSize, Color color,
        bool bold = false, bool center = false)
    {
        var label = new Label(parent, name)
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont(
                bold ? "sourcesansproblack" : "sourcesanspro") ?? Skin.DefaultFont,
            FontSize = fontSize,
            TextAlign = center ? Pos.Center : Pos.Left | Pos.CenterV,
            TextColorOverride = color,
            Text = value,
            MouseInputEnabled = false,
        };
        label.SetBounds(x, y, width, height);
        return label;
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

    private static string FormatDate(long unixMilliseconds)
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

    private sealed class RecordFrame : Base
    {
        private readonly bool _header;

        public RecordFrame(Base parent, string name, bool header) : base(parent, name)
        {
            _header = header;
            MouseInputEnabled = false;
            KeyboardInputEnabled = false;
        }

        protected override void Render(SkinBase skin)
        {
            var r = RenderBounds;
            void Fill(int x, int y, int w, int h, Color color)
            {
                if (w <= 0 || h <= 0)
                    return;
                skin.Renderer.DrawColor = color;
                skin.Renderer.DrawFilledRect(new UiRectangle(x, y, w, h));
            }

            var border = new Color(a: 255, r: 157, g: 110, b: 62);
            var background = _header
                ? new Color(a: 255, r: 51, g: 33, b: 27)
                : new Color(a: 255, r: 37, g: 23, b: 21);

            Fill(r.X, r.Y, r.Width, r.Height, background);
            Fill(r.X, r.Y, r.Width, 2, border);
            Fill(r.X, r.Bottom - 2, r.Width, 2, border);
            Fill(r.X, r.Y, 2, r.Height, border);
            Fill(r.Right - 2, r.Y, 2, r.Height, border);
            if (!_header)
                Fill(r.X + 12, r.Y + 66, r.Width - 24, 1,
                    new Color(a: 255, r: 91, g: 59, b: 38));
        }
    }
}
