using Intersect.Network.Packets.Client;
using Intersect.Utilities;
using Intersect.Framework.Core;
using Intersect.Network.Packets.Editor;

namespace Intersect.Server.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, NavalConfigurationRequestPacket packet)
    {
        if (!client.IsEditor) return;
        var response = Naval.NavalSystem.Configure(packet);
        response.RequestId = packet.RequestId;
        client.Send(response);
    }
    public void HandlePacket(Client client, PrepareNavalMapPacket packet)
    {
        if (client.IsEditor) client.Send(Naval.NavalSystem.PrepareMap(packet.MapId));
    }
    public void HandlePacket(Client client, HarbourRequestPacket packet)
    {
        if (!client.IsEditor && client.Entity is { InGame: true } player && (packet.ModelId?.Length ?? 0) <= 64)
            Naval.NavalSystem.Harbour(player, packet);
    }
    public void HandlePacket(Client client, ShipActionPacket packet)
    {
        if (!client.IsEditor && client.Entity is { InGame: true } player)
            Naval.NavalSystem.Action(player,packet,Timing.Global.Milliseconds);
    }
    public void HandlePacket(Client client, ShipInputPacket packet)
    {
        if (client.Entity is { InGame: true } player)
            Naval.NavalSystem.Input(player, packet, Timing.Global.Milliseconds);
    }
}
