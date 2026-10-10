using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Network.Packets.WorldEvents;

namespace Intersect.Client.Interface.Game;

internal sealed class WorldBossResultWindow : Window
{
    private readonly Label _title;
    private readonly Label _content;
    private readonly Label _experience;

    public WorldBossResultWindow(Canvas parent) : base(parent, "World Boss Results", false, nameof(WorldBossResultWindow))
    {
        SetSize(540, 440);
        Alignment = [Alignments.Center];
        DeleteOnClose = false;
        _title = MakeLabel("WorldBossResultTitle", 18, 45, 504, 45, 21);
        _content = MakeLabel("WorldBossResultDetails", 32, 110, 476, 220, 11);
        _experience = MakeLabel("WorldBossExperience", 32, 340, 476, 35, 18);
        var close = new Button(this, "Continue") { Text = "Continue" };
        close.SetBounds(180, 390, 180, 34);
        close.Clicked += (_, _) => Hide();
        Hide();
    }

    private Label MakeLabel(string name, int x, int y, int width, int height, int fontSize)
    {
        var label = new Label(this, name)
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = fontSize,
            TextAlign = Pos.Center,
            TextColorOverride = Color.White,
        };
        label.SetBounds(x, y, width, height);
        return label;
    }

    protected override void EnsureInitialized() { }

    public void Apply(WorldBossResultPacket packet)
    {
        _title.Text = packet.Victory ? "WORLD BOSS DEFEATED!" : "WORLD BOSS ESCAPED";
        _title.TextColorOverride = packet.Victory
            ? new Color(a: 255, r: 240, g: 205, b: 100)
            : new Color(a: 255, r: 225, g: 100, b: 95);
        _content.Text = packet.Name + "\n" +
            $"Damage dealt: {packet.ContributionDamage:N0} ({packet.ContributionPercent}%)\n" +
            $"Your rank: #{packet.Rank} / {packet.ParticipantCount}\n" +
            string.Join("\n", (packet.TopDamagers ?? [])
                .Take(5).Select((p, i) => $"{i + 1}. {p.PlayerName}: {p.Damage:N0}")) +
            "\n" + (packet.RewardQualified
                ? $"Reward: {packet.RewardPercentOfBase}% of base EXP"
                : "No participation EXP earned");
        _experience.Text = packet.ExperienceAwarded > 0
            ? $"+{packet.ExperienceAwarded:N0} EXP" : "NO EXP REWARD";
        Show();
        BringToFront();
    }
}
