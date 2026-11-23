using Intersect.Network;
using Intersect.Network.Packets.EquipmentLoadouts;
namespace Intersect.Client.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, EquipmentLoadoutStatePacket packet) => Interface.Interface.EnqueueInGame(ui => ui.ApplyEquipmentLoadouts(packet));
    public void HandlePacket(IPacketSender sender, EquipmentFeedbackPacket packet) => Interface.Interface.EnqueueInGame(_ => Interface.Game.EquipmentPresentation.Play(packet.Cue));
}
