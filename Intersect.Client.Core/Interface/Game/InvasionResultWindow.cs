using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Network.Packets.WorldEvents;
namespace Intersect.Client.Interface.Game;
internal sealed class InvasionResultWindow(Canvas parent) : EventResultPanelWindow(parent, "Invasion Result", nameof(InvasionResultWindow))
{
    public void Apply(InvasionResultPacket result) => Present(
        ClientLanguages.Text(result.Victory ? "ISLAND DEFENDED!" : "INVASION LOST"),
        string.Join("\n\n", ClientLanguages.ContentText(result.Name),
            ClientLanguages.Format("Waves: {0}/{1}", result.WavesCompleted, result.WaveCount),
            ClientLanguages.Format("Objective HP remaining: {0:N0}", result.ObjectiveHealthRemaining),
            ClientLanguages.Format("Defenders: {0:N0}", result.ParticipantCount),
            ClientLanguages.Format("Damage: {0:N0}", result.ContributionDamage),
            ClientLanguages.Format("Effective healing: {0:N0}", result.ContributionHealing),
            ClientLanguages.Format("Your contribution: {0}%", result.ContributionPercent),
            result.Victory ? ClientLanguages.Format("Effort reward: {0}% of base EXP", result.RewardPercentOfBase) : ClientLanguages.Text("Effort recorded - no victory EXP")),
        result.ExperienceAwarded > 0 ? ClientLanguages.Format("+{0:N0} EXP", result.ExperienceAwarded) : ClientLanguages.Text("No EXP reward"), result.Victory);
}
