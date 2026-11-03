using Intersect.Network;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, RoyalContractBoardPacket packet) =>
        Interface.Interface.EnqueueInGame(ui => ui.ApplyRoyalContractsState(packet));
}
