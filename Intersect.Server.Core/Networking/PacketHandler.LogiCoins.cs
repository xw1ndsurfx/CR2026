using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Editor;
using Intersect.Network.Packets.Server;
using Intersect.Server.LogiCoins;

namespace Intersect.Server.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, RequestLogiCoinShopConfigurationPacket packet)
    {
        if (!client.IsEditor)
        {
            return;
        }

        client.Send(
            new LogiCoinShopConfigurationPacket(
                LogiCoinShopRuntime.Json,
                openEditor: true
            )
        );
    }

    public void HandlePacket(Client client, SaveLogiCoinShopConfigurationPacket packet)
    {
        if (!client.IsEditor)
        {
            return;
        }

        try
        {
            if (packet.ConfigurationJson is not { Length: > 0 and <= 4_000_000 })
            {
                throw new InvalidDataException("LogiCoin Shop configuration is empty or too large.");
            }

            LogiCoinShopRuntime.Save(packet.ConfigurationJson);
            client.Send(
                new LogiCoinShopConfigurationPacket(
                    LogiCoinShopRuntime.Json,
                    openEditor: false
                )
            );
        }
        catch (Exception exception)
        {
            PacketSender.SendError(client, exception.Message, "LogiCoin Shop");
        }
    }

    public void HandlePacket(Client client, RequestLogiCoinShopStatePacket packet)
    {
        if (client.IsEditor || client.Entity is not { } player)
        {
            return;
        }

        client.Send(LogiCoinPurchaseRuntime.BuildState(player));
    }

    public void HandlePacket(Client client, PurchaseLogiCoinOfferPacket packet)
    {
        if (client.IsEditor || client.Entity is not { } player)
        {
            return;
        }

        lock (player.EntityLock)
        {
            client.Send(
                LogiCoinPurchaseRuntime.Purchase(
                    player,
                    packet.OfferId,
                    packet.PurchaseId
                )
            );
        }
    }
}
