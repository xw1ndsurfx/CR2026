using Intersect.Network.Packets.PartyLobbies;
using Intersect.Server.PartyLobbies;
using Newtonsoft.Json;
namespace Intersect.Server.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client,LobbyConfigurationRequestPacket packet)
    {
        if(!client.IsEditor)return;
        try { if(packet.Save)LobbyConfigurationRuntime.Save(packet.Json);client.Send(new LobbyConfigurationPacket {Json=JsonConvert.SerializeObject(LobbyConfigurationRuntime.Current)}); }
        catch { client.Send(new LobbyConfigurationPacket {Message="Invalid lobby settings."}); }
    }
}
