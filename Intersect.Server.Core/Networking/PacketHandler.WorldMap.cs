using Intersect.Network.Packets.Editor;
using Intersect.Network.Packets.Server;
using Intersect.Server.Maps;

namespace Intersect.Server.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, RequestWorldMapLegendPacket packet)
    {
        if (!client.IsEditor) return;
        client.Send(new WorldMapLegendPacket { LegendJson = WorldMapLegendStore.Json, OpenEditor = true });
    }

    public void HandlePacket(Client client, SaveWorldMapLegendPacket packet)
    {
        if (!client.IsEditor) return;
        try
        {
            if (packet.LegendJson is not { Length: > 0 and <= 100_000 })
                throw new InvalidDataException("World Map legend is empty or too large.");
            WorldMapLegendStore.Save(packet.LegendJson);
            Intersect.Server.Localization.ContentTranslationRuntime.QueueJson(packet.LegendJson);
            client.Send(new WorldMapLegendPacket { LegendJson = WorldMapLegendStore.Json });
        }
        catch (Exception exception)
        {
            PacketSender.SendError(client, exception.Message, "World Map Legend");
        }
    }
}
