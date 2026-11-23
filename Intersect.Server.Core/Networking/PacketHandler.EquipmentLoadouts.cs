using Intersect.Network.Packets.EquipmentLoadouts;
namespace Intersect.Server.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, EquipmentLoadoutRequestPacket packet)
    {
        if (!client.IsEditor && client.Entity is {} player) player.HandleEquipmentLoadout(packet);
    }

    public void HandlePacket(Client client, UseEquipmentSpellPacket packet)
    {
        if (client.IsEditor || client.Entity is not {} player || !player.InGame) return;
        lock (player.EntityLock)
        {
            var target = packet.TargetId == Guid.Empty ? null : Intersect.Server.Maps.MapController.GetSurroundingMapInstances(player.MapId, player.MapInstanceId, true)
                .SelectMany(m => m.GetEntities()).FirstOrDefault(e => e.Id == packet.TargetId);
            player.BeginEquipmentSpell(target);
        }
    }
}
