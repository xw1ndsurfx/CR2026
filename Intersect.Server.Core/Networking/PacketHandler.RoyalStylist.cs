using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Editor;
using Intersect.Network.Packets.Server;
using Intersect.Server.RoyalStylist;

namespace Intersect.Server.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(
        Client client,
        RequestRoyalStylistConfigurationPacket packet
    )
    {
        if (!client.IsEditor)
        {
            return;
        }

        client.Send(
            new RoyalStylistConfigurationPacket(
                RoyalStylistRuntime.Json,
                packet.OpenEditor
            )
        );
    }

    public void HandlePacket(
        Client client,
        SaveRoyalStylistConfigurationPacket packet
    )
    {
        if (!client.IsEditor)
        {
            return;
        }

        try
        {
            if (packet.ConfigurationJson is not { Length: > 0 and <= 4_000_000 })
            {
                throw new InvalidDataException(
                    "Royal Stylist configuration is empty or too large."
                );
            }

            RoyalStylistRuntime.Save(packet.ConfigurationJson);
            client.Send(
                new RoyalStylistConfigurationPacket(
                    RoyalStylistRuntime.Json,
                    openEditor: false
                )
            );
        }
        catch (Exception exception)
        {
            PacketSender.SendError(
                client,
                exception.Message,
                "Royal Stylist"
            );
        }
    }

    public void HandlePacket(
        Client client,
        ApplyRoyalStylistAppearancePacket packet
    )
    {
        if (client.IsEditor || client.Entity is not { } player)
        {
            return;
        }

        client.Send(
            RoyalStylistRuntime.Apply(
                player,
                packet.StylistId,
                packet.Appearance
            )
        );
    }
}
