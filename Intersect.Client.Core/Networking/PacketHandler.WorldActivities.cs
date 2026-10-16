using Intersect.Network;
using Intersect.Network.Packets.WorldEvents;
using Intersect.Framework.Core.WorldEvents;
using Newtonsoft.Json;
namespace Intersect.Client.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, ExperienceEventAnnouncementPacket packet)
    {
        var message = packet.Names.Length > 0 ? Intersect.Client.Localization.ClientLanguages.Format("Experience event: {0} (+{1}% XP)", string.Join(", ", packet.Names.Select(Intersect.Client.Localization.ClientLanguages.ContentText)), packet.BonusPercent) : Intersect.Client.Localization.ClientLanguages.Text("The experience event has ended.");
        Intersect.Client.Interface.Interface.EnqueueInGame(ui => ui.AnnouncementWindow.ShowAnnouncement(message, 6000));
    }
    public void HandlePacket(IPacketSender sender, WorldActivitiesPacket packet)
    { var config = JsonConvert.DeserializeObject<WorldActivitiesConfiguration>(packet.Json); if (config is { IsValid: true }) { Intersect.Client.WorldEvents.WorldMusic.Configuration = config; Intersect.Client.WorldEvents.WorldMusic.MapChanged(); } }
}
