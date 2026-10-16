using Intersect.Network.Packets.Housing;
using Intersect.Server.Housing;
using Newtonsoft.Json;
namespace Intersect.Server.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, HouseActionPacket packet)
    { if (!client.IsEditor && client.Entity is { } player) HousingRuntime.Handle(player, packet); }
    public void HandlePacket(Client client, RequestHousingConfigurationPacket packet)
    { if (client.IsEditor) client.Send(new HousingConfigurationPacket { Json = JsonConvert.SerializeObject(HousingRuntime.Configuration), OpenWindow = true }); }
    public void HandlePacket(Client client, SaveHousingConfigurationPacket packet)
    {
        if (!client.IsEditor) return;
        try { HousingRuntime.SaveConfiguration(packet.Json); client.Send(new HousingConfigurationPacket { Json = JsonConvert.SerializeObject(HousingRuntime.Configuration) }); }
        catch (Exception exception) { PacketSender.SendError(client, exception.Message, "Housing"); }
    }
}
