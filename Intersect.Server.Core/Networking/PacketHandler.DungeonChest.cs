using Intersect.Network.Packets.Client;
namespace Intersect.Server.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, DungeonChestRequestPacket packet)
    { if (!client.IsEditor && client.Entity is {} player) Dungeons.DungeonRunRuntime.ClaimChest(player, packet.RunId); }
}
