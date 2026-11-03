using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Archery;
namespace Intersect.Server.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, ArcheryRequestPacket packet)
    {
        if (client.IsEditor)
        {
            if (packet.Action is not (ArcheryAction.GetConfiguration or ArcheryAction.SaveConfiguration)) return;
            try
            {
                if (packet.Action == ArcheryAction.SaveConfiguration) RoyalCombatRuntime.Save(packet.ConfigurationJson);
                client.Send(new ArcheryStatePacket { Editor = true, Open = true, RequestId = packet.RequestId,
                    Saved = packet.Action == ArcheryAction.SaveConfiguration, ConfigurationJson = RoyalCombatRuntime.Current.ToJson() });
            }
            catch (Exception e) when (e is IOException or System.Text.Json.JsonException or ArgumentException)
            { client.Send(new ArcheryStatePacket { Editor = true, RequestId = packet.RequestId, Message = "Invalid royal combat configuration." }); }
            return;
        }
        if (client.Entity is { InGame: true } player) player.HandleArcheryRequest(packet);
    }
}
