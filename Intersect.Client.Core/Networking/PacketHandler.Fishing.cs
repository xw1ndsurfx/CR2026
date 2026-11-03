using Intersect.Network;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, FishingStatePacket packet)
    {
        if (!packet.Editor) Interface.Interface.EnqueueInGame(game => game.ApplyFishingState(packet));
    }

    public void HandlePacket(IPacketSender sender, FishingWorldPacket packet) =>
        Interface.Interface.EnqueueInGame(_ => Interface.Game.FishingWorldPresentation.Apply(packet));
}
