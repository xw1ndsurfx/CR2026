using Intersect.Network.Packets.Client;
using Intersect.Server.CraftingOrders;

namespace Intersect.Server.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, RequestCraftingOrdersPacket packet)
    {
        if (client.IsEditor || client.Entity is not { } player) return;
        lock (player.EntityLock) client.Send(CraftingOrdersRuntime.StateFor(player));
    }

    public void HandlePacket(Client client, PostCraftingOrderPacket packet)
    {
        if (client.IsEditor || client.Entity is not { } player) return;
        lock (player.EntityLock)
            client.Send(CraftingOrdersRuntime.Post(player, packet.ItemId, packet.Quantity, packet.Reward, packet.DurationHours));
    }

    public void HandlePacket(Client client, CraftingOrderActionPacket packet)
    {
        if (client.IsEditor || client.Entity is not { } player) return;
        lock (player.EntityLock)
            client.Send(CraftingOrdersRuntime.Act(player, packet.OrderId, packet.Action));
    }
}
