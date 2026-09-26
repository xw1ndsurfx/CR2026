using System.Text;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Enums;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

internal sealed class PlayerProfileWindow : Window
{
    private readonly Label _headline;
    private readonly Label _subtitle;
    private readonly ScrollControl _scroll;

    public PlayerProfileWindow(Canvas parent) : base(parent, "Character Information", false, nameof(PlayerProfileWindow))
    {
        SetSize(620, 610);
        Alignment = [Alignments.Center];
        DeleteOnClose = false;
        DisableResizing();

        _headline = new Label(this, "ProfileHeadline")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 22,
            TextAlign = Pos.Center,
            TextColorOverride = Color.White,
        };
        _headline.SetBounds(20, 38, 580, 34);

        _subtitle = new Label(this, "ProfileSubtitle")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 10,
            TextAlign = Pos.Center,
            TextColorOverride = new Color(a: 255, r: 205, g: 195, b: 175),
        };
        _subtitle.SetBounds(20, 74, 580, 44);

        _scroll = new ScrollControl(this, "ProfileScroll")
        {
            OverflowX = OverflowBehavior.Hidden,
            OverflowY = OverflowBehavior.Scroll,
            AutoHideBars = true,
        };
        _scroll.SetBounds(28, 124, 564, 446);

        Hide();
    }

    protected override void EnsureInitialized()
    {
    }

    public void Apply(PlayerProfilePacket profile)
    {
        if (!profile.Found)
            return;

        _headline.Text = profile.Name;
        _subtitle.Text =
            $"Level {profile.Level:N0}  •  {profile.ClassName}" +
            (string.IsNullOrWhiteSpace(profile.GuildName) ? string.Empty : $"  •  Guild: {profile.GuildName}") +
            (string.IsNullOrWhiteSpace(profile.MapName) ? string.Empty : $"\nLocation: {profile.MapName}");

        _scroll.DeleteAll();

        var y = 8;
        y = AddSection("CHARACTER", BuildCharacterText(profile), y);
        y = AddSection("STATS", BuildStatsText(profile), y);
        y = AddSection("EQUIPMENT", BuildEquipmentText(profile), y);
        y = AddSection("PROFESSIONS", BuildProfessionsText(profile), y);

        _scroll.SetInnerSize(540, Math.Max(430, y + 16));
        _scroll.UpdateScrollBars();

        Show();
        BringToFront();
    }

    private int AddSection(string title, string body, int y)
    {
        var titleLabel = new Label(_scroll, title + "Title")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 13,
            TextColorOverride = new Color(a: 255, r: 236, g: 210, b: 153),
            Text = title,
        };
        titleLabel.SetBounds(8, y, 510, 24);
        y += 26;

        var lineCount = Math.Max(1, body.Count(ch => ch == '\n') + 1);
        var bodyHeight = Math.Max(26, lineCount * 20 + 8);
        var bodyLabel = new Label(_scroll, title + "Body")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 10,
            TextColorOverride = Color.White,
            Text = body,
        };
        bodyLabel.SetBounds(18, y, 500, bodyHeight);

        return y + bodyHeight + 16;
    }

    private static string BuildCharacterText(PlayerProfilePacket profile)
    {
        var next = profile.ExperienceToNextLevel < 0
            ? "MAX"
            : profile.ExperienceToNextLevel.ToString("N0");

        return
            $"Name: {profile.Name}\n" +
            $"Level: {profile.Level:N0}\n" +
            $"Class: {profile.ClassName}\n" +
            $"Guild: {(string.IsNullOrWhiteSpace(profile.GuildName) ? "None" : profile.GuildName)}\n" +
            $"Experience: {profile.Experience:N0} / {next}";
    }

    private static string BuildStatsText(PlayerProfilePacket profile)
    {
        if (profile.Stats == null || profile.Stats.Length == 0)
            return "No stat information.";

        var builder = new StringBuilder();
        var stats = Enum.GetValues<Stat>();

        for (var index = 0; index < stats.Length && index < profile.Stats.Length; ++index)
        {
            var stat = stats[index];
            if (builder.Length > 0) builder.AppendLine();
            builder.Append($"{Strings.Combat.Stats[stat]}: {profile.Stats[index]:N0}");
        }

        return builder.ToString();
    }

    private static string BuildEquipmentText(PlayerProfilePacket profile)
    {
        if (profile.Equipment == null || profile.Equipment.Length == 0)
            return "No equipment information.";

        var builder = new StringBuilder();
        foreach (var equipment in profile.Equipment.OrderBy(entry => entry.SlotIndex))
        {
            if (builder.Length > 0) builder.AppendLine();
            builder.Append($"{equipment.SlotName}: {(equipment.ItemId == Guid.Empty ? "Empty" : equipment.ItemName)}");
        }

        return builder.ToString();
    }

    private static string BuildProfessionsText(PlayerProfilePacket profile)
    {
        if (profile.Professions == null || profile.Professions.Length == 0)
            return "No professions learned.";

        var builder = new StringBuilder();
        foreach (var profession in profile.Professions.OrderBy(entry => entry.Name))
        {
            if (builder.Length > 0) builder.AppendLine();

            var progress = profession.ExperienceToNextLevel < 0
                ? "MAX"
                : $"{profession.ExperienceToNextLevel:N0} XP to next level";

            builder.Append(
                $"{profession.Name}: Level {profession.Level}/{profession.MaximumLevel}  •  " +
                $"{profession.Experience:N0} total XP  •  {progress}"
            );
        }

        return builder.ToString();
    }
}
