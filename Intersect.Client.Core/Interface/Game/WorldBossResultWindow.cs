using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Network.Packets.WorldEvents;
namespace Intersect.Client.Interface.Game;
internal sealed class WorldBossResultWindow(Canvas parent) : EventResultPanelWindow(parent, "World Boss Results", nameof(WorldBossResultWindow))
{
    public void Apply(WorldBossResultPacket packet) => Present(
        ClientLanguages.Text(packet.Victory ? "WORLD BOSS DEFEATED!" : "WORLD BOSS ESCAPED"),
        string.Join("\n\n", ClientLanguages.ContentText(packet.Name),
            ClientLanguages.Format("Damage dealt: {0:N0} ({1}%)", packet.ContributionDamage, packet.ContributionPercent),
            ClientLanguages.Format("Your rank: #{0} / {1}", packet.Rank, packet.ParticipantCount),
            string.Join("\n", (packet.TopDamagers ?? []).Take(5).Select((p, i) => $"{i + 1}. {p.PlayerName}: {p.Damage:N0}")),
            packet.RewardQualified ? ClientLanguages.Format("Reward: {0}% of base EXP", packet.RewardPercentOfBase) : ClientLanguages.Text("No participation EXP earned")),
        packet.ExperienceAwarded > 0 ? ClientLanguages.Format("+{0:N0} EXP", packet.ExperienceAwarded) : ClientLanguages.Text("NO EXP REWARD"), packet.Victory);
}
