using Intersect.Network.Packets.Editor;
using Intersect.Network.Packets.Server;
using Intersect.Server.Professions;

namespace Intersect.Server.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, RequestProfessionConfigurationPacket packet)
    {
        if (!client.IsEditor) return;
        client.Send(new ProfessionConfigurationPacket(ProfessionConfigurationRuntime.Json, packet.OpenEditor));
    }

    public void HandlePacket(Client client, SaveProfessionConfigurationPacket packet)
    {
        if (!client.IsEditor) return;
        try
        {
            if (packet.ConfigurationJson is not { Length: > 0 and <= 2_000_000 })
                throw new InvalidDataException("Profession configuration is empty or too large.");

            ProfessionConfigurationRuntime.Save(packet.ConfigurationJson);
            client.Send(new ProfessionConfigurationPacket(ProfessionConfigurationRuntime.Json, openEditor: false));
        }
        catch (Exception exception)
        {
            PacketSender.SendError(client, exception.Message, "Professions");
        }
    }
}
