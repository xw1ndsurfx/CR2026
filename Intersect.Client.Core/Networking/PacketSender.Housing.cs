using Intersect.Network.Packets.Housing;
namespace Intersect.Client.Networking;
public static partial class PacketSender
{
    public static void SendHouseAction(HouseAction action, Guid owner = default, Guid id = default, Guid placement = default, int x = 0, int y = 0, int rotation = 0, bool open = false) =>
        Network.SendPacket(new HouseActionPacket { Action = action, OwnerId = owner, Id = id, PlacementId = placement, X = x, Y = y, Rotation = rotation, Public = open });
}
