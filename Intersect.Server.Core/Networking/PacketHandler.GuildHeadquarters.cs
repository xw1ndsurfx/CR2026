using Intersect.Network.Packets.GuildHeadquarters;
using Intersect.Server.GuildHeadquarters;
using Newtonsoft.Json;
namespace Intersect.Server.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, HeadquartersRequestPacket packet)
    { if (!client.IsEditor && client.Entity is {} player) HeadquartersRuntime.Handle(player, packet); }
    public void HandlePacket(Client client, HeadquartersConfigurationRequestPacket packet)
    { if (client.IsEditor) SendHeadquartersConfiguration(client, true); }
    public void HandlePacket(Client client, HeadquartersConfigurationSavePacket packet)
    {
        if (!client.IsEditor) return;
        try
        {
            HeadquartersRuntime.Store.Save(packet.Json, packet.Revision, HeadquartersRuntime.Validate);
            Localization.ContentTranslationRuntime.QueueJson(packet.Json);
            SendHeadquartersConfiguration(client, false);
        }
        catch (Exception exception)
        { SendHeadquartersConfiguration(client, false, exception is InvalidOperationException ? exception.Message : "Headquarters could not be saved. Please try again."); }
    }
    private static void SendHeadquartersConfiguration(Client client, bool open, string error = "")
    {
        lock (HeadquartersRuntime.Store.Gate) client.Send(new HeadquartersConfigurationPacket {
            Json = JsonConvert.SerializeObject(HeadquartersRuntime.Store.Configuration), Revision = HeadquartersRuntime.Store.Revision, Open = open, Error = error });
    }
}
