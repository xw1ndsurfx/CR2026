using Intersect.Framework.Core.RoyalContracts;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Editor;
using Intersect.Network.Packets.Server;
using Intersect.Server.RoyalContracts;

namespace Intersect.Server.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, RoyalContractRequestPacket packet)
    {
        if (!client.IsEditor && client.Entity is { } player)
            RoyalContractsRuntime.Request(player, packet.Action, packet.ContractId);
    }

    public void HandlePacket(Client client, RoyalContractsConfigurationRequestPacket packet)
    {
        if (!client.IsEditor) return;
        var message = "";
        try
        {
            if (packet.Save)
            {
                if (packet.ConfigurationJson is not { Length: > 0 and <= 1_000_000 }) throw new InvalidDataException();
                RoyalContractsRuntime.SaveConfiguration(packet.ConfigurationJson);
                message = "Configuration saved. Changes apply at the next rotation.";
            }
        }
        catch (Exception exception)
        {
            client.Send(new RoyalContractsConfigurationPacket { Message = exception is InvalidDataException &&
                exception.Message is "Invalid contract configuration." or "Invalid contract template." or "A contract references a missing or invalid target or reward item."
                    ? exception.Message : "Configuration could not be saved." });
            return;
        }
        client.Send(new RoyalContractsConfigurationPacket
        {
            OpenEditor = packet.OpenEditor, ConfigurationJson = RoyalContractRules.Json(RoyalContractsRuntime.Current), Message = message,
        });
    }
}
