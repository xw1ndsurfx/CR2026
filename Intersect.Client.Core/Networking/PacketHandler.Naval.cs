using Intersect.Network;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender,NavalManagementPacket packet)=>Intersect.Client.Interface.Interface.EnqueueInGame(ui=>ui.ApplyNavalManagement(packet.Json));
    public void HandlePacket(IPacketSender sender, HarbourStatePacket packet) =>
        Intersect.Client.Interface.Interface.EnqueueInGame(ui => ui.ApplyHarbourState(packet));
    public void HandlePacket(IPacketSender sender, NavalSnapshotPacket packet) =>
        Intersect.Client.Interface.Interface.EnqueueInGame(_ => Naval.NavalWorld.Apply(packet));
}
