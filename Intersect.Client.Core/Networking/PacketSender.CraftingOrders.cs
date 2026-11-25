using Intersect.Network.Packets.Client;

namespace Intersect.Client.Networking;

public static partial class PacketSender
{
    public static void RequestCraftingOrders() => Network.SendPacket(new RequestCraftingOrdersPacket());

    public static void PostCraftingOrder(Guid itemId, int quantity, int reward, int hours, Guid requestId = default) =>
        Network.SendPacket(new PostCraftingOrderPacket
        {
            ItemId = itemId, Quantity = quantity, Reward = reward, DurationHours = hours, RequestId = requestId,
        });

    public static void CraftingOrderAction(Guid id, byte action) =>
        Network.SendPacket(new CraftingOrderActionPacket { OrderId = id, Action = action });
}
