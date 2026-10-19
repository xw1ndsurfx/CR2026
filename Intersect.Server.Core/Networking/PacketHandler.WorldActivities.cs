using Intersect.Network.Packets.WorldEvents;
using Intersect.Server.WorldEvents;
using Newtonsoft.Json;
namespace Intersect.Server.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, Intersect.Network.Packets.Client.SortBankPacket packet)
    { if (!client.IsEditor) client.Entity?.BankInterface?.SortItems(); }
    public void HandlePacket(Client client, RequestWorldActivitiesPacket packet)
    { if (client.IsEditor) client.Send(new WorldActivitiesPacket { Json = JsonConvert.SerializeObject(WorldActivitiesRuntime.Current), OpenEditor = true, Section = packet.Section }); }
    public void HandlePacket(Client client, SaveWorldActivitiesPacket packet)
    {
        if (!client.IsEditor) return;
        try
        {
            WorldActivitiesRuntime.Save(packet.Json, packet.Section);
            if (packet.RequestId != Guid.Empty)
                client.Send(new WorldActivitiesPacket { RequestId = packet.RequestId, Section = packet.Section, Saved = true });
        }
        catch (Exception e)
        {
            if (packet.RequestId != Guid.Empty)
                client.Send(new WorldActivitiesPacket { RequestId = packet.RequestId, Section = packet.Section, Error = e.Message });
            else PacketSender.SendError(client, e.Message, "World activities");
        }
    }
}
