using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Network.Packets.WorldEvents;

namespace Intersect.Client.Interface.Game;

internal sealed class InvasionResultWindow : Window
{
    private readonly Label _headline;
    private readonly Label _summary;
    private readonly Label _experience;
    private readonly Button _close;

    public InvasionResultWindow(Canvas parent) : base(parent, "Invasion Result", false, nameof(InvasionResultWindow))
    {
        SetSize(520, 380);
        Alignment = [Alignments.Center];
        DeleteOnClose = false;

        _headline = new Label(this, "Headline")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 24,
            TextAlign = Pos.Center,
        };
        _headline.SetBounds(20, 48, 480, 44);

        _summary = new Label(this, "Summary")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 12,
            TextAlign = Pos.Center,
            TextColorOverride = Color.White,
        };
        _summary.SetBounds(32, 102, 456, 132);

        _experience = new Label(this, "Experience")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 20,
            TextAlign = Pos.Center,
        };
        _experience.SetBounds(32, 242, 456, 42);

        _close = new Button(this, "Close")
        {
            Text = "Continue",
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 13,
        };
        _close.SetBounds(170, 308, 180, 38);
        _close.Clicked += (_, _) => Hide();

        Hide();
    }

    protected override void EnsureInitialized()
    {
    }

    public void Apply(InvasionResultPacket result)
    {
        _headline.Text = result.Victory ? "ISLAND DEFENDED!" : "INVASION LOST";
        _headline.TextColorOverride = result.Victory
            ? new Color(a: 255, r: 245, g: 214, b: 110)
            : new Color(a: 255, r: 235, g: 105, b: 90);

        _summary.Text =
            $"{result.Name}\n" +
            $"Waves: {result.WavesCompleted}/{result.WaveCount}\n" +
            $"Objective HP remaining: {result.ObjectiveHealthRemaining:N0}\n" +
            $"Defenders: {result.ParticipantCount:N0}\n" +
            $"Your contribution: {result.ContributionPercent}% ({result.ContributionDamage:N0} damage)\n" +
            (result.Victory
                ? $"Effort reward: {result.RewardPercentOfBase}% of base EXP"
                : "Effort recorded - no victory EXP");

        _experience.Text = result.ExperienceAwarded > 0
            ? $"+{result.ExperienceAwarded:N0} EXP"
            : "No EXP reward";
        _experience.TextColorOverride = result.ExperienceAwarded > 0
            ? new Color(a: 255, r: 110, g: 225, b: 115)
            : new Color(a: 255, r: 190, g: 180, b: 170);

        Show();
        BringToFront();
    }
}
