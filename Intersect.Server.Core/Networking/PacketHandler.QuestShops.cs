using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Editor;
using Intersect.Network.Packets.Server;
using Intersect.Server.QuestShops;

namespace Intersect.Server.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, RequestQuestShopConfigurationPacket packet)
    {
        if (!client.IsEditor)
            return;

        client.Send(new QuestShopConfigurationPacket(QuestShopRuntime.Json, packet.OpenEditor));
    }

    public void HandlePacket(Client client, SaveQuestShopConfigurationPacket packet)
    {
        if (!client.IsEditor)
            return;

        try
        {
            if (packet.ConfigurationJson is not { Length: > 0 and <= 4_000_000 })
                throw new InvalidDataException("Quest Shop configuration is empty or too large.");

            QuestShopRuntime.Save(packet.ConfigurationJson);
            client.Send(new QuestShopConfigurationPacket(QuestShopRuntime.Json, openEditor: false));
        }
        catch (Exception exception)
        {
            PacketSender.SendError(client, exception.Message, "Quest Shops");
        }
    }

    public void HandlePacket(Client client, RequestQuestShopOfferPacket packet)
    {
        if (client.IsEditor || client.Entity is not { } player)
            return;

        if (!QuestShopRuntime.TryOffer(player, packet.ShopId, packet.QuestId, out var error))
            client.Send(QuestShopRuntime.Open(player, packet.ShopId, error));
    }
}
